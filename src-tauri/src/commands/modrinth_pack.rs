//! 本地 Modrinth 整合包的预览计划、任务与实例登记。

use std::{
    fs, io,
    path::{Path, PathBuf},
    thread,
};

use tauri::{AppHandle, Manager, State};
use time::{Duration, OffsetDateTime, format_description::well_known::Rfc3339};

use super::{
    implementation::generated_id,
    vanilla::{
        VersionInstall, download_error, download_resource, download_timestamp, finish_install,
        install_error, minecraft_client, report_install,
    },
};
use crate::{
    core::{modrinth_pack::Archive, vanilla_download::safe_component},
    demo::StateResource,
    local_instances,
    model::{ApiError, PackPreviewInfo, SavedInfo, TaskInfo},
    schema_dto::generated::tasks::ResourceKeyInfo as TaskResourceKey,
    state::Backend,
};

#[derive(Clone)]
pub(crate) struct PackPlan {
    path: PathBuf,
    fingerprint: [u8; 32],
    expires: OffsetDateTime,
    settings_revision: u64,
    profile: String,
    submission: Option<PackSubmission>,
}

#[derive(Clone)]
struct PackSubmission {
    destination: PathBuf,
    task_id: String,
}

impl PackPlan {
    fn retained(&self, now: OffsetDateTime, tasks: &[TaskInfo]) -> bool {
        self.expires > now
            || self
                .submission
                .as_ref()
                .is_some_and(|submission| tasks.iter().any(|task| task.id == submission.task_id))
    }
}

#[tauri::command]
pub async fn preview_pack(
    backend: State<'_, Backend>,
    path: String,
) -> Result<PackPreviewInfo, ApiError> {
    let (path, fingerprint, name, request) = tauri::async_runtime::spawn_blocking(move || {
        let path = PathBuf::from(path);
        if !path.is_absolute() {
            return Err(ApiError::validation(
                "无法预览整合包。",
                "path",
                "请选择本地文件。",
            ));
        }
        let path = path.canonicalize().map_err(install_error)?;
        let pack = Archive::open(&path).map_err(|error| {
            if error.kind() == io::ErrorKind::InvalidData {
                ApiError::validation("无法预览 Modrinth 整合包。", "path", error.to_string())
            } else {
                install_error(error)
            }
        })?;
        let request = VersionInstall::from_pack(&pack.manifest.dependencies)?;
        Ok((path, pack.fingerprint, pack.manifest.name, request))
    })
    .await
    .map_err(|error| download_error("INTERNAL_ERROR", error))??;

    let profile = request.profile();
    let name = if safe_component(&name) {
        name
    } else {
        profile.clone()
    };
    let (game, loader) = request.pack_details();
    let mut plans = backend.pack_plans.lock().unwrap();
    let now = OffsetDateTime::now_utc();
    let (settings_revision, root) = {
        let data = backend.data.lock().unwrap();
        plans.retain(|_, plan| plan.retained(now, &data.tasks));
        let parent = Path::new(data.state.config.root())
            .parent()
            .filter(|path| path.is_dir());
        (
            data.revisions.settings,
            parent
                .unwrap_or(backend.config_dir())
                .to_string_lossy()
                .into_owned(),
        )
    };
    let expires = now + Duration::minutes(10);
    let plan_id = generated_id("import-pack");
    let target_id = path.to_string_lossy().into_owned();
    plans.insert(
        plan_id.clone(),
        PackPlan {
            path,
            fingerprint,
            expires,
            settings_revision,
            profile,
            submission: None,
        },
    );
    Ok(PackPreviewInfo {
        plan_id,
        target_id,
        target_revision: i64::try_from(settings_revision).unwrap_or(i64::MAX),
        expires_at: expires.format(&Rfc3339).unwrap_or_default(),
        name,
        root,
        game: game.into(),
        loader: loader.into(),
    })
}

