//! 内容搜索、版本、加载器和收藏命令。

use tauri::State;

use super::{
    implementation::{generated_id, list_demo_content_versions, search_demo_content},
    mod_download::error,
};
use crate::{
    core::mod_sources::{self, mcmod, modrinth, modrinth_search},
    model::{ApiError, ContentSearchResult, ResourceVersionInfo, ResourceVersionsInfo},
    state::Backend,
};

#[allow(unused_imports)]
pub use super::{
    implementation::toggle_bookmark,
    modrinth_pack::{confirm_pack, preview_pack},
};
#[allow(unused_imports)]
pub use super::vanilla::{install_loader, install_vanilla, list_loaders, list_vanilla_versions};

/// 查询当前模组索引。桌面端默认使用 Modrinth。
#[tauri::command]
pub async fn search_content(
    backend: State<'_, Backend>,
    category: String,
    query: String,
    game: Option<String>,
    loader: Option<String>,
) -> Result<ContentSearchResult, ApiError> {
    if category != "mod" {
        return Ok(search_demo_content(backend, category, query, game, loader));
    }
    let (source, items) = if matches!(std::env::var("MCUG_MOD_INDEX").as_deref(), Ok("mcmod")) {
        let items = backend
            .mod_catalog
            .lock()
            .await
            .search(query.trim())
            .await
            .map_err(|failure| error("NETWORK_ERROR", failure))?;
        ("mcmod", items)
    } else {
        let client = mod_sources::client().map_err(modrinth_error)?;
        let items = modrinth_search::search(
            &client,
            query.trim(),
            game.as_deref().unwrap_or_default().trim(),
            loader.as_deref().unwrap_or_default().trim(),
        )
        .await
        .map_err(modrinth_error)?;
        ("modrinth", items)
    };
    Ok(ContentSearchResult {
        category,
        request_id: generated_id(source),
        items,
    })
}

/// 按项目来源查询真实版本和下载文件。
#[tauri::command]
pub async fn list_content_versions(
    backend: State<'_, Backend>,
    id: String,
    request_id: Option<String>,
    game: Option<String>,
    loader: Option<String>,
    #[allow(unused_variables)] category: Option<String>,
    #[allow(unused_variables)] instance: Option<String>,
) -> Result<ResourceVersionsInfo, ApiError> {
    let modrinth_id = id.strip_prefix(modrinth::PROJECT_PREFIX);
    let mcmod_id = id.strip_prefix(mcmod::PROJECT_PREFIX);
    if modrinth_id.is_none() && mcmod_id.is_none() {
        return Ok(list_demo_content_versions(
            backend, id, request_id, game, loader,
        ));
    }
    let game = game.as_deref().unwrap_or_default().trim();
    let loader = loader.unwrap_or_default().trim().to_ascii_lowercase();
    let client =
        mod_sources::client().map_err(|failure| error("NETWORK_ERROR", failure.without_url()))?;
    let versions = if let Some(project_id) = modrinth_id {
        if project_id.is_empty() || !project_id.bytes().all(|byte| byte.is_ascii_alphanumeric()) {
            return Err(ApiError::validation(
                "无法读取模组版本。",
                "id",
                "Modrinth 项目 ID 无效。",
            ));
        }
        modrinth::versions(&client, project_id, game, &loader)
            .await
            .map_err(modrinth_error)?
    } else {
        let entry_id: u32 = mcmod_id
            .and_then(|value| value.parse().ok())
            .filter(|id| *id != 0)
            .ok_or_else(|| {
                ApiError::validation("无法读取模组词条。", "id", "MC 百科词条 ID 无效。")
            })?;
        let sources = mcmod::sources(&client, entry_id)
            .await
            .map_err(|failure| error("NETWORK_ERROR", failure))?;
        mod_sources::versions(&client, sources, game, &loader)
            .await
            .map_err(|failure| {
                let code = if failure.kind() == std::io::ErrorKind::PermissionDenied {
                    "AUTH_REQUIRED"
                } else {
                    "NETWORK_ERROR"
                };
                error(code, failure)
            })?
    };
    let items = versions
        .iter()
        .map(|version| ResourceVersionInfo {
            id: version.id.clone(),
            name: version.name.clone(),
            files: vec![version.file.filename.clone()],
            game_versions: version.game_versions.clone(),
            loaders: version.loaders.clone(),
            version_type: version.version_type.clone(),
        })
        .collect();
    let token = if versions.is_empty() {
        String::new()
    } else {
        backend.mod_downloads.lock().unwrap().remember(versions)
    };
    Ok(ResourceVersionsInfo {
        request_id: request_id
            .filter(|request| !request.is_empty())
            .unwrap_or(id),
        token,
        game: if game.is_empty() {
            "不限游戏版本"
        } else {
            game
        }
        .into(),
        loader: if loader.is_empty() {
            "不限加载器"
        } else {
            &loader
        }
        .into(),
        items,
    })
}

fn modrinth_error(failure: reqwest::Error) -> ApiError {
    if failure.status() == Some(reqwest::StatusCode::NOT_FOUND) {
        return error("NOT_FOUND", "Modrinth 项目不存在。");
    }
    error(
        "NETWORK_ERROR",
        format!("Modrinth 请求失败：{}", failure.without_url()),
    )
}
