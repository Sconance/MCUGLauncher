//! 根据版本 ID 准备原版游戏文件。由后台任务调用。

use std::{
    collections::BTreeMap,
    env::consts::{ARCH, OS},
    fs, io,
    path::{Path, PathBuf},
    thread,
};

use reqwest::blocking::Client;
use serde::{Deserialize, de::DeserializeOwned};

use super::{
    file_download::{FileIntegrity, download_file},
    game_sources::{GameVersion, available_versions},
    vanilla_install::download_version_metadata,
};

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct Metadata {
    downloads: ClientDownloads,
    asset_index: AssetIndex,
    libraries: Vec<Library>,
}

#[derive(Deserialize)]
struct ClientDownloads {
    client: Artifact,
}

#[derive(Clone, Deserialize)]
pub(super) struct Artifact {
    url: String,
    sha1: String,
    size: Option<u64>,
    path: Option<String>,
}

#[derive(Deserialize)]
struct AssetIndex {
    id: String,
    #[serde(flatten)]
    artifact: Artifact,
}

#[derive(Deserialize)]
struct Assets {
    objects: BTreeMap<String, Asset>,
}

#[derive(Deserialize)]
struct Asset {
    hash: String,
    size: u64,
}

#[derive(Deserialize)]
pub(super) struct Library {
    #[serde(default)]
    pub(super) name: String,
    pub(super) downloads: LibraryDownloads,
    pub(super) rules: Option<Vec<Rule>>,
    #[serde(default)]
    pub(super) natives: BTreeMap<String, String>,
}

#[derive(Deserialize)]
pub(super) struct LibraryDownloads {
    pub(super) artifact: Option<Artifact>,
    #[serde(default)]
    pub(super) classifiers: BTreeMap<String, Artifact>,
}

#[derive(Deserialize)]
pub(super) struct Rule {
    pub(super) action: String,
    pub(super) os: Option<PlatformRule>,
    #[serde(default)]
    pub(super) features: BTreeMap<String, bool>,
}

#[derive(Deserialize)]
pub(super) struct PlatformRule {
    pub(super) name: Option<String>,
    pub(super) arch: Option<String>,
    pub(super) version: Option<String>,
}

impl Rule {
    fn applies(&self, os: &str, arch: &str) -> bool {
        self.features.values().all(|enabled| !enabled)
            && self.os.as_ref().is_none_or(|platform| {
                platform.name.as_deref().is_none_or(|name| name == os)
                    && platform.arch.as_deref().is_none_or(|name| name == arch)
                    // 下载阶段保留可能需要的库，系统版本条件由启动阶段判定。
                    && (platform.version.is_none() || self.action == "allow")
            })
    }
}

pub(crate) fn safe_component(value: &str) -> bool {
    !value.is_empty()
        && value != "."
        && value != ".."
        && !value.ends_with(['.', ' '])
        && !value.chars().any(|ch| ch.is_control() || "/\\:<>*?\"|".contains(ch))
        && !matches!(
            value.split('.').next().unwrap_or_default().to_ascii_uppercase().as_str(),
            "CON" | "PRN" | "AUX" | "NUL" | "COM1" | "COM2" | "COM3" | "COM4"
                | "COM5" | "COM6" | "COM7" | "COM8" | "COM9" | "LPT1" | "LPT2"
                | "LPT3" | "LPT4" | "LPT5" | "LPT6" | "LPT7" | "LPT8" | "LPT9"
        )
}

fn invalid(message: impl Into<String>) -> io::Error {
    io::Error::new(io::ErrorKind::InvalidData, message.into())
}

fn read_json<T: DeserializeOwned>(path: &Path) -> io::Result<T> {
    serde_json::from_reader(fs::File::open(path)?)
        .map_err(|error| invalid(format!("无法解析 {}：{error}", path.display())))
}

fn fetch(client: &Client, artifact: &Artifact, path: &Path) -> io::Result<()> {
    let integrity = FileIntegrity {
        size: artifact.size,
        sha1: &artifact.sha1,
    };
    download_file(client, &artifact.url, path, &integrity)
        .map_err(|error| io::Error::new(error.kind(), format!("{}：{error}", path.display())))
}

/// 仅在所有文件校验完成后公开实例目录。回调可以暂停任务或请求取消。
pub fn download_vanilla(
    client: &Client,
    root: &Path,
    version_id: &str,
    mut report: impl FnMut(&str, Option<(u64, u64)>) -> io::Result<()>,
) -> io::Result<()> {
    if !safe_component(version_id) {
        return Err(io::Error::new(io::ErrorKind::InvalidInput, "无效的版本 ID。"));
    }
    report("manifest", None)?;
    let version = available_versions(client)
        .map_err(io::Error::other)?
        .into_iter()
        .find(|version| version.id == version_id)
        .ok_or_else(|| io::Error::new(io::ErrorKind::NotFound, "版本清单中没有该版本。"))?;
    install_version(client, root, &version, report)
}

