//! 从 Modrinth 的公开搜索索引读取模组列表摘要。

use reqwest::{Client, Url};
use serde::Deserialize;

use super::modrinth::PROJECT_PREFIX;
use crate::model::ContentItem;

const MOD_LOADERS: &[&str] = &[
    "babric",
    "bta-babric",
    "cleanroom",
    "fabric",
    "forge",
    "legacy-fabric",
    "liteloader",
    "modloader",
    "neoforge",
    "nilloader",
    "ornithe",
    "quilt",
    "rift",
];

#[derive(Deserialize)]
struct SearchResponse {
    hits: Vec<Project>,
}

#[derive(Deserialize)]
struct Project {
    project_id: String,
    title: String,
    description: String,
    author: Option<String>,
    icon_url: Option<String>,
    downloads: u64,
    categories: Vec<String>,
    #[serde(default)]
    display_categories: Vec<String>,
    versions: Vec<String>,
}

impl From<Project> for ContentItem {
    fn from(project: Project) -> Self {
        let loaders = project
            .categories
            .iter()
            .filter(|category| MOD_LOADERS.contains(&category.as_str()))
            .cloned()
            .collect();
        Self {
            id: format!("{PROJECT_PREFIX}{}", project.project_id),
            category: "mod".into(),
            name: project.title,
            description: project.description,
            author: project.author.unwrap_or_default(),
            icon: project
                .icon_url
                .filter(|url| url.starts_with("https://"))
                .unwrap_or_default(),
            downloads: project.downloads.to_string(),
            tags: if project.display_categories.is_empty() {
                project.categories
            } else {
                project.display_categories
            },
            game_versions: project.versions,
            loaders,
        }
    }
}

fn search_url(query: &str, game: &str, loader: &str) -> Url {
    let mut facets = vec![vec!["project_type:mod".to_owned()]];
    if !game.is_empty() {
        facets.push(vec![format!("versions:{game}")]);
    }
    let loader = loader.to_ascii_lowercase();
    // Unmodded instances can still browse mods for their Minecraft version.
    if !loader.is_empty() && loader != "vanilla" {
        facets.push(vec![format!("categories:{loader}")]);
    }
    let mut url = Url::parse("https://api.modrinth.com/v2/search").expect("fixed Modrinth URL");
    url.query_pairs_mut()
        .append_pair("query", query)
        .append_pair("facets", &serde_json::json!(facets).to_string())
        .append_pair("limit", "100");
    url
}

pub(crate) async fn search(
    client: &Client,
    query: &str,
    game: &str,
    loader: &str,
) -> Result<Vec<ContentItem>, reqwest::Error> {
    let response = client
        .get(search_url(query, game, loader))
        .send()
        .await?
        .error_for_status()?
        .json::<SearchResponse>()
        .await?;
    Ok(response.hits.into_iter().map(ContentItem::from).collect())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn search_filters_mods_without_treating_vanilla_as_a_loader() {
        for (loader, expected) in [
            (
                "Vanilla",
                serde_json::json!([["project_type:mod"], ["versions:1.21.1"]]),
            ),
            (
                "Fabric",
                serde_json::json!([
                    ["project_type:mod"],
                    ["versions:1.21.1"],
                    ["categories:fabric"]
                ]),
            ),
        ] {
            let url = search_url("A & B", "1.21.1", loader);
            let params: std::collections::HashMap<_, _> = url.query_pairs().collect();
            assert_eq!(params["query"], "A & B");
            assert_eq!(
                serde_json::from_str::<serde_json::Value>(&params["facets"]).unwrap(),
                expected
            );
        }
    }
}
