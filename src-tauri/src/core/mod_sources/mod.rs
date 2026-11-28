//! 模组来源的查询接口。

pub mod curseforge;
pub mod mcmod;
pub mod modrinth;
pub(crate) mod modrinth_search;

use std::{io, time::Duration};

use reqwest::{Client, Url};

const USER_AGENT: &str = concat!(
    "Sconance/MCUGLauncher/",
    env!("CARGO_PKG_VERSION"),
    " (https://github.com/Sconance/MCUGLauncher)"
);

pub(crate) fn client() -> Result<Client, reqwest::Error> {
    Client::builder()
        .timeout(Duration::from_secs(15))
        .user_agent(USER_AGENT)
        .redirect(reqwest::redirect::Policy::none())
        .build()
}

pub(crate) fn download_client() -> Result<reqwest::blocking::Client, reqwest::Error> {
    reqwest::blocking::Client::builder()
        .timeout(Duration::from_secs(90))
        .user_agent(USER_AGENT)
        .redirect(reqwest::redirect::Policy::none())
        .build()
}

#[derive(Clone)]
pub(crate) struct ModFile {
    pub filename: String,
    pub url: Url,
    pub size: u64,
    pub sha1: String,
}

#[derive(Clone)]
pub(crate) struct ModVersion {
    pub id: String,
    pub name: String,
    pub file: ModFile,
    pub game_versions: Vec<String>,
    pub loaders: Vec<String>,
    pub version_type: String,
}

/// 优先查询 Modrinth，百科提供 CurseForge 且已配置密钥时可回退。
pub(crate) async fn versions(
    client: &Client,
    sources: mcmod::Sources,
    game: &str,
    loader: &str,
) -> io::Result<Vec<ModVersion>> {
    let modrinth_error = if let Some(slug) = sources.modrinth.as_deref() {
        match modrinth::versions(client, slug, game, loader).await {
            Ok(versions) if !versions.is_empty() => return Ok(versions),
            Ok(_) => None,
            Err(error) => Some(error.without_url()),
        }
    } else {
        None
    };
    if let Some(slug) = sources.curseforge.as_deref() {
        let key = std::env::var("MCUG_CURSEFORGE_API_KEY")
            .ok()
            .and_then(|key| reqwest::header::HeaderValue::from_str(&key).ok())
            .filter(|key| !key.is_empty());
        if let Some(mut key) = key {
            key.set_sensitive(true);
            return curseforge::versions(client, key, slug, game, loader)
                .await
                .map_err(|error| io::Error::other(error.without_url()));
        }
        if sources.modrinth.is_none() {
            return Err(io::Error::new(
                io::ErrorKind::PermissionDenied,
                "该模组使用 CurseForge，请配置 MCUG_CURSEFORGE_API_KEY。",
            ));
        }
    }
    modrinth_error.map_or_else(|| Ok(Vec::new()), |error| Err(io::Error::other(error)))
}

fn valid_file(filename: &str, sha1: &str) -> bool {
    super::vanilla_download::safe_component(filename)
        && filename.to_ascii_lowercase().ends_with(".jar")
        && sha1.len() == 40
        && sha1.bytes().all(|byte| byte.is_ascii_hexdigit())
}
