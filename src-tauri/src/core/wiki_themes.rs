//! 从中文 Minecraft Wiki 获取「Java版版本记录」主题分类并持久化本地缓存。

use std::{
    collections::BTreeMap,
    fs,
    path::Path,
    sync::OnceLock,
    time::{Duration, SystemTime, UNIX_EPOCH},
};

use regex::Regex;
use reqwest::blocking::Client;
use serde::{Deserialize, Serialize};

const WIKI_API_URL: &str = "https://zh.minecraft.wiki/api.php?action=parse&page=Java%E7%89%88%E7%89%88%E6%9C%AC%E8%AE%B0%E5%BD%95&prop=wikitext&format=json&formatversion=2";
const CACHE_TTL_SECS: u64 = 24 * 60 * 60;
const MIN_VALID_ENTRIES: usize = 10;

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct CachedThemes {
    fetched_at: u64,
    themes: BTreeMap<String, String>,
}

#[derive(Deserialize)]
struct WikiParseResponse {
    parse: Option<WikiParseBody>,
}

#[derive(Deserialize)]
struct WikiParseBody {
    wikitext: Option<String>,
}

fn now_unix_secs() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|duration| duration.as_secs())
        .unwrap_or(0)
}

fn read_cache(cache_path: &Path) -> Option<CachedThemes> {
    let bytes = fs::read(cache_path).ok()?;
    let cached: CachedThemes = serde_json::from_slice(&bytes).ok()?;
    if cached.themes.is_empty() {
        return None;
    }
    Some(cached)
}

fn write_cache(cache_path: &Path, themes: &BTreeMap<String, String>) {
    if let Some(parent) = cache_path.parent() {
        let _ = fs::create_dir_all(parent);
    }
    let payload = CachedThemes {
        fetched_at: now_unix_secs(),
        themes: themes.clone(),
    };
    if let Ok(json) = serde_json::to_vec_pretty(&payload) {
        let _ = fs::write(cache_path, json);
    }
}

fn fetch_wikitext() -> Result<String, String> {
    let client = Client::builder()
        .timeout(Duration::from_secs(12))
        .user_agent("MCUGLauncher/0.1 (https://github.com/MCUGLauncher)")
        .build()
        .map_err(|error| format!("初始化 Wiki HTTP 客户端失败：{error}"))?;
    let response: WikiParseResponse = client
        .get(WIKI_API_URL)
        .send()
        .map_err(|error| format!("请求中文 Minecraft Wiki 失败：{error}"))?
        .error_for_status()
        .map_err(|error| format!("中文 Minecraft Wiki 返回异常状态：{error}"))?
        .json()
        .map_err(|error| format!("解析中文 Minecraft Wiki 响应失败：{error}"))?;
    response
        .parse
        .and_then(|body| body.wikitext)
        .ok_or_else(|| "中文 Minecraft Wiki 响应中缺少 wikitext 内容。".to_string())
}

fn clean_wiki_markup(raw: &str) -> String {
    static REF_RE: OnceLock<Regex> = OnceLock::new();
    static HTML_RE: OnceLock<Regex> = OnceLock::new();
    static LINK_LABEL_RE: OnceLock<Regex> = OnceLock::new();
    static LINK_PLAIN_RE: OnceLock<Regex> = OnceLock::new();
    static TEMPLATE_RE: OnceLock<Regex> = OnceLock::new();

    let ref_re = REF_RE.get_or_init(|| Regex::new(r"(?is)<ref[^>]*>.*?</ref>|<ref[^/>]*/>").unwrap());
    let html_re = HTML_RE.get_or_init(|| Regex::new(r"<[^>]+>").unwrap());
    let link_label_re = LINK_LABEL_RE.get_or_init(|| Regex::new(r"\[\[[^\[\]|]+\|([^\[\]]+)\]\]").unwrap());
    let link_plain_re = LINK_PLAIN_RE.get_or_init(|| Regex::new(r"\[\[([^\[\]|]+)\]\]").unwrap());
    let template_re = TEMPLATE_RE.get_or_init(|| Regex::new(r"\{\{[^{}]*\}\}").unwrap());

    let step = ref_re.replace_all(raw, "");
    let step = html_re.replace_all(&step, "");
    let step = link_label_re.replace_all(&step, "$1");
    let step = link_plain_re.replace_all(&step, "$1");
    let step = template_re.replace_all(&step, "");
    step.replace("'''", "")
        .replace("''", "")
        .trim()
        .to_string()
}

