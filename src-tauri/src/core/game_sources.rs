//! 从 Mojang 或 BMCLAPI 请求版本清单。请在后台线程调用。

use reqwest::blocking::Client;
use serde::Deserialize;

const MOJANG_MANIFEST: &str = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
const BMCLAPI_MANIFEST: &str = "https://bmclapi2.bangbang93.com/mc/game/version_manifest_v2.json";
const BMCLAPI_BASE: &str = "https://bmclapi2.bangbang93.com";

#[derive(Clone, Copy, Debug)]
pub enum GameSource {
    Mojang,
    Bmclapi,
}

#[derive(Debug, Deserialize)]
pub struct GameVersion {
    pub id: String,
    #[serde(rename = "type")]
    pub kind: String,
    #[serde(rename = "releaseTime")]
    pub release_time: String,
    pub url: String,
    pub sha1: String,
}

#[derive(Deserialize)]
struct VersionManifest {
    versions: Vec<GameVersion>,
}

impl GameSource {
    /// 获取所选源的版本清单。
    pub fn versions(self, client: &Client) -> Result<Vec<GameVersion>, reqwest::Error> {
        let url = match self {
            Self::Mojang => MOJANG_MANIFEST,
            Self::Bmclapi => BMCLAPI_MANIFEST,
        };
        let mut manifest: VersionManifest = client.get(url).send()?.error_for_status()?.json()?;
        if matches!(self, Self::Bmclapi) {
            for version in &mut manifest.versions {
                if let Some(path) = version
                    .url
                    .strip_prefix("https://piston-meta.mojang.com/")
                    .or_else(|| version.url.strip_prefix("https://launchermeta.mojang.com/"))
                {
                    version.url = format!("{BMCLAPI_BASE}/{path}");
                }
            }
        }
        Ok(manifest.versions)
    }
}

/// 桌面端优先使用 Mojang，连接或解析失败时改用 BMCLAPI。
pub fn available_versions(client: &Client) -> Result<Vec<GameVersion>, reqwest::Error> {
    GameSource::Mojang
        .versions(client)
        .or_else(|_| GameSource::Bmclapi.versions(client))
}
