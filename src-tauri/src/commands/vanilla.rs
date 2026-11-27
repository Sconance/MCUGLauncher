//! Tauri 原版版本查询与安装任务。

use std::{
    fs, io,
    path::{Path, PathBuf},
    thread,
    time::Duration,
};

use tauri::{AppHandle, Manager, State};
use time::{OffsetDateTime, format_description::well_known::Rfc3339};

use super::implementation::generated_id;
use crate::{
    core::{
        game_sources::available_versions,
        vanilla_download::{download_vanilla, safe_component},
    },
    demo::StateResource,
    local_instances,
    model::{ApiError, ResourceKeyInfo, SavedInfo, TaskInfo, TaskProgressInfo, VanillaVersionInfo},
    schema_dto::generated::tasks::{ResourceKeyInfo as TaskResourceKey, TaskInfoError, TaskResultInfo},
    state::Backend,
};

/// 读取原版版本清单。
#[tauri::command]
pub async fn list_vanilla_versions() -> Result<Vec<VanillaVersionInfo>, ApiError> {
    tauri::async_runtime::spawn_blocking(|| {
        let client = minecraft_client()?;
        Ok(available_versions(&client)
            .map_err(|error| download_error("NETWORK_ERROR", error))?
            .into_iter()
            .map(|version| VanillaVersionInfo {
                id: version.id,
                type_: version.kind,
                released: version.release_time,
            })
            .collect())
    })
    .await
    .map_err(|error| download_error("INTERNAL_ERROR", error))?
}

/// 提交原版安装任务，版本 ID 同时作为目标实例名。
#[tauri::command]
pub fn install_vanilla(
    app: AppHandle,
    backend: State<'_, Backend>,
    minecraft_version: String,
) -> Result<SavedInfo, ApiError> {
    if !safe_component(&minecraft_version) {
        return Err(ApiError::validation("无法安装原版。", "minecraftVersion", "版本 ID 无效。"));
    }
    let root = PathBuf::from(backend.data.lock().unwrap().state.config.root());
    if !root.is_absolute() {
        return Err(ApiError::validation("无法安装原版。", "root", "请先选择游戏目录。"));
    }
    fs::create_dir_all(&root).map_err(|error| download_error("IO_ERROR", error))?;
    let root = root.canonicalize().map_err(|error| download_error("IO_ERROR", error))?;
    let destination = root.join("versions").join(&minecraft_version);
    match fs::symlink_metadata(&destination) {
        Ok(_) => return Err(download_error("CONFLICT", "目标版本目录已存在。")),
        Err(error) if error.kind() == io::ErrorKind::NotFound => {}
        Err(error) => return Err(download_error("IO_ERROR", error)),
    }

    let instance_id = format!("local:{}", destination.display());
    let mut data = backend.data.lock().unwrap();
    let task_id = match data.tasks.iter().find(|task| {
        task.kind == "installVanilla"
            && task.instance_id.as_deref() == Some(instance_id.as_str())
            && !matches!(task.status.as_str(), "succeeded" | "failed" | "cancelled")
    }) {
        Some(task) => task.id.clone(),
        None => {
            let id = generated_id("install-vanilla");
            let now = download_timestamp();
            data.tasks.insert(0, TaskInfo {
                id: id.clone(),
                name: format!("安装 Minecraft {minecraft_version}"),
                kind: "installVanilla".into(),
                instance_id: Some(instance_id.clone()),
                status: "queued".into(),
                phase: "waiting".into(),
                created_at: now.clone(),
                updated_at: now,
                ..Default::default()
            });
            data.touch(StateResource::Tasks);
            let worker_id = id.clone();
            let started = thread::Builder::new().name("vanilla-install".into()).spawn(move || {
                run_vanilla_install(&app, root, minecraft_version, instance_id, worker_id);
            });
            if let Err(error) = started {
                data.tasks.retain(|task| task.id != id);
                return Err(download_error("INTERNAL_ERROR", error));
            }
            id
        }
    };
    Ok(SavedInfo::changed("安装请求已提交。", vec![download_resource("tasks")], vec![task_id]))
}

