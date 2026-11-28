//! 根据 Modrinth 项目 ID 或 slug 查询版本和下载文件。

use reqwest::{Client, Url};
use serde::Deserialize;

use super::{ModFile, ModVersion, valid_file};

pub(crate) const PROJECT_PREFIX: &str = "modrinth:";

#[derive(Deserialize)]
struct Version {
    id: String,
    name: String,
    #[serde(default)]
    game_versions: Vec<String>,
    #[serde(default)]
    loaders: Vec<String>,
    #[serde(default)]
    version_type: Option<String>,
    files: Vec<VersionFile>,
}

#[derive(Deserialize)]
struct VersionFile {
    filename: String,
    url: String,
    size: u64,
    hashes: Hashes,
    primary: bool,
}

#[derive(Deserialize)]
struct Hashes {
    sha1: String,
}

pub(crate) async fn versions(
    client: &Client,
    slug: &str,
    game: &str,
    loader: &str,
) -> Result<Vec<ModVersion>, reqwest::Error> {
    let mut url = Url::parse("https://api.modrinth.com/v2/project/").expect("fixed Modrinth URL");
    url.path_segments_mut()
        .expect("hierarchical URL")
        .pop_if_empty()
        .extend([slug, "version"]);
    if !game.is_empty() {
        url.query_pairs_mut()
            .append_pair("game_versions", &serde_json::json!([game]).to_string());
    }
    if !loader.is_empty() {
        url.query_pairs_mut()
            .append_pair("loaders", &serde_json::json!([loader]).to_string());
    }
    let versions = client
        .get(url)
        .send()
        .await?
        .error_for_status()?
        .json::<Vec<Version>>()
        .await?;
    Ok(versions
        .into_iter()
        .filter_map(|version| {
            let file = version
                .files
                .into_iter()
                .filter_map(|file| {
                    let url = Url::parse(&file.url).ok()?;
                    if url.scheme() != "https"
                        || !matches!(url.host_str(), Some("cdn.modrinth.com"))
                        || !valid_file(&file.filename, &file.hashes.sha1)
                    {
                        return None;
                    }
                    Some((
                        file.primary,
                        ModFile {
                            filename: file.filename,
                            url,
                            size: file.size,
                            sha1: file.hashes.sha1,
                        },
                    ))
                })
                .max_by_key(|(primary, _)| *primary)?
                .1;
            Some(ModVersion {
                id: format!("{PROJECT_PREFIX}{}", version.id),
                name: version.name,
                file,
                game_versions: version.game_versions,
                loaders: version.loaders,
                version_type: version
                    .version_type
                    .filter(|value| !value.is_empty())
                    .unwrap_or_else(|| "release".into()),
            })
        })
        .collect())
}
