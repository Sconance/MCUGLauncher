//! 模组下载令牌、后台任务和安全落盘。

use std::{
    collections::HashMap,
    fs, io,
    path::{Path, PathBuf},
    thread,
    time::{Duration, Instant},
};

use tauri::{AppHandle, Manager, State};

use super::implementation::generated_id;
use crate::{
    core::{
        file_download::{FileIntegrity, stage_file, verify_file},
        mod_sources::{ModVersion, download_client},
    },
    demo::StateResource,
    model::{ApiError, ResourceKeyInfo, SavedInfo, TaskInfo, TaskProgressInfo},
    schema_dto::generated::tasks::{TaskInfoError, TaskResultInfo},
    state::Backend,
};

const TOKEN_LIFETIME: Duration = Duration::from_secs(600);

#[derive(Default)]
pub(crate) struct ModDownloadState {
    plans: HashMap<String, (Instant, Vec<ModVersion>)>,
    active: HashMap<PathBuf, String>,
}

impl ModDownloadState {
    pub(super) fn remember(&mut self, versions: Vec<ModVersion>) -> String {
        let token = generated_id("mod-versions");
        let now = Instant::now();
        self.plans
            .retain(|_, (time, _)| now.duration_since(*time) < TOKEN_LIFETIME);
        self.plans.insert(token.clone(), (now, versions));
        token
    }

    fn version(&self, token: &str, version_id: &str) -> Result<ModVersion, ApiError> {
        let (_, versions) = self
            .plans
            .get(token)
            .filter(|(created, _)| created.elapsed() < TOKEN_LIFETIME)
            .ok_or_else(|| error("PLAN_EXPIRED", "版本列表已过期，请重新读取。"))?;
        versions
            .iter()
            .find(|version| version.id == version_id)
            .cloned()
            .ok_or_else(|| error("NOT_FOUND", "该版本不在本次查询结果内。"))
    }
}

#[tauri::command]
pub fn queue_content_version(
    app: AppHandle,
    backend: State<'_, Backend>,
    token: String,
    version_id: String,
    mode: Option<String>,
    path: Option<String>,
) -> Result<SavedInfo, ApiError> {
    if !token.starts_with("mod-versions-") {
        return Err(ApiError::unsupported("queue_content_version"));
    }
    if mode.as_deref() != Some("local") {
        return Err(error(
            "UNSUPPORTED",
            "请使用「下载到目录」。安装到实例需要提供目标实例 ID。",
        ));
    }
    let folder = path
        .as_deref()
        .map(Path::new)
        .filter(|path| path.is_absolute())
        .ok_or_else(|| ApiError::validation("无法下载模组。", "path", "请选择下载目录。"))?;
    let folder = folder.canonicalize().map_err(io_error)?;
    if !folder.is_dir() {
        return Err(ApiError::validation(
            "无法下载模组。",
            "path",
            "目标不是目录。",
        ));
    }

    let mut store = backend.mod_downloads.lock().unwrap();
    let version = store.version(&token, &version_id)?;
    let destination = folder.join(&version.file.filename);
    if let Some(task_id) = store.active.get(&destination) {
        return Err(error("BUSY", format!("此文件已有下载任务（{task_id}）。")));
    }
    let id = generated_id("download-mod");
    let now = timestamp();
    let mut data = backend.data.lock().unwrap();
    data.tasks.insert(
        0,
        TaskInfo {
            id: id.clone(),
            name: format!("下载 {}", version.name),
            kind: "downloadMod".into(),
            status: "queued".into(),
            phase: "waiting".into(),
            created_at: now.clone(),
            updated_at: now,
            ..Default::default()
        },
    );
    store.active.insert(destination.clone(), id.clone());
    let worker_id = id.clone();
    let worker_destination = destination.clone();
    if let Err(failure) = thread::Builder::new()
        .name("mod-download".into())
        .spawn(move || run(&app, &worker_id, &worker_destination, &version))
    {
        data.tasks.retain(|task| task.id != id);
        store.active.remove(&destination);
        return Err(error("INTERNAL_ERROR", failure));
    }
    data.touch(StateResource::Tasks);
    Ok(SavedInfo::changed(
        "模组下载已提交。",
        vec![ResourceKeyInfo {
            kind: "tasks".into(),
            instance_id: None,
        }],
        vec![id],
    ))
}

fn run(app: &AppHandle, task_id: &str, destination: &Path, version: &ModVersion) {
    let backend = app.state::<Backend>();
    let result = stage_and_publish(&backend, task_id, destination, version);
    let mut store = backend.mod_downloads.lock().unwrap();
    let mut data = backend.data.lock().unwrap();
    store.active.remove(destination);
    let Some(task) = data.tasks.iter_mut().find(|task| task.id == task_id) else {
        return;
    };
    if task.status == "cancelling" {
        task.status = "cancelled".into();
    } else if let Err(failure) = result {
        task.status = "failed".into();
        task.error = Some(TaskInfoError {
            code: failure.code,
            message: failure.message,
            retryable: failure.retryable,
            field_errors: failure.field_errors,
        });
    } else {
        task.status = "succeeded".into();
        task.progress = Some(TaskProgressInfo {
            completed: version.file.size,
            total: Some(version.file.size),
            unit: "bytes".into(),
        });
        task.result = Some(TaskResultInfo {
            changed: Vec::new(),
            entity_ids: vec![version.id.clone()],
        });
    }
    task.phase = task.status.clone();
    task.updated_at = timestamp();
    data.touch(StateResource::Tasks);
}

