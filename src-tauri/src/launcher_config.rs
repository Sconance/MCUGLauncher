use std::{
    fs,
    io::{self, Write},
    path::{Path, PathBuf},
};

use serde::{Deserialize, Serialize};
use tempfile::NamedTempFile;

use crate::model::DataSourceSet;

pub(crate) const FILE_NAME: &str = "mcuglauncher.conf.json";

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub(crate) struct LauncherConfig {
    #[serde(default, rename = "root", skip_serializing)]
    legacy_root: Option<String>,
    #[serde(default)]
    pub game_roots: Vec<String>,
    pub java: String,
    pub width: i32,
    pub height: i32,
    pub ram: i32,
    pub auto_memory: bool,
    pub auto_chinese: bool,
    pub anonymous: bool,
    pub custom_data_sources: DataSourceSet,
    pub data_source_mode: String,
    /// 选中的第三方前端 identifier；`None` 表示使用官方前端。
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub frontend: Option<String>,
}

impl Default for LauncherConfig {
    fn default() -> Self {
        Self {
            legacy_root: None,
            game_roots: Vec::new(),
            java: String::new(),
            width: 1280,
            height: 720,
            ram: 8192,
            auto_memory: true,
            auto_chinese: true,
            anonymous: false,
            custom_data_sources: DataSourceSet::default(),
            data_source_mode: "default".into(),
            frontend: None,
        }
    }
}

impl LauncherConfig {
    pub(crate) fn root(&self) -> &str {
        self.game_roots.first().map_or("", String::as_str)
    }

    pub(crate) fn select_root(&mut self, root: String) {
        self.game_roots.retain(|entry| Path::new(entry) != Path::new(&root));
        self.game_roots.insert(0, root);
    }

    pub(crate) fn remove_root(&mut self, root: &str) {
        self.game_roots.retain(|entry| entry != root);
    }

    pub(crate) fn load_or_create(
        directory: &Path,
        default_game_root: impl FnOnce() -> io::Result<PathBuf>,
    ) -> io::Result<Self> {
        let path = directory.join(FILE_NAME);
        match Self::read(&path) {
            Ok(mut config) => {
                if let Some(root) = config.legacy_root.take() {
                    if !root.is_empty() {
                        config.select_root(root);
                    }
                    config.save(directory)?;
                }
                return Ok(config);
            }
            Err(error) if error.kind() == io::ErrorKind::NotFound => {}
            Err(error) => return Err(error),
        }

        let root = default_game_root()?.to_string_lossy().into_owned();
        let mut config = Self::default();
        config.select_root(root);
        fs::create_dir_all(directory)?;
        match config.stage(directory)?.persist_noclobber(&path) {
            Ok(_) => Ok(config),
            Err(error) if error.error.kind() == io::ErrorKind::AlreadyExists => Self::read(&path),
            Err(error) => Err(error.error),
        }
    }

    pub(crate) fn save(&self, directory: &Path) -> io::Result<()> {
        self.stage(directory)?
            .persist(directory.join(FILE_NAME))
            .map_err(|error| error.error)?;
        Ok(())
    }

    fn read(path: &Path) -> io::Result<Self> {
        let contents = fs::read(path)?;
        serde_json::from_slice(&contents).map_err(|error| {
            io::Error::new(
                io::ErrorKind::InvalidData,
                format!("{}: {error}", path.display()),
            )
        })
    }

    fn stage(&self, directory: &Path) -> io::Result<NamedTempFile> {
        let mut file = NamedTempFile::new_in(directory)?;
        serde_json::to_writer_pretty(file.as_file_mut(), self)?;
        file.write_all(b"\n")?;
        file.as_file().sync_all()?;
        Ok(file)
    }
}