fn is_ignored_heading(title: &str) -> bool {
    const IGNORED: &[&str] = &[
        "正式版",
        "完整版",
        "开发版本",
        "预览版",
        "远古版本",
        "愚人节",
        "目录",
        "参考",
        "注释",
        "另见",
        "外部链接",
        "导航",
        "历史",
        "Beta",
        "Alpha",
        "Infdev",
        "Indev",
        "Classic",
        "Pre-classic",
    ];
    IGNORED.iter().any(|word| title.eq_ignore_ascii_case(word))
}

/// 从标题中解析出 `(可选版本号, 主题名)`。
/// 兼容：`至深寒意`、`26.4（至深寒意）`、`1.21.4 (苍园觉醒)`、`至深寒意（26.4）` 等写法。
fn parse_heading_theme(raw_heading: &str) -> Option<(Option<String>, String)> {
    static VER_THEN_THEME: OnceLock<Regex> = OnceLock::new();
    static THEME_THEN_VER: OnceLock<Regex> = OnceLock::new();
    static PURE_VERSION: OnceLock<Regex> = OnceLock::new();

    let cleaned = clean_wiki_markup(raw_heading);
    if cleaned.is_empty() || is_ignored_heading(&cleaned) {
        return None;
    }

    let ver_then_theme = VER_THEN_THEME
        .get_or_init(|| Regex::new(r"^(\d+\.\d+(?:\.\d+)?)\s*[（(：:\-—]\s*([^）)]+?)\s*[）)]?$").unwrap());
    if let Some(caps) = ver_then_theme.captures(&cleaned) {
        let ver = caps[1].to_string();
        let theme = caps[2].trim().to_string();
        if !theme.is_empty() && !is_ignored_heading(&theme) {
            return Some((Some(ver), theme));
        }
    }

    let theme_then_ver = THEME_THEN_VER
        .get_or_init(|| Regex::new(r"^([^（(]+?)\s*[（(]\s*(\d+\.\d+(?:\.\d+)?)\s*[）)]$").unwrap());
    if let Some(caps) = theme_then_ver.captures(&cleaned) {
        let theme = caps[1].trim().to_string();
        let ver = caps[2].to_string();
        if !theme.is_empty() && !is_ignored_heading(&theme) {
            return Some((Some(ver), theme));
        }
    }

    let pure_version = PURE_VERSION
        .get_or_init(|| Regex::new(r"^\d+\.\d+(?:\.\d+)?(?:\s*(?:版本|系列))?$").unwrap());
    if pure_version.is_match(&cleaned) {
        return None;
    }

    Some((None, cleaned))
}

/// 解析中文 Minecraft Wiki「Java版版本记录」的 wikitext，提取 `正式版版本号 -> 主题名` 映射。
/// 仅从章节标题与表格/列表首列的正式版条目提取，避免正文提及旧版本或快照时造成误归类。
pub fn parse_wiki_themes(wikitext: &str) -> BTreeMap<String, String> {
    static HEADING_RE: OnceLock<Regex> = OnceLock::new();
    static RELEASE_CELL_RE: OnceLock<Regex> = OnceLock::new();

    let heading_re = HEADING_RE.get_or_init(|| Regex::new(r"^={2,4}\s*(.+?)\s*={2,4}\s*$").unwrap());
    // 只匹配表格单元格或列表项开头的正式版链接（如 `| [[Java版 26.4|26.4]]`、`* [[Java版 1.21.4]]`）
    let release_cell_re = RELEASE_CELL_RE.get_or_init(|| {
        Regex::new(r"^(?:\|\s*|!\s*|\*+\s*)\[\[Java版\s+(\d+\.\d+(?:\.\d+)?)(?:\|[^\[\]]*)?\]\]")
            .unwrap()
    });

    let mut map = BTreeMap::new();
    let mut current_theme: Option<String> = None;

    for line in wikitext.lines() {
        let trimmed = line.trim();
        if let Some(caps) = heading_re.captures(trimmed) {
            let raw_title = &caps[1];
            match parse_heading_theme(raw_title) {
                Some((maybe_ver, theme)) => {
                    if let Some(ver) = maybe_ver {
                        map.entry(ver).or_insert_with(|| theme.clone());
                    }
                    current_theme = Some(theme);
                }
                None => {
                    current_theme = None;
                }
            }
            continue;
        }

        let Some(theme) = current_theme.as_ref() else {
            continue;
        };

        if let Some(caps) = release_cell_re.captures(trimmed) {
            let ver = caps[1].to_string();
            map.entry(ver).or_insert_with(|| theme.clone());
        }
    }

    map
}

