//! MC 百科目录与词条来源。只访问公开目录和词条，不打开站外跳转页。

use std::{
    collections::HashSet,
    io,
    time::{Duration, Instant},
};

use base64::{Engine, engine::general_purpose};
use dom_query::Document;
use reqwest::{Client, Url};

use crate::model::ContentItem;

const ORIGIN: &str = "https://www.mcmod.cn";
pub(crate) const PROJECT_PREFIX: &str = "mcmod:";
const MOD_CARD: &str = ".modlist-list-frame > .modlist-block";

#[derive(Default)]
pub(crate) struct Sources {
    pub modrinth: Option<String>,
    pub curseforge: Option<String>,
}

fn origin() -> Url {
    Url::parse(ORIGIN).expect("fixed MCMod origin")
}

fn network_error(error: reqwest::Error) -> io::Error {
    io::Error::other(error.without_url())
}

async fn fetch_page(client: &Client, url: Url) -> io::Result<String> {
    let response = client.get(url).send().await.map_err(network_error)?;
    if !response.status().is_success() {
        return Err(io::Error::other(format!(
            "MC 百科返回 HTTP {}",
            response.status()
        )));
    }
    response.text().await.map_err(network_error)
}

const CATALOG_TTL: Duration = Duration::from_secs(300);
const FAILURE_COOLDOWN: Duration = Duration::from_secs(60);

#[derive(Default)]
pub(crate) struct CatalogCache {
    page: Option<(Instant, Result<Vec<ContentItem>, String>)>,
}

impl CatalogCache {
    pub(crate) async fn search(&mut self, query: &str) -> Result<Vec<ContentItem>, String> {
        let page = match &self.page {
            Some((expires, result)) if Instant::now() < *expires => result,
            _ => {
                let result = fetch_catalog().await.map_err(|error| error.to_string());
                let ttl = if result.is_ok() {
                    CATALOG_TTL
                } else {
                    FAILURE_COOLDOWN
                };
                let (_, result) = self.page.insert((Instant::now() + ttl, result));
                result
            }
        };
        page.as_ref()
            .map(|items| filter_items(items, query))
            .map_err(Clone::clone)
    }
}

async fn fetch_catalog() -> io::Result<Vec<ContentItem>> {
    let client = super::client().map_err(network_error)?;
    let mut url = origin();
    url.set_path("/modlist.html");
    url.query_pairs_mut()
        .append_pair("platform", "1")
        .append_pair("sort", "createtime");
    parse_catalog(&fetch_page(&client, url).await?)
}

fn filter_items(items: &[ContentItem], query: &str) -> Vec<ContentItem> {
    let query = query.to_lowercase();
    items
        .iter()
        .filter(|item| {
            item.name.to_lowercase().contains(&query)
                || item.description.to_lowercase().contains(&query)
        })
        .cloned()
        .collect()
}

fn project_id(href: &str) -> Option<u32> {
    let url = origin().join(href).ok()?;
    if url.origin() != origin().origin() {
        return None;
    }
    let id = url
        .path()
        .strip_prefix("/class/")?
        .strip_suffix(".html")?
        .parse()
        .ok()?;
    (id != 0).then_some(id)
}

fn parse_catalog(html: &str) -> io::Result<Vec<ContentItem>> {
    let doc = Document::from(html);
    if doc.select(".modlist-list-frame").is_empty() {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            "MC 百科没有返回模组目录。",
        ));
    }
    let mut seen = HashSet::new();
    let mut items = Vec::new();
    for card in doc.select(MOD_CARD).iter() {
        let mut linked_text = Vec::new();
        let mut id = None;
        for link in card.select("a[href]").iter() {
            let Some(link_id) = link.attr("href").as_deref().and_then(project_id) else {
                continue;
            };
            if id.get_or_insert(link_id) != &link_id {
                continue;
            }
            let text = link.text();
            let text = text.trim();
            if !text.is_empty() && !linked_text.iter().any(|value| value == text) {
                linked_text.push(text.to_owned());
            }
        }
        let Some(id) = id else { continue };
        // The card links its description, display name and optional English name to the same entry.
        let (name, description) = match linked_text.as_slice() {
            [description, name, original, ..] => {
                (format!("{name} ({original})"), description.clone())
            }
            [description, name, ..] => (name.clone(), description.clone()),
            [name] => (name.clone(), String::new()),
            _ => continue,
        };
        if !seen.insert(id) {
            continue;
        }
        let image = card.select_single("img");
        let icon = image
            .attr("data-src")
            .or_else(|| image.attr("src"))
            .and_then(|src| origin().join(&src).ok())
            .filter(|url| url.scheme() == "https" && url.host_str() == Some("i.mcmod.cn"))
            .map(|url| url.to_string())
            .unwrap_or_default();
        items.push(ContentItem {
            id: format!("{PROJECT_PREFIX}{id}"),
            category: "mod".into(),
            name,
            description,
            author: String::new(),
            icon,
            downloads: String::new(),
            tags: Vec::new(),
            game_versions: Vec::new(),
            loaders: Vec::new(),
        });
    }
    Ok(items)
}