fn minecraft_client() -> Result<reqwest::blocking::Client, ApiError> {
    reqwest::blocking::Client::builder()
        .timeout(Duration::from_secs(90))
        .user_agent(concat!("MCUGLauncher/", env!("CARGO_PKG_VERSION")))
        .build()
        .map_err(|error| download_error("NETWORK_ERROR", error))
}

fn download_error(code: &str, message: impl std::fmt::Display) -> ApiError {
    ApiError {
        code: code.into(),
        message: message.to_string(),
        retryable: matches!(code, "NETWORK_ERROR" | "IO_ERROR" | "INTERNAL_ERROR"),
        field_errors: Default::default(),
    }
}

fn download_timestamp() -> String {
    OffsetDateTime::now_utc().format(&Rfc3339).unwrap_or_default()
}

fn download_resource(kind: &str) -> ResourceKeyInfo {
    ResourceKeyInfo { kind: kind.into(), instance_id: None }
}

fn run_vanilla_install(
    app: &AppHandle,
    root: PathBuf,
    version: String,
    instance_id: String,
    task_id: String,
) {
    let backend = app.state::<Backend>();
    let result = minecraft_client().and_then(|client| {
        download_vanilla(&client, &root, &version, |phase, progress| {
            loop {
                let mut data = backend.data.lock().unwrap();
                let task = data.tasks.iter_mut().find(|task| task.id == task_id)
                    .ok_or_else(|| io::Error::new(io::ErrorKind::Interrupted, "任务已移除。"))?;
                match task.status.as_str() {
                    "cancelling" => return Err(io::Error::new(io::ErrorKind::Interrupted, "安装已取消。")),
                    "paused" => {
                        drop(data);
                        thread::sleep(Duration::from_millis(100));
                    }
                    _ => {
                        task.status = "running".into();
                        task.phase = phase.into();
                        task.progress = progress.map(|(completed, total)| TaskProgressInfo {
                            completed, total: Some(total), unit: "items".into(),
                        });
                        task.updated_at = download_timestamp();
                        data.touch(StateResource::Tasks);
                        return Ok(());
                    }
                }
            }
        })
        .map_err(|error| {
            let code = match error.kind() {
                io::ErrorKind::NotFound => "NOT_FOUND",
                io::ErrorKind::AlreadyExists => "CONFLICT",
                _ if error.get_ref().is_some_and(|source| source.is::<reqwest::Error>()) => "NETWORK_ERROR",
                _ => "IO_ERROR",
            };
            download_error(code, error)
        })
    });

    let mut data = backend.data.lock().unwrap();
    let mut changed = Vec::new();
    if result.is_ok() && Path::new(data.state.config.root()).canonicalize().ok().as_ref() == Some(&root) {
        match local_instances::scan(&root) {
            Ok(catalog) => {
                data.replace_instance_catalog(catalog);
                changed.push(TaskResourceKey { kind: "instances".into(), instance_id: None });
            }
            Err(error) => eprintln!("[mcugl] 安装后扫描实例失败：{error}"),
        }
    }
    let Some(task) = data.tasks.iter_mut().find(|task| task.id == task_id) else { return };
    if task.status == "cancelling" {
        task.status = "cancelled".into();
    } else if let Err(error) = result {
        task.status = "failed".into();
        task.error = Some(TaskInfoError {
            code: error.code,
            message: error.message,
            retryable: error.retryable,
            field_errors: error.field_errors,
        });
    } else {
        task.status = "succeeded".into();
        task.result = Some(TaskResultInfo { changed, entity_ids: vec![instance_id] });
    }
    task.phase = task.status.clone();
    task.updated_at = download_timestamp();
    data.touch(StateResource::Tasks);
}