/// 读取版本主题缓存；缓存缺失、过期（24 小时）或要求强制刷新时从中文 Minecraft Wiki 拉取。
/// 若拉取失败但存在旧缓存，自动回退到旧缓存。
pub fn load_or_refresh_themes(
    cache_path: &Path,
    force_refresh: bool,
) -> Result<BTreeMap<String, String>, String> {
    let cached = read_cache(cache_path);
    if !force_refresh {
        if let Some(ref hit) = cached {
            let age = now_unix_secs().saturating_sub(hit.fetched_at);
            if age < CACHE_TTL_SECS {
                return Ok(hit.themes.clone());
            }
        }
    }

    match fetch_wikitext().map(|text| parse_wiki_themes(&text)) {
        Ok(themes) if themes.len() >= MIN_VALID_ENTRIES => {
            write_cache(cache_path, &themes);
            Ok(themes)
        }
        Ok(_) => {
            if let Some(hit) = cached {
                Ok(hit.themes)
            } else {
                Err("未能从中文 Minecraft Wiki 解析出有效的版本主题列表。".to_string())
            }
        }
        Err(error) => {
            if let Some(hit) = cached {
                Ok(hit.themes)
            } else {
                Err(error)
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::parse_wiki_themes;

    #[test]
    fn parses_headings_and_version_links() {
        let sample = r#"
== 正式版 ==
=== [[至深寒意]] ===
| [[Java版 26.4|26.4]] || 2026年11月 || 参考了 [[Java版 1.21.4]] 的机制
=== 26.3（奔赴荒野） ===
| [[Java版 26.3]]
| [[Java版 26.3.1|26.3.1]]
=== 混沌立方（26.2） ===
| [[Java版 26.2]]
=== 小鬼当家 ===
| [[Java版 26.1]]
* [[Java版 26.1.1|26.1.1]]
=== 群骑纷争 ===
| [[Java版 1.21.11|1.21.11]]
=== 1.21 ===
| [[Java版 1.21.4|1.21.4]]
== 外部链接 ==
| [[Java版 1.0.0]]
"#;
        let map = parse_wiki_themes(sample);
        assert_eq!(map.get("26.4").map(String::as_str), Some("至深寒意"));
        assert_eq!(map.get("26.3").map(String::as_str), Some("奔赴荒野"));
        assert_eq!(map.get("26.3.1").map(String::as_str), Some("奔赴荒野"));
        assert_eq!(map.get("26.2").map(String::as_str), Some("混沌立方"));
        assert_eq!(map.get("26.1").map(String::as_str), Some("小鬼当家"));
        assert_eq!(map.get("26.1.1").map(String::as_str), Some("小鬼当家"));
        assert_eq!(map.get("1.21.11").map(String::as_str), Some("群骑纷争"));
        // 纯版本号标题（如 === 1.21 ===）与正文提及的旧版本均不应生成「xxx 版本」覆盖真实主题
        assert!(!map.contains_key("1.21"));
        assert!(!map.contains_key("1.21.4"));
        assert!(!map.contains_key("1.0.0"));
    }
}