pub(crate) async fn sources(client: &Client, id: u32) -> io::Result<Sources> {
    let mut url = origin();
    url.set_path(&format!("/class/{id}.html"));
    parse_sources(&fetch_page(client, url).await?)
}

fn parse_sources(html: &str) -> io::Result<Sources> {
    let doc = Document::from(html);
    if doc.select(".class-title").is_empty() {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            "MC 百科没有返回模组词条。",
        ));
    }
    let mut sources = Sources::default();
    for link in doc.select(".common-link-frame a[href]").iter() {
        let Some(url) = link.attr("href").as_deref().and_then(source_url) else {
            continue;
        };
        let Some(segments) = url.path_segments() else {
            continue;
        };
        let segments: Vec<_> = segments.collect();
        let (slot, slug) = match (url.host_str(), segments.as_slice()) {
            (Some("modrinth.com" | "www.modrinth.com"), ["mod", slug, ..]) => {
                (&mut sources.modrinth, *slug)
            }
            (Some("curseforge.com" | "www.curseforge.com"), ["minecraft", "mc-mods", slug, ..])
            | (Some("minecraft.curseforge.com"), ["projects" | "mc-mods", slug, ..]) => {
                (&mut sources.curseforge, *slug)
            }
            _ => continue,
        };
        if !slug.is_empty()
            && slug
                .bytes()
                .all(|byte| byte.is_ascii_alphanumeric() || byte == b'-' || byte == b'_')
        {
            slot.get_or_insert_with(|| slug.to_owned());
        }
    }
    Ok(sources)
}

fn source_url(href: &str) -> Option<Url> {
    let url = origin().join(href).ok()?;
    if matches!(
        url.host_str(),
        Some("link.mcmod.cn" | "www.mcmod.cn" | "mcmod.cn")
    ) {
        let encoded = url.path().split_once("/target/")?.1;
        let bytes = general_purpose::STANDARD
            .decode(encoded)
            .or_else(|_| general_purpose::STANDARD_NO_PAD.decode(encoded))
            .or_else(|_| general_purpose::URL_SAFE.decode(encoded))
            .or_else(|_| general_purpose::URL_SAFE_NO_PAD.decode(encoded))
            .ok()?;
        return Url::parse(std::str::from_utf8(&bytes).ok()?)
            .ok()
            .filter(|target| matches!(target.scheme(), "https" | "http"));
    }
    matches!(url.scheme(), "https" | "http").then_some(url)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn list_uses_catalog_cards_and_groups_project_links() {
        let items = parse_catalog(
            r#"
            <a href="/class/9.html">Navigation</a>
            <div class="modlist-list-frame">
              <div class="modlist-block">
                <a href="/class/2785.html">渲染优化</a>
                <a href="/class/2785.html">钠</a>
                <a href="/class/2785.html">Sodium</a>
                <img data-src="https://i.mcmod.cn/icon.png">
              </div>
            </div>
            <ul><li class="page-item active">1</li><li class="page-item">
              <a href="/modlist.html?platform=1&amp;page=2">2</a>
            </li></ul>
            <div>当前 1 / 2 页，共计 33 条。</div>
        "#,
        )
        .unwrap();
        let items = filter_items(&items, "Sodium");
        assert_eq!(items.len(), 1);
        assert_eq!(items[0].id, "mcmod:2785");
        assert_eq!(items[0].name, "钠 (Sodium)");
        assert_eq!(items[0].description, "渲染优化");
        assert_eq!(items[0].icon, "https://i.mcmod.cn/icon.png");
        assert!(parse_catalog("<title>Access denied</title>").is_err());
    }

    #[test]
    fn recognizes_project_links_and_skips_other_resource_types() {
        let encoded = general_purpose::STANDARD.encode("https://modrinth.com/mod/sodium");
        let sources = parse_sources(&format!(
            r#"
            <div class="class-title"><h3>钠</h3></div>
            <div class="common-link-frame">
              <a href="https://modrinth.com/resourcepack/translations-for-sodium">Translation</a>
              <a href="https://link.mcmod.cn/target/{encoded}">Modrinth</a>
              <a href="https://www.curseforge.com/minecraft/mc-mods/sodium">CurseForge</a>
              <a href="https://example.com/minecraft/mc-mods/not-a-provider">Other</a>
            </div>
        "#
        ))
        .unwrap();
        assert_eq!(sources.modrinth.as_deref(), Some("sodium"));
        assert_eq!(sources.curseforge.as_deref(), Some("sodium"));
    }
}
