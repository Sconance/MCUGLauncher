//! 通过 MC 百科提供的 CurseForge 项目标识查询发布者开放分发的版本。

use reqwest::{Client, Url, header::HeaderValue};
use serde::Deserialize;

use super::{ModFile, ModVersion, valid_file};

const API: &str = "https://api.curseforge.com/v1";

#[derive(Deserialize)]
struct Response<T> {
    data: T,
}

#[derive(Deserialize)]
struct Project {
    id: u32,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct File {
    id: u32,
    display_name: String,
    file_name: String,
    file_length: u64,
    download_url: Option<String>,
    #[serde(default)]
    game_versions: Vec<String>,
    #[serde(default)]
    release_type: Option<u8>,
    hashes: Vec<FileHash>,
}

#[derive(Deserialize)]
struct FileHash {
    value: String,
    algo: u8,
}

pub(super) async fn versions(
    client: &Client,
    key: HeaderValue,
    slug: &str,
    game: &str,
    loader: &str,
) -> Result<Vec<ModVersion>, reqwest::Error> {
    let mut search = Url::parse(&format!("{API}/mods/search")).expect("fixed CurseForge URL");
    search
        .query_pairs_mut()
        .append_pair("gameId", "432")
        .append_pair("classId", "6")
        .append_pair("slug", slug);
    let projects = client
        .get(search)
        .header("x-api-key", key.clone())
        .send()
        .await?
        .error_for_status()?
        .json::<Response<Vec<Project>>>()
        .await?;
    let Some(project) = projects.data.first() else {
        return Ok(Vec::new());
    };
    let mut url =
        Url::parse(&format!("{API}/mods/{}/files", project.id)).expect("numeric project ID");
    url.query_pairs_mut().append_pair("pageSize", "50");
    if !game.is_empty() {
        url.query_pairs_mut().append_pair("gameVersion", game);
    }
    let loader_id = match loader {
        "forge" => Some("1"),
        "liteloader" => Some("2"),
        "fabric" => Some("4"),
        "quilt" => Some("5"),
        "neoforge" => Some("6"),
        _ => None,
    };
    if let Some(loader_id) = loader_id {
        url.query_pairs_mut()
            .append_pair("modLoaderType", loader_id);
    } else if !loader.is_empty() {
        return Ok(Vec::new());
    }
    let files = client
        .get(url)
        .header("x-api-key", key)
        .send()
        .await?
        .error_for_status()?
        .json::<Response<Vec<File>>>()
        .await?;
    Ok(files
        .data
        .into_iter()
        .filter_map(|file| {
            let url = Url::parse(&file.download_url?).ok()?;
            let sha1 = file.hashes.into_iter().find(|hash| hash.algo == 1)?.value;
            if url.scheme() != "https"
                || !matches!(
                    url.host_str(),
                    Some("edge.forgecdn.net" | "mediafilez.forgecdn.net")
                )
                || !valid_file(&file.file_name, &sha1)
            {
                return None;
            }
            let mut parsed_games = Vec::new();
            let mut parsed_loaders = Vec::new();
            for entry in file.game_versions {
                let lower = entry.trim().to_ascii_lowercase();
                if matches!(
                    lower.as_str(),
                    "fabric" | "forge" | "neoforge" | "quilt" | "liteloader" | "rift"
                ) {
                    parsed_loaders.push(lower);
                } else if entry.chars().next().is_some_and(|ch| ch.is_ascii_digit()) {
                    parsed_games.push(entry);
                }
            }
            let version_type = match file.release_type {
                Some(2) => "beta",
                Some(3) => "alpha",
                _ => "release",
            };
            Some(ModVersion {
                id: format!("curseforge:{}", file.id),
                name: file.display_name,
                file: ModFile {
                    filename: file.file_name,
                    url,
                    size: file.file_length,
                    sha1,
                },
                game_versions: parsed_games,
                loaders: parsed_loaders,
                version_type: version_type.into(),
            })
        })
        .collect())
}
