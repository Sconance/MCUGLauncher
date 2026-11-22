use std::{
    io,
    path::{Path, PathBuf},
    sync::Mutex,
};

use crate::{
    demo::{Data, StateResource},
    launcher_config::LauncherConfig,
    local_instances,
    model::ApiError,
};

/// 后端运行时状态和启动器设置的保存位置。
pub struct Backend {
    pub data: Mutex<Data>,
    config_dir: PathBuf,
}

impl Backend {
    pub(crate) fn open(
        config_dir: PathBuf,
        default_game_root: impl FnOnce() -> io::Result<PathBuf>,
    ) -> io::Result<Self> {
        let config = LauncherConfig::load_or_create(&config_dir, default_game_root)?;
        let mut data = Data::demo();
        let catalog = local_instances::scan(Path::new(config.root())).unwrap_or_else(|error| {
            eprintln!("[mcugl] 无法读取游戏目录 {}：{error}", config.root());
            local_instances::empty_catalog()
        });
        data.state.config = config;
        data.replace_instance_catalog(catalog);
        Ok(Self {
            data: Mutex::new(data),
            config_dir,
        })
    }

    pub(crate) fn config_dir(&self) -> &Path {
        &self.config_dir
    }

    pub(crate) fn save_config(
        &self,
        data: &mut Data,
        config: LauncherConfig,
        expected_revision: u64,
    ) -> Result<(), ApiError> {
        data.ensure_settings_revision(expected_revision)?;
        config.save(&self.config_dir).map_err(|error| ApiError {
            code: "IO_ERROR".into(),
            message: format!("无法保存启动器配置（{}）：{error}", self.config_dir.display()),
            retryable: true,
            field_errors: Default::default(),
        })?;
        data.state.config = config;
        data.touch(StateResource::Settings);
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::launcher_config::FILE_NAME;
    use std::fs;

    #[test]
    fn migrates_legacy_root_to_the_first_list_entry() {
        let temp = tempfile::tempdir().unwrap();
        let config_path = temp.path().join(FILE_NAME);
        let mut legacy = serde_json::to_value(LauncherConfig::default()).unwrap();
        legacy["root"] = serde_json::json!("/game/active");
        legacy["gameRoots"] = serde_json::json!(["/game/other", "/game/active"]);
        fs::write(&config_path, serde_json::to_vec(&legacy).unwrap()).unwrap();

        let backend = Backend::open(temp.path().to_path_buf(), || panic!("existing config")).unwrap();
        let data = backend.data.lock().unwrap();
        assert_eq!(data.state.config.root(), "/game/active");
        let config: serde_json::Value = serde_json::from_slice(&fs::read(config_path).unwrap()).unwrap();
        assert_eq!(config["gameRoots"], serde_json::json!(["/game/active", "/game/other"]));
        assert!(config.get("root").is_none());
    }

    #[test]
    fn creates_config_and_restores_saved_settings_on_restart() {
        let temp = tempfile::tempdir().unwrap();
        let directory = temp.path().join("app-data");
        let root = temp.path().join(".minecraft");
        let new_root = temp.path().join("other-game-dir");
        let backend = Backend::open(directory.clone(), || Ok(root.clone())).unwrap();
        assert!(directory.join(FILE_NAME).is_file());
        assert!(!root.exists());

        {
            let mut data = backend.data.lock().unwrap();
            assert_eq!(data.state.config.game_roots, vec![root.to_str().unwrap()]);
            let revision = data.revisions.settings;
            let mut config = data.state.config.clone();
            config.select_root(new_root.to_str().unwrap().into());
            config.ram = 4096;
            backend.save_config(&mut data, config, revision).unwrap();
            assert_eq!(data.revisions.settings, revision + 1);
        }
        drop(backend);

        let backend = Backend::open(directory, || panic!("saved config should take precedence"))
            .unwrap();
        let data = backend.data.lock().unwrap();
        assert_eq!(data.state.config.ram, 4096);
        assert_eq!(data.state.config.root(), new_root.to_str().unwrap());
        assert_eq!(data.state.config.game_roots.len(), 2);
    }

    #[test]
    fn malformed_config_is_preserved() {
        let directory = tempfile::tempdir().unwrap();
        let path = directory.path().join(FILE_NAME);
        fs::write(&path, b"{ malformed").unwrap();

        let error = Backend::open(directory.path().to_path_buf(), || {
            panic!("malformed config must not be treated as missing")
        })
        .err()
        .unwrap();
        assert_eq!(error.kind(), io::ErrorKind::InvalidData);
        assert_eq!(fs::read(&path).unwrap(), b"{ malformed");
    }

    #[test]
    fn save_failure_leaves_settings_and_revision_unchanged() {
        let temp = tempfile::tempdir().unwrap();
        let directory = temp.path().join("app-data");
        let backend = Backend::open(directory.clone(), || Ok(temp.path().join("game"))).unwrap();
        fs::rename(&directory, temp.path().join("previous")).unwrap();
        fs::write(&directory, b"not a directory").unwrap();

        let mut data = backend.data.lock().unwrap();
        let revision = data.revisions.settings;
        let mut config = data.state.config.clone();
        config.ram = 4096;
        let error = backend.save_config(&mut data, config, revision).unwrap_err();
        assert_eq!(error.code, "IO_ERROR");
        assert_eq!(data.state.config.ram, LauncherConfig::default().ram);
        assert_eq!(data.revisions.settings, revision);
    }

    #[test]
    fn stale_revision_cannot_replace_saved_settings() {
        let directory = tempfile::tempdir().unwrap();
        let path = directory.path().join(FILE_NAME);
        let backend = Backend::open(directory.path().to_path_buf(), || {
            Ok(directory.path().join("game"))
        })
        .unwrap();
        let saved = fs::read(&path).unwrap();
        let mut data = backend.data.lock().unwrap();
        let stale_revision = data.revisions.settings;
        data.touch(StateResource::Settings);
        let mut config = data.state.config.clone();
        config.data_source_mode = "custom".into();

        let error = backend.save_config(&mut data, config, stale_revision).unwrap_err();
        assert_eq!(error.code, "CONFLICT");
        assert_eq!(data.state.config.data_source_mode, "default");
        assert_eq!(fs::read(&path).unwrap(), saved);
    }
}
