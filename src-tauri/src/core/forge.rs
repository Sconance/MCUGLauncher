//! Forge 官方版本索引与客户端安装。

use std::{collections::HashMap, io, path::Path, time::Duration};

use reqwest::blocking::Client;

use super::loader_install::{self, Installer};

const MAVEN: &str = "https://maven.minecraftforge.net/net/minecraftforge/forge";

pub(crate) fn is_loader_library(name: &str) -> bool {
    name.starts_with("net.minecraftforge:forge:")
        || name.starts_with("net.minecraftforge:fmlloader:")
}

pub(crate) fn supports_game(game: &str) -> bool {
    let fields: Vec<_> = game.split('.').map(str::parse::<u32>).collect();
    match fields.as_slice() {
        [Ok(1), Ok(20), Ok(patch)] => *patch >= 1,
        [Ok(1), Ok(21..), ..] | [Ok(26..), Ok(_), ..] => fields.iter().all(Result::is_ok),
        _ => false,
    }
}

pub(crate) fn profile_id(game: &str, version: &str) -> String {
    format!("{game}-forge-{version}")
}

pub(crate) fn valid_version(version: &str) -> bool {
    let fields: Vec<_> = version.split('.').collect();
    fields.len() == 3 && fields.iter().all(|field| field.parse::<u32>().is_ok())
}

pub(crate) fn list_versions(client: &Client, game: &str) -> io::Result<Vec<String>> {
    if !supports_game(game) {
        return Ok(Vec::new());
    }
    let mut index: HashMap<String, Vec<String>> = client
        .get("https://files.minecraftforge.net/net/minecraftforge/forge/maven-metadata.json")
        .timeout(Duration::from_secs(12))
        .send()
        .and_then(reqwest::blocking::Response::error_for_status)
        .and_then(reqwest::blocking::Response::json)
        .map_err(io::Error::other)?;
    let prefix = format!("{game}-");
    Ok(index
        .remove(game)
        .unwrap_or_default()
        .into_iter()
        .rev()
        .filter_map(|version| {
            version
                .strip_prefix(&prefix)
                .filter(|v| valid_version(v))
                .map(str::to_owned)
        })
        .collect())
}

pub(crate) fn install(
    client: &Client,
    root: &Path,
    game: &str,
    version: &str,
    java: &str,
    managed_java: &Path,
    report: impl FnMut(&str, Option<(u64, u64)>) -> io::Result<()>,
) -> io::Result<()> {
    if !supports_game(game) || !valid_version(version) {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "不支持该 Forge 版本。",
        ));
    }
    let id = profile_id(game, version);
    let coordinate = format!("{game}-{version}");
    let url = format!("{MAVEN}/{coordinate}/forge-{coordinate}-installer.jar");
    loader_install::install(
        client,
        root,
        game,
        java,
        managed_java,
        Installer {
            id: &id,
            url: &url,
            flag: "--installClient",
            identifies: is_loader_library,
        },
        report,
    )
}