#[tauri::command]
pub fn confirm_pack(
    app: AppHandle,
    backend: State<'_, Backend>,
    plan_id: String,
    name: String,
    directory: String,
) -> Result<SavedInfo, ApiError> {
    if !safe_component(&name) {
        return Err(ApiError::validation(
            "无法导入整合包。",
            "name",
            "名称不能用作目录名。",
        ));
    }
    let directory = Path::new(&directory);
    if !directory.is_absolute() || !directory.is_dir() {
        return Err(ApiError::validation(
            "无法导入整合包。",
            "directory",
            "请选择已存在的安装父目录。",
        ));
    }
    let destination = directory.canonicalize().map_err(install_error)?.join(&name);
    let mut plans = backend.pack_plans.lock().unwrap();
    let mut data = backend.data.lock().unwrap();
    let now = OffsetDateTime::now_utc();
    plans.retain(|_, plan| plan.retained(now, &data.tasks));
    let plan = plans
        .get_mut(&plan_id)
        .ok_or_else(|| download_error("PLAN_EXPIRED", "请重新预览整合包。"))?;
    if let Some(submission) = &plan.submission {
        if destination != submission.destination {
            return Err(download_error("CONFLICT", "整合包计划已用于其他目标目录。"));
        }
        if data.tasks.iter().any(|task| task.id == submission.task_id) {
            return Ok(accepted(submission.task_id.clone()));
        }
        return Err(download_error(
            "PLAN_EXPIRED",
            "安装任务已移除，请重新预览整合包。",
        ));
    }
    data.ensure_settings_revision(plan.settings_revision)?;
    match fs::symlink_metadata(&destination) {
        Ok(_) => return Err(download_error("CONFLICT", "同名整合包目录已存在。")),
        Err(error) if error.kind() == io::ErrorKind::NotFound => {}
        Err(error) => return Err(install_error(error)),
    }
    let root = destination.join(".minecraft");
    let versions = root.join("versions");
    let instance_id = format!("local:{}", versions.join(&plan.profile).display());
    if data.tasks.iter().any(|task| {
        task.kind == "installPack"
            && task
                .instance_id
                .as_deref()
                .and_then(|id| id.strip_prefix("local:"))
                .and_then(|path| Path::new(path).parent())
                == Some(versions.as_path())
            && !matches!(task.status.as_str(), "succeeded" | "failed" | "cancelled")
    }) {
        return Err(download_error("BUSY", "目标整合包正在安装。"));
    }

    let id = generated_id("install-pack");
    let timestamp = download_timestamp();
    data.tasks.insert(
        0,
        TaskInfo {
            id: id.clone(),
            kind: "installPack".into(),
            name: format!("安装整合包 {name}"),
            instance_id: Some(instance_id.clone()),
            status: "queued".into(),
            phase: "waiting".into(),
            created_at: timestamp.clone(),
            updated_at: timestamp,
            ..Default::default()
        },
    );
    let task_id = id.clone();
    let worker_destination = destination.clone();
    let worker_plan = plan.clone();
    if let Err(error) = thread::Builder::new()
        .name("modrinth-pack-install".into())
        .spawn(move || {
            let backend = app.state::<Backend>();
            let result = run_pack(&backend, worker_plan, &worker_destination, &task_id);
            let (result, changed) = match result {
                Ok(changed) => (Ok(()), changed),
                Err(error) => (Err(error), Vec::new()),
            };
            finish_install(
                &mut backend.data.lock().unwrap(),
                &task_id,
                instance_id,
                result,
                changed,
            );
        })
    {
        data.tasks.retain(|task| task.id != id);
        return Err(download_error("INTERNAL_ERROR", error));
    }
    plan.submission = Some(PackSubmission {
        destination,
        task_id: id.clone(),
    });
    data.touch(StateResource::Tasks);
    Ok(accepted(id))
}

fn accepted(id: String) -> SavedInfo {
    SavedInfo::changed(
        "整合包安装请求已提交。",
        vec![download_resource("tasks")],
        vec![id],
    )
}

fn run_pack(
    backend: &Backend,
    plan: PackPlan,
    destination: &Path,
    task_id: &str,
) -> Result<Vec<TaskResourceKey>, ApiError> {
    let report = |phase: &str, progress| report_install(backend, task_id, phase, progress);
    report("readingPack", None).map_err(install_error)?;
    let mut pack = Archive::open(&plan.path).map_err(install_error)?;
    if pack.fingerprint != plan.fingerprint {
        return Err(download_error("CONFLICT", "整合包自预览后已发生变化。"));
    }
    let request = VersionInstall::from_pack(&pack.manifest.dependencies)?;
    let staged = tempfile::Builder::new()
        .prefix(".mcugl-pack-")
        .tempdir_in(
            destination
                .parent()
                .expect("install destination has parent"),
        )
        .map_err(install_error)?;
    pack.install_files(staged.path(), &report)
        .map_err(install_error)?;
    request
        .run(
            &minecraft_client()?,
            staged.path(),
            backend,
            &|phase, progress| {
                let phase = if phase == "committing" {
                    "preparingPackGame"
                } else {
                    phase
                };
                report(phase, progress)
            },
        )
        .map_err(install_error)?;
    report("committing", None).map_err(install_error)?;
    publish_pack(backend, staged.path(), destination)
}

