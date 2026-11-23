//! 前端包管理命令：导入、列出、切换、移除。
//!
//! 这些是宿主自己的管理接口，不属于对第三方前端公开的内核方法表
//! （`kernel_manifest.methods` 仍然只列 `frontend::` 能调用的 66 个方法）。

use std::path::Path;
use std::time::Duration;

use serde::Serialize;
use tauri::{AppHandle, State};

use crate::{
    frontends::{self, InstalledFrontend, PackageError},
    model::{ApiError, ResourceKeyInfo, SavedInfo},
    state::Backend,
};

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct FrontendSummary {
    pub identifier: String,
    pub name: String,
    pub version: String,
    pub kernel_api: String,
    pub entry: String,
    pub author: Option<String>,
    pub description: Option<String>,
    pub homepage: Option<String>,
    /// 包内是否带图标；`true` 时界面可以调 `read_frontend_icon` 取图。
    pub has_icon: bool,
    /// 是否是当前选中的前端（多个版本时只有最新版本会被加载）。
    pub active: bool,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct FrontendList {
    /// 配置里选中的 identifier；`None` 表示官方前端。
    pub active: Option<String>,
    pub items: Vec<FrontendSummary>,
}

fn api_error(code: &str, message: impl Into<String>, retryable: bool) -> ApiError {
    ApiError {
        code: code.into(),
        message: message.into(),
        retryable,
        field_errors: Default::default(),
    }
}

fn map_error(error: PackageError) -> ApiError {
    let (code, retryable) = match &error {
        PackageError::Invalid(_) => ("VALIDATION_ERROR", false),
        PackageError::Conflict(_) => ("CONFLICT", false),
        PackageError::Io(_) => ("IO_ERROR", true),
    };
    api_error(code, error.message().to_string(), retryable)
}

fn summary(item: &InstalledFrontend, active: Option<&str>) -> FrontendSummary {
    FrontendSummary {
        identifier: item.identifier.clone(),
        name: item.name.clone(),
        version: item.version.clone(),
        kernel_api: item.kernel_api.clone(),
        entry: item.entry.clone(),
        author: item.author.clone(),
        description: item.description.clone(),
        homepage: item.homepage.clone(),
        has_icon: item.icon.is_some(),
        active: active == Some(item.identifier.as_str()),
    }
}

#[tauri::command]
pub fn list_frontends(backend: State<'_, Backend>) -> Result<FrontendList, ApiError> {
    let active = backend
        .data
        .lock()
        .map(|data| data.state.config.frontend.clone())
        .unwrap_or_default();
    let items = frontends::list(backend.config_dir())
        .map_err(|message| api_error("IO_ERROR", message, true))?;
    Ok(FrontendList {
        active: active.clone(),
        items: items
            .iter()
            .map(|item| summary(item, active.as_deref()))
            .collect(),
    })
}

/// 读取已安装前端的图标，返回 `data:` URL；没有图标时返回 `null`，界面回退到内置图标。
#[tauri::command]
pub fn read_frontend_icon(
    backend: State<'_, Backend>,
    identifier: String,
    version: Option<String>,
) -> Result<Option<String>, ApiError> {
    let items = frontends::list(backend.config_dir())
        .map_err(|message| api_error("IO_ERROR", message, true))?;
    let item = if let Some(version) = version {
        items
            .into_iter()
            .find(|item| item.identifier == identifier && item.version == version)
    } else {
        items
            .into_iter()
            .filter(|item| item.identifier == identifier)
            .max_by_key(|item| semver::Version::parse(&item.version).ok())
    };
    let Some(item) = item else {
        return Ok(None);
    };
    let Some(icon) = item.icon else {
        return Ok(None);
    };
    frontends::icon_data_url(&icon).map_err(|message| api_error("IO_ERROR", message, true))
}

#[tauri::command]
pub fn install_frontend(    backend: State<'_, Backend>,
    path: String,
) -> Result<FrontendSummary, ApiError> {
    if path.trim().is_empty() {
        return Err(api_error("VALIDATION_ERROR", "没有选择前端包。", false));
    }
    let installed = frontends::install(backend.config_dir(), Path::new(&path)).map_err(map_error)?;
    let active = backend
        .data
        .lock()
        .map(|data| data.state.config.frontend.clone())
        .unwrap_or_default();
    Ok(summary(&installed, active.as_deref()))
}

/// 切换当前前端；`identifier` 为 `null` 时切回官方前端。重启后生效。
#[tauri::command]
pub fn use_frontend(
    backend: State<'_, Backend>,
    identifier: Option<String>,
    expected_revision: u64,
) -> Result<SavedInfo, ApiError> {
    let mut data = backend.data.lock().unwrap();
    if let Some(identifier) = &identifier {
        frontends::resolve_active(backend.config_dir(), identifier).map_err(|message| {
            api_error(
                "VALIDATION_ERROR",
                format!("这个前端无法加载：{message}"),
                false,
            )
        })?;
    }
    let mut config = data.state.config.clone();
    config.frontend = identifier;
    backend.save_config(&mut data, config, expected_revision)?;
    Ok(SavedInfo::changed(
        "前端已切换，点「重启前端」立即生效（或重启启动器）。",
        vec![ResourceKeyInfo {
            kind: "settings".into(),
            instance_id: None,
        }],
        Vec::new(),
    ))
}

/// 立即按当前配置重建界面，让新选中的前端生效。不退出进程。
#[tauri::command]
pub fn reload_frontend(app: AppHandle, backend: State<'_, Backend>) -> Result<SavedInfo, ApiError> {
    let config_dir = backend.config_dir().to_path_buf();
    let selected = backend
        .data
        .lock()
        .map(|data| data.state.config.frontend.clone())
        .map_err(|_| api_error("INTERNAL_ERROR", "状态锁已损坏。", false))?;
    let next = crate::resolve_selected_frontend(&config_dir, selected).map_err(|message| {
        api_error(
            "VALIDATION_ERROR",
            format!("这个前端无法加载：{message}"),
            false,
        )
    })?;
    let label = next
        .as_ref()
        .map(|frontend| {
            format!(
                "{} {}",
                frontend.manifest.name, frontend.manifest.version
            )
        })
        .unwrap_or_else(|| "官方前端".to_string());

    let handle = app;
    // 先让这次调用的响应回到页面，再重建窗口；否则页面会在拿到结果前被销毁，
    // 前端就只能看到一条「调用中断」，分不清是成功还是失败。
    //
    // 注意：必须在独立线程里建窗。`WebviewWindowBuilder::build()` 会把建窗请求
    // 投递到事件循环并阻塞等待结果；如果在主线程（事件循环）里调用就会死锁。
    std::thread::spawn(move || {
        std::thread::sleep(Duration::from_millis(300));
        if let Err(error) = crate::switch_frontend(&handle, next) {
            crate::report_warning(
                &handle,
                "无法重启前端",
                &format!("{error}\n\n可以关闭启动器后重新打开。"),
            );
        }
    });

    Ok(SavedInfo::changed(
        format!("正在重启前端：{label}…"),
        Vec::new(),
        Vec::new(),
    ))
}

/// 移除已安装的前端；`version` 为空时移除全部版本。
#[tauri::command]
pub fn remove_frontend(
    backend: State<'_, Backend>,
    identifier: String,
    version: Option<String>,
    expected_revision: u64,
) -> Result<SavedInfo, ApiError> {
    let removed = frontends::remove(backend.config_dir(), &identifier, version.as_deref())
        .map_err(map_error)?;
    if !removed {
        return Err(api_error(
            "NOT_FOUND",
            format!("前端 {identifier} 不在已安装列表里。"),
            false,
        ));
    }
    let mut data = backend.data.lock().unwrap();
    let config = data.state.config.clone();
    let still_selected = config.frontend.as_deref() == Some(identifier.as_str());
    let any_left = frontends::list(backend.config_dir())
        .map(|items| items.iter().any(|item| item.identifier == identifier))
        .unwrap_or(false);
    if still_selected && !any_left {
        let mut config = config;
        config.frontend = None;
        backend.save_config(&mut data, config, expected_revision)?;
        return Ok(SavedInfo::changed(
            "前端已移除，并切回官方前端；点「重启前端」立即生效。",
            vec![ResourceKeyInfo {
                kind: "settings".into(),
                instance_id: None,
            }],
            Vec::new(),
        ));
    }
    Ok(SavedInfo::changed("前端已移除。", Vec::new(), Vec::new()))
}
