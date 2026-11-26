//! NeoForge 版本发现与官方无界面客户端安装。

use std::{io, path::Path, time::Duration};

use reqwest::blocking::Client;
use serde::Deserialize;

use super::{
    loader_install::{self, Installer},
    vanilla_download::safe_component,
};

const ORIGIN: &str = "https://maven.neoforged.net";

#[derive(Deserialize)]
struct MavenVersions {
    versions: Vec<String>,
}

/// Resolve modern NeoForge's Maven version convention to the corresponding game ID.
fn game_version(loader_version: &str) -> Option<String> {
    if !loader_version
        .bytes()
        .all(|byte| byte.is_ascii_alphanumeric() || b".-+".contains(&byte))
    {
        return None;
    }
    let (version, snapshot) = loader_version
        .split_once('+')
        .map_or((loader_version, None), |(v, s)| (v, Some(s)));
    let numbers = version.split('-').next()?;
    let components: Vec<_> = numbers.split('.').collect();
    let unsigned = |value: &str| value.parse::<u32>().ok();
    match components.as_slice() {
        ["0", special, build] if unsigned(build).is_some() && !special.is_empty() => {
            Some((*special).to_owned())
        }
        [major, minor, patch] | [major, minor, patch, _] => {
            let (major, minor, patch) = (unsigned(major)?, unsigned(minor)?, unsigned(patch)?);
            let mut game = match (major, minor, patch) {
                (20..=23, 0, _) if major > 20 => format!("1.{major}"),
                (20, 0..=1, _) => return None,
                (20..=23, _, _) => format!("1.{major}.{minor}"),
                (26.., _, 0) => format!("{major}.{minor}"),
                (26.., _, _) => format!("{major}.{minor}.{patch}"),
                _ => return None,
            };
            if let Some(snapshot) = snapshot {
                game.push('-');
                game.push_str(snapshot);
            }
            Some(game)
        }
        _ => None,
    }
}

pub(crate) fn compatible(minecraft: &str, loader_version: &str) -> bool {
    game_version(loader_version).as_deref() == Some(minecraft)
}

pub(crate) fn list_versions(client: &Client, minecraft: &str) -> io::Result<Vec<String>> {
    // NeoForge for 1.20.1 is a separate legacy Forge artifact.
    if matches!(minecraft, "1.20" | "1.20.1") {
        return Ok(Vec::new());
    }
    let list: MavenVersions = client
        .get(format!(
            "{ORIGIN}/api/maven/versions/releases/net/neoforged/neoforge"
        ))
        .timeout(Duration::from_secs(12))
        .send()
        .and_then(reqwest::blocking::Response::error_for_status)
        .and_then(reqwest::blocking::Response::json)
        .map_err(io::Error::other)?;
    Ok(list
        .versions
        .into_iter()
        .rev()
        .filter(|version| compatible(minecraft, version))
        .collect())
}

pub(crate) fn profile_id(loader_version: &str) -> String {
    format!("neoforge-{loader_version}")
}

pub(crate) fn is_loader_library(name: &str) -> bool {
    name.starts_with("net.neoforged:neoforge:")
        || name.starts_with("net.neoforged.fancymodloader:loader:")
}

pub(crate) fn install(
    client: &Client,
    root: &Path,
    minecraft: &str,
    loader_version: &str,
    configured_java: &str,
    managed_java: &Path,
    report: impl FnMut(&str, Option<(u64, u64)>) -> io::Result<()>,
) -> io::Result<()> {
    if !safe_component(minecraft) || !compatible(minecraft, loader_version) {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "所选 NeoForge 版本与 Minecraft 版本不兼容。",
        ));
    }

    let id = profile_id(loader_version);
    let url =
        format!("{ORIGIN}/releases/net/neoforged/neoforge/{loader_version}/{id}-installer.jar");
    loader_install::install(
        client,
        root,
        minecraft,
        configured_java,
        managed_java,
        Installer {
            id: &id,
            url: &url,
            flag: "--install-client",
            identifies: is_loader_library,
        },
        report,
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn maps_maven_versions_to_minecraft_release_ids() {
        for (loader, game) in [
            ("20.4.251", "1.20.4"),
            ("21.0.167", "1.21"),
            ("21.1.267", "1.21.1"),
            ("26.3.0.118", "26.3"),
            ("26.1.2.130", "26.1.2"),
            ("26.1.0.0-alpha.1+snapshot-1", "26.1-snapshot-1"),
            ("0.25w14craftmine.3-beta", "25w14craftmine"),
        ] {
            assert!(compatible(game, loader), "{loader} should match {game}");
        }
        assert!(!compatible("1.21.10", "21.1.267"));
        assert!(!compatible("1.20.1", "1.20.1-47.1.99"));
        assert!(!compatible("1.21.1", "../21.1.267"));
    }
}