fn publish_pack(
    backend: &Backend,
    staged: &Path,
    destination: &Path,
) -> Result<Vec<TaskResourceKey>, ApiError> {
    fs::create_dir(destination).map_err(install_error)?;
    let root = destination.join(".minecraft");
    if let Err(error) = fs::rename(staged, &root) {
        let _ = fs::remove_dir(destination);
        return Err(install_error(error));
    }

    let mut data = backend.data.lock().unwrap();
    let mut config = data.state.config.clone();
    let settings_revision = data.revisions.settings;
    let refresh_active = config.root().is_empty() || Path::new(config.root()) == root;
    if !config.game_roots.iter().any(|path| Path::new(path) == root) {
        config.game_roots.push(root.to_string_lossy().into_owned());
    }
    let saved = (|| {
        let catalog = if refresh_active {
            Some(local_instances::scan(&root).map_err(install_error)?)
        } else {
            None
        };
        backend.save_config(&mut data, config, settings_revision)?;
        let mut changed = vec![TaskResourceKey {
            kind: "settings".into(),
            instance_id: None,
        }];
        if let Some(catalog) = catalog {
            data.replace_instance_catalog(catalog);
            changed.push(TaskResourceKey {
                kind: "instances".into(),
                instance_id: None,
            });
        }
        Ok(changed)
    })();
    if saved.is_err() {
        fs::rename(&root, staged).map_err(install_error)?;
        fs::remove_dir(destination).map_err(install_error)?;
    }
    saved
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn publishes_pack_to_a_new_root_and_refuses_an_existing_destination() {
        let temp = tempfile::tempdir().unwrap();
        let active = temp.path().join("active");
        let config_dir = temp.path().join("config");
        let backend = Backend::open(config_dir.clone(), || Ok(active.clone())).unwrap();
        let destination = temp.path().join("Pack");
        let staged = temp.path().join("staged");
        let profile = staged.join("versions/example");
        fs::create_dir_all(&profile).unwrap();
        fs::write(profile.join("example.json"), r#"{"id":"example"}"#).unwrap();

        publish_pack(&backend, &staged, &destination).unwrap();
        let root = destination.join(".minecraft");
        assert_eq!(local_instances::scan(&root).unwrap().items.len(), 1);
        let reopened = Backend::open(config_dir, || panic!("missing config")).unwrap();
        let data = reopened.data.lock().unwrap();
        assert_eq!(data.state.config.root(), active.to_str().unwrap());
        assert!(
            data.state
                .config
                .game_roots
                .contains(&root.to_str().unwrap().into())
        );
        assert_eq!(
            publish_pack(&backend, &staged, &destination)
                .unwrap_err()
                .code,
            "CONFLICT"
        );
        assert!(root.join("versions/example/example.json").is_file());
    }

    #[test]
    fn restores_staging_when_game_root_cannot_be_saved() {
        let temp = tempfile::tempdir().unwrap();
        let config_dir = temp.path().join("config");
        let active = temp.path().join("active");
        let backend = Backend::open(config_dir.clone(), || Ok(active)).unwrap();
        fs::rename(&config_dir, temp.path().join("config-backup")).unwrap();
        fs::write(&config_dir, b"not a directory").unwrap();
        let staged = temp.path().join("staged");
        fs::create_dir(&staged).unwrap();
        fs::write(staged.join("pack.txt"), b"installed data").unwrap();
        let destination = temp.path().join("Pack");

        assert_eq!(
            publish_pack(&backend, &staged, &destination)
                .unwrap_err()
                .code,
            "IO_ERROR"
        );
        assert!(!destination.exists());
        assert!(staged.join("pack.txt").is_file());
        assert_eq!(
            backend.data.lock().unwrap().state.config.game_roots.len(),
            1
        );
    }
}