fn install_version(
    client: &Client,
    root: &Path,
    version: &GameVersion,
    mut report: impl FnMut(&str, Option<(u64, u64)>) -> io::Result<()>,
) -> io::Result<()> {
    let version_id = &version.id;
    let versions_dir = root.join("versions");
    fs::create_dir_all(&versions_dir)?;
    let destination = versions_dir.join(version_id);
    let staging = tempfile::Builder::new().prefix(".mcug-install-").tempdir_in(&versions_dir)?;
    let metadata_path = staging.path().join(format!("{version_id}.json"));
    report("metadata", None)?;
    let metadata: Metadata = serde_json::from_value(download_version_metadata(
        client, version, &metadata_path,
    )?)
    .map_err(|error| invalid(format!("版本 JSON 缺少原版安装所需信息：{error}")))?;

    let asset_id = &metadata.asset_index.id;
    if !safe_component(asset_id) {
        return Err(invalid("无效的资源索引 ID。"));
    }
    let index_path = root.join("assets/indexes").join(format!("{asset_id}.json"));
    report("assetIndex", None)?;
    fetch(client, &metadata.asset_index.artifact, &index_path)?;
    let assets: Assets = read_json(&index_path)?;

    let mut artifacts = BTreeMap::from([(
        staging.path().join(format!("{version_id}.jar")),
        metadata.downloads.client,
    )]);
    let os = if OS == "macos" { "osx" } else { OS };
    let arch = if ARCH == "aarch64" { "arm64" } else { ARCH };
    for library in metadata.libraries {
        if library.rules.as_ref().is_some_and(|rules| {
            !rules.iter().rev().find(|rule| rule.applies(os, arch))
                .is_some_and(|rule| rule.action == "allow")
        }) {
            continue;
        }
        if let Some(artifact) = library.downloads.artifact {
            artifacts.entry(library_path(&artifact, root)?).or_insert(artifact);
        }
        if let Some(classifier) = library.natives.get(os) {
            let classifier = classifier.replace("${arch}", &usize::BITS.to_string());
            let artifact = library.downloads.classifiers.get(&classifier)
                .ok_or_else(|| invalid(format!("找不到原生库 {classifier}。")))?;
            artifacts.entry(library_path(artifact, root)?).or_insert_with(|| artifact.clone());
        }
    }
    for asset in assets.objects.into_values() {
        if asset.hash.len() != 40 || !asset.hash.bytes().all(|byte| byte.is_ascii_hexdigit()) {
            return Err(invalid("无效的资源文件 SHA-1。"));
        }
        let relative = format!("{}/{}", &asset.hash[..2], asset.hash);
        artifacts.entry(root.join("assets/objects").join(&relative)).or_insert(Artifact {
            url: format!("https://resources.download.minecraft.net/{relative}"),
            sha1: asset.hash,
            size: Some(asset.size),
            path: None,
        });
    }

    const CONCURRENT_FILES: usize = 8;
    let artifacts: Vec<_> = artifacts.into_iter().collect();
    let total = artifacts.len() as u64;
    for (batch, artifacts) in artifacts.chunks(CONCURRENT_FILES).enumerate() {
        report("downloading", Some(((batch * CONCURRENT_FILES) as u64, total)))?;
        thread::scope(|scope| {
            let transfers: Vec<_> = artifacts.iter()
                .map(|(path, artifact)| scope.spawn(move || fetch(client, artifact, path)))
                .collect();
            let mut result = Ok(());
            for transfer in transfers {
                let finished = transfer.join().unwrap_or_else(|_| Err(io::Error::other("下载线程异常退出。")));
                result = result.and(finished);
            }
            result
        })?;
    }
    report("committing", Some((total, total)))?;
    match fs::symlink_metadata(&destination) {
        Ok(_) => return Err(io::Error::new(io::ErrorKind::AlreadyExists, "目标版本目录已存在。")),
        Err(error) if error.kind() == io::ErrorKind::NotFound => {}
        Err(error) => return Err(error),
    }
    fs::rename(staging.path(), &destination)
}

pub(super) fn library_path(artifact: &Artifact, root: &Path) -> io::Result<PathBuf> {
    let relative = artifact.path.as_deref().filter(|path| path.split('/').all(safe_component))
        .ok_or_else(|| invalid("版本 JSON 中的库文件路径无效。"))?;
    Ok(root.join("libraries").join(relative))
}