fn stage_and_publish(
    backend: &Backend,
    task_id: &str,
    destination: &Path,
    version: &ModVersion,
) -> Result<(), ApiError> {
    phase(backend, task_id, "downloading")?;
    let file = &version.file;
    let integrity = FileIntegrity {
        size: Some(file.size),
        sha1: &file.sha1,
    };
    match fs::symlink_metadata(destination) {
        Ok(metadata) if metadata.is_file() && verify_file(destination, &integrity).is_ok() => {
            return phase(backend, task_id, "committing");
        }
        Ok(_) => return Err(error("CONFLICT", "目标目录已有同名文件。")),
        Err(failure) if failure.kind() == io::ErrorKind::NotFound => {}
        Err(failure) => return Err(io_error(failure)),
    }
    let folder = destination.parent().expect("validated absolute path");
    let client =
        download_client().map_err(|failure| error("NETWORK_ERROR", failure.without_url()))?;
    let staged = stage_file(&client, file.url.as_str(), folder, &integrity).map_err(io_error)?;
    phase(backend, task_id, "committing")?;
    staged
        .persist_noclobber(destination)
        .map_err(|failure| io_error(failure.error))?;
    Ok(())
}

fn phase(backend: &Backend, task_id: &str, phase: &str) -> Result<(), ApiError> {
    loop {
        let mut data = backend.data.lock().unwrap();
        let task = data
            .tasks
            .iter_mut()
            .find(|task| task.id == task_id)
            .ok_or_else(|| error("NOT_FOUND", "下载任务不存在。"))?;
        match task.status.as_str() {
            "cancelling" => return Err(error("CONFLICT", "模组下载已取消。")),
            "paused" => {
                drop(data);
                thread::sleep(Duration::from_millis(100));
            }
            _ => {
                task.status = "running".into();
                task.phase = phase.into();
                task.updated_at = timestamp();
                data.touch(StateResource::Tasks);
                return Ok(());
            }
        }
    }
}

fn timestamp() -> String {
    time::OffsetDateTime::now_utc()
        .format(&time::format_description::well_known::Rfc3339)
        .unwrap_or_default()
}

fn io_error(failure: io::Error) -> ApiError {
    let code = match failure.kind() {
        io::ErrorKind::AlreadyExists => "CONFLICT",
        _ if failure
            .get_ref()
            .is_some_and(|source| source.is::<reqwest::Error>()) =>
        {
            "NETWORK_ERROR"
        }
        _ => "IO_ERROR",
    };
    error(code, failure)
}

pub(super) fn error(code: &str, failure: impl std::fmt::Display) -> ApiError {
    ApiError {
        code: code.into(),
        message: failure.to_string(),
        retryable: matches!(code, "NETWORK_ERROR" | "INTERNAL_ERROR" | "IO_ERROR"),
        field_errors: Default::default(),
    }
}

#[cfg(test)]
mod tests {
    use std::{io::Write, net::TcpListener};

    use super::*;
    use crate::core::mod_sources::ModFile;

    #[test]
    fn publishes_verified_file_and_preserves_conflicts() {
        let directory = tempfile::tempdir().unwrap();
        let backend = Backend::open(directory.path().join("data"), || {
            Ok(directory.path().join("game"))
        })
        .unwrap();
        backend.data.lock().unwrap().tasks.push(TaskInfo {
            id: "task".into(),
            status: "queued".into(),
            ..Default::default()
        });
        let listener = TcpListener::bind("127.0.0.1:0").unwrap();
        let url = format!("http://{}/mod.jar", listener.local_addr().unwrap());
        let server = thread::spawn(move || {
            let (mut stream, _) = listener.accept().unwrap();
            stream
                .write_all(b"HTTP/1.1 200 OK\r\nContent-Length: 3\r\nConnection: close\r\n\r\nabc")
                .unwrap();
        });
        let version = ModVersion {
            id: "provider:version".into(),
            name: "Example mod".into(),
            file: ModFile {
                filename: "mod.jar".into(),
                url: reqwest::Url::parse(&url).unwrap(),
                size: 3,
                sha1: "a9993e364706816aba3e25717850c26c9cd0d89d".into(),
            },
            game_versions: vec!["1.21.4".into()],
            loaders: vec!["fabric".into()],
            version_type: "release".into(),
        };
        let destination = directory.path().join("mod.jar");
        fs::write(&destination, b"old").unwrap();
        let failure = stage_and_publish(&backend, "task", &destination, &version).unwrap_err();
        assert_eq!(failure.code, "CONFLICT");
        assert_eq!(fs::read(&destination).unwrap(), b"old");

        fs::remove_file(&destination).unwrap();
        stage_and_publish(&backend, "task", &destination, &version).unwrap();
        assert_eq!(fs::read(&destination).unwrap(), b"abc");
        server.join().unwrap();
    }
}
