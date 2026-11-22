//! Tauri 命令层：前端唯一的后端入口。
//!
//! 约定：
//! - 命令名使用 snake_case，前端通过 `invoke("命令名", { camelCase 参数 })` 调用；
//! - 命令返回语义化结果，失败通过 `Err(ApiError)` 抛出；
//! - 状态变更由前端在命令返回后重新调用查询命令获取，事件仅作为可选的推送优化。

#![allow(unused_variables)]

use serde::Serialize;
use std::collections::HashMap;
use std::path::Path;
use std::time::{SystemTime, UNIX_EPOCH};
use tauri::{AppHandle, Emitter, State};
use time::{Duration, OffsetDateTime, format_description::well_known::Rfc3339};

use crate::{
    DEVICE_OS,
    demo::{StateResource, avatar_for},
    launcher_config::LauncherConfig,
    local_instances,
    model::*,
    state::Backend,
};

pub const EVENT_TASKS: &str = "launcher://tasks";
pub const EVENT_COMMUNITY: &str = "launcher://community";
pub const EVENT_AVATAR: &str = "launcher://avatar";

fn emit<T: Serialize + Clone>(app: &AppHandle, event: &str, payload: T) {
    let _ = app.emit(event, payload);
}

fn generated_id(prefix: &str) -> String {
    let value = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|duration| duration.as_nanos())
        .unwrap_or_default();
    format!("{prefix}-{value}")
}

// ---------------------------------------------------------------- 状态

/// 返回只包含全局选择的概览资源。
#[tauri::command]
pub fn get_overview(backend: State<'_, Backend>) -> ResourceSnapshot {
    let data = backend.data.lock().unwrap();
    data.resource_snapshot(
        data.revisions.overview,
        LauncherOverview {
            app_version: data.state.app_version.clone(),
            selected_instance_id: data.state.selected_instance_id.clone(),
            active_account_id: data.state.active_account_id.clone(),
        },
    )
}

/// 返回全局设置资源。
#[tauri::command]
pub fn get_settings(backend: State<'_, Backend>) -> ResourceSnapshot {
    let data = backend.data.lock().unwrap();
    let state = &data.state;
    let config = &state.config;
    let active_sources = if config.data_source_mode == "custom" {
        &config.custom_data_sources
    } else {
        &state.default_data_sources
    };
    data.resource_snapshot(
        data.revisions.settings,
        SettingsInfo {
            root: config.root().into(),
            game_roots: config.game_roots.clone(),
            java: config.java.clone(),
            width: config.width as i64,
            height: config.height as i64,
            ram: config.ram as i64,
            auto_memory: config.auto_memory,
            anonymous: config.anonymous,
            has_community_key: state.has_community_key,
            has_admin_key: state.has_admin_key,
            skin_station: state.skin_station.clone(),
            cape: state.cape.clone(),
            data_sources: active_sources.clone(),
            default_data_sources: state.default_data_sources.clone(),
            custom_data_sources: config.custom_data_sources.clone(),
            data_source_mode: config.data_source_mode.clone(),
            auto_chinese: config.auto_chinese,
        },
    )
}

#[tauri::command]
pub fn list_accounts(backend: State<'_, Backend>) -> ResourceSnapshot {
    let data = backend.data.lock().unwrap();
    data.resource_snapshot(data.revisions.accounts, data.state.accounts.clone())
}

#[tauri::command]
pub fn list_instances(backend: State<'_, Backend>) -> ResourceSnapshot {
    let data = backend.data.lock().unwrap();
    data.resource_snapshot(
        data.revisions.instances,
        InstanceCatalogInfo {
            items: data.state.instances.clone(),
            version_groups: data.state.version_groups.clone(),
        },
    )
}

/// 扫描所有已登记游戏目录，并合并其本地实例。
#[tauri::command]
pub fn list_all_instances(backend: State<'_, Backend>) -> Result<InstanceCatalogInfo, ApiError> {
    let roots = backend.data.lock().unwrap().state.config.game_roots.clone();
    let mut combined = InstanceCatalogInfo {
        items: Vec::new(),
        version_groups: Vec::new(),
    };
    let mut groups = std::collections::BTreeMap::<String, Vec<VersionChoice>>::new();

    for root in roots {
        let path = Path::new(&root);
        let catalog = local_instances::scan(path).map_err(|error| game_root_error(path, error))?;
        combined.items.extend(catalog.items);
        for group in catalog.version_groups {
            groups.entry(group.label).or_default().extend(group.versions);
        }
    }

    combined.items.sort_by(|left, right| left.id.cmp(&right.id));
    combined.version_groups = groups
        .into_iter()
        .map(|(label, mut versions)| {
            versions.sort_by(|left, right| left.name.cmp(&right.name));
            VersionGroupInfo {
                label,
                count: versions.len() as u64,
                versions,
            }
        })
        .collect();
    Ok(combined)
}

/// 从已登记列表移除游戏目录，不会删除目录中的文件。
#[tauri::command]
pub fn remove_game_root(
    backend: State<'_, Backend>,
    root: String,
    expected_revision: u64,
) -> Result<SavedInfo, ApiError> {
    let mut data = backend.data.lock().unwrap();
    data.ensure_settings_revision(expected_revision)?;
    let mut config = data.state.config.clone();
    if !config.game_roots.iter().any(|entry| entry == &root) {
        return Err(ApiError {
            code: "NOT_FOUND".into(),
            message: "游戏目录不在已登记列表中。".into(),
            retryable: false,
            field_errors: HashMap::new(),
        });
    }
    config.remove_root(&root);
    let active_root_removed = data.state.config.root() == root;
    let catalog = if active_root_removed {
        let next_root = config.root().to_owned();
        Some(if next_root.is_empty() {
            local_instances::scan(Path::new("")).map_err(|error| game_root_error(Path::new(""), error))?
        } else {
            select_game_root(&mut config, next_root)?
        })
    } else {
        None
    };
    backend.save_config(&mut data, config, expected_revision)?;
    if let Some(catalog) = catalog {
        data.replace_instance_catalog(catalog);
    }
    let mut changed = vec![ResourceKeyInfo {
        kind: "settings".into(),
        instance_id: None,
    }];
    if active_root_removed {
        changed.extend(["instances", "overview", "instanceMods"].map(|kind| ResourceKeyInfo {
            kind: kind.into(),
            instance_id: None,
        }));
    }
    Ok(SavedInfo::changed("游戏目录已移除。", changed, vec![root]))
}

#[tauri::command]
pub fn list_instance_mods(
    backend: State<'_, Backend>,
    instance_id: String,
) -> Result<ResourceSnapshot, ApiError> {
    let data = backend.data.lock().unwrap();
    if !data
        .state
        .instances
        .iter()
        .any(|item| item.id == instance_id)
    {
        return Err(ApiError {
            code: "NOT_FOUND".into(),
            message: "实例不存在。".into(),
            retryable: false,
            field_errors: HashMap::new(),
        });
    }
    let items = if data.state.selected_instance_id.as_deref() == Some(&instance_id) {
        data.state.mods.clone()
    } else {
        Vec::new()
    };
    Ok(data.resource_snapshot(data.revisions.instance_mods, items))
}

#[tauri::command]
pub fn list_servers(backend: State<'_, Backend>) -> ResourceSnapshot {
    let data = backend.data.lock().unwrap();
    data.resource_snapshot(data.revisions.servers, data.state.servers.clone())
}

#[tauri::command]
pub fn list_bookmarks(backend: State<'_, Backend>) -> ResourceSnapshot {
    let data = backend.data.lock().unwrap();
    data.resource_snapshot(data.revisions.bookmarks, data.state.favorites.clone())
}

/// 切换当前游戏目录中的实例。
#[tauri::command]
pub fn select_instance(
    app: AppHandle,
    backend: State<'_, Backend>,
    instance_id: String,
) -> Result<SavedInfo, ApiError> {
    let state = {
        let mut data = backend.data.lock().unwrap();
        if !data
            .state
            .instances
            .iter()
            .any(|item| item.id == instance_id)
        {
            return Err(ApiError {
                code: "NOT_FOUND".into(),
                message: "实例不存在。".into(),
                retryable: false,
                field_errors: HashMap::new(),
            });
        }
        data.state.selected_instance_id = Some(instance_id.clone());
        data.mutating_snapshot(&[StateResource::Overview])
    };
    drop(state);
    Ok(SavedInfo::changed(
        "当前实例已切换。",
        vec![
            ResourceKeyInfo {
                kind: "overview".into(),
                instance_id: None,
            },
            ResourceKeyInfo {
                kind: "instanceMods".into(),
                instance_id: Some(instance_id),
            },
        ],
        Vec::new(),
    ))
}

// ---------------------------------------------------------------- 启动

/// 启动指定版本；内核重写后接入真实启动流程。
#[tauri::command]
#[allow(unused_variables)]
pub fn launch_game(
    instance_id: String,
    account_id: String,
) -> Result<LaunchAcceptedInfo, ApiError> {
    Err(ApiError::unsupported("launch_game"))
}

/// 使用指定实例直连服务器启动。
#[tauri::command]
#[allow(unused_variables)]
pub fn launch_to_server(
    instance_id: String,
    account_id: String,
    server_address: String,
) -> Result<LaunchAcceptedInfo, ApiError> {
    Err(ApiError::unsupported("launch_to_server"))
}

// ---------------------------------------------------------------- 服务器

/// 按稳定 ID 新增或更新服务器收藏。
#[tauri::command]
pub fn save_server(
    app: AppHandle,
    backend: State<'_, Backend>,
    name: String,
    address: String,
    id: Option<String>,
    instance_id: Option<String>,
) -> Result<SavedInfo, ApiError> {
    let mut message = "服务器已添加。".to_string();
    let state = {
        let mut data = backend.data.lock().unwrap();
        if let Some(id) = id.filter(|value| !value.is_empty()) {
            if let Some(server) = data.state.servers.iter_mut().find(|item| item.id == id) {
                server.name = name.clone();
                server.address = address.clone();
                server.instance_id = instance_id.clone();
                message = "服务器已更新。".to_string();
            } else {
                return Err(ApiError {
                    code: "NOT_FOUND".into(),
                    message: "服务器收藏不存在。".into(),
                    retryable: false,
                    field_errors: HashMap::new(),
                });
            }
        } else if data
            .state
            .servers
            .iter()
            .any(|item| item.address == address)
        {
            return Err(ApiError {
                code: "CONFLICT".into(),
                message: "这个地址已经在收藏里了。".into(),
                retryable: false,
                field_errors: HashMap::new(),
            });
        } else {
            data.state.servers.push(SavedServerInfo {
                id: generated_id("server"),
                name,
                address,
                instance_id,
            });
        }
        data.mutating_snapshot(&[StateResource::Servers])
    };
    drop(state);
    Ok(SavedInfo::changed(
        message,
        vec![ResourceKeyInfo {
            kind: "servers".into(),
            instance_id: None,
        }],
        Vec::new(),
    ))
}

/// 删除服务器收藏。
#[tauri::command]
pub fn delete_server(
    app: AppHandle,
    backend: State<'_, Backend>,
    id: String,
) -> Result<SavedInfo, ApiError> {
    let state = {
        let mut data = backend.data.lock().unwrap();
        let before = data.state.servers.len();
        data.state.servers.retain(|item| item.id != id);
        if data.state.servers.len() == before {
            return Err(ApiError {
                code: "NOT_FOUND".into(),
                message: "服务器收藏不存在。".into(),
                retryable: false,
                field_errors: HashMap::new(),
            });
        }
        data.mutating_snapshot(&[StateResource::Servers])
    };
    drop(state);
    Ok(SavedInfo::changed(
        "服务器已删除。",
        vec![ResourceKeyInfo {
            kind: "servers".into(),
            instance_id: None,
        }],
        vec![id],
    ))
}

/// 查询服务器在线状态与延迟。
#[tauri::command]
#[allow(unused_variables)]
pub fn ping_server(address: String) -> Result<ServerPingInfo, ApiError> {
    Err(ApiError::unsupported("ping_server"))
}

// ---------------------------------------------------------------- 账号

/// 读取当前账号头像。
#[tauri::command]
pub fn read_avatar(backend: State<'_, Backend>) -> AvatarInfo {
    backend.data.lock().unwrap().avatar_for_current()
}

/// 创建登录会话；演示后端未连接认证服务。
#[tauri::command]
#[allow(unused_variables)]
pub fn start_login(
    provider: String,
    account: Option<String>,
    password: Option<String>,
) -> Result<LoginSessionInfo, ApiError> {
    Err(ApiError::unsupported("start_login"))
}

/// 按稳定 ID 切换当前账号。
#[tauri::command]
pub fn select_account(
    app: AppHandle,
    backend: State<'_, Backend>,
    account_id: String,
) -> Result<SavedInfo, ApiError> {
    let (state, avatar) = {
        let mut data = backend.data.lock().unwrap();
        let account = data.select_account(&account_id)?;
        let avatar = avatar_for(&account.player, &account.kind);
        data.avatar = avatar.clone();
        (
            data.mutating_snapshot(&[StateResource::Overview, StateResource::Accounts]),
            avatar,
        )
    };
    drop(state);
    emit(&app, EVENT_AVATAR, avatar);
    Ok(SavedInfo::changed(
        "当前账号已切换。",
        vec![
            ResourceKeyInfo {
                kind: "overview".into(),
                instance_id: None,
            },
            ResourceKeyInfo {
                kind: "accounts".into(),
                instance_id: None,
            },
        ],
        vec![account_id],
    ))
}

/// 按稳定 ID 移除本机保存的账号。
#[tauri::command]
pub fn forget_account(
    app: AppHandle,
    backend: State<'_, Backend>,
    account_id: String,
) -> Result<SavedInfo, ApiError> {
    let (state, avatar) = {
        let mut data = backend.data.lock().unwrap();
        let fallback = data.forget_account(&account_id)?;
        let avatar = avatar_for(&fallback.player, &fallback.kind);
        data.avatar = avatar.clone();
        (
            data.mutating_snapshot(&[StateResource::Overview, StateResource::Accounts]),
            avatar,
        )
    };
    drop(state);
    emit(&app, EVENT_AVATAR, avatar);
    Ok(SavedInfo::changed(
        "账号已从本机移除。",
        vec![
            ResourceKeyInfo {
                kind: "overview".into(),
                instance_id: None,
            },
            ResourceKeyInfo {
                kind: "accounts".into(),
                instance_id: None,
            },
        ],
        vec![account_id],
    ))
}

/// 设置或清除离线皮肤；空 `path` 表示清除。
#[tauri::command]
#[allow(unused_variables)]
pub fn set_offline_skin(
    app: AppHandle,
    backend: State<'_, Backend>,
    path: String,
) -> Result<AvatarInfo, ApiError> {
    Err(ApiError::unsupported("set_offline_skin"))
}

/// 查询登录会话。
#[tauri::command]
#[allow(unused_variables)]
pub fn get_login_session(auth_session_id: String) -> Result<LoginSessionInfo, ApiError> {
    Err(ApiError::unsupported("get_login_session"))
}

/// 为登录会话选择角色。
#[tauri::command]
#[allow(unused_variables)]
pub fn select_login_profile(
    auth_session_id: String,
    profile_id: String,
) -> Result<LoginSessionInfo, ApiError> {
    Err(ApiError::unsupported("select_login_profile"))
}

/// 取消登录会话。
#[tauri::command]
#[allow(unused_variables)]
pub fn cancel_login(auth_session_id: String) -> Result<LoginSessionInfo, ApiError> {
    Err(ApiError::unsupported("cancel_login"))
}

// ---------------------------------------------------------------- 设置

fn select_game_root(config: &mut LauncherConfig, path: String) -> Result<InstanceCatalogInfo, ApiError> {
    let root = Path::new(&path);
    let saved_root = config
        .game_roots
        .iter()
        .find(|entry| Path::new(entry) == root);
    let path = if let Some(saved_root) = saved_root {
        saved_root.clone()
    } else {
        if !root.is_absolute() || !root.is_dir() {
            return Err(ApiError::validation(
                "无法添加游戏目录。",
                "root",
                "请输入一个已存在的游戏根目录绝对路径。",
            ));
        }
        let canonical = root.canonicalize().map_err(|error| game_root_error(root, error))?;
        config
            .game_roots
            .iter()
            .find(|entry| {
                Path::new(entry)
                    .canonicalize()
                    .is_ok_and(|entry| entry == canonical)
            })
            .cloned()
            .unwrap_or(path)
    };
    let root = Path::new(&path);
    let catalog = local_instances::scan(root).map_err(|error| game_root_error(root, error))?;
    config.select_root(path);
    Ok(catalog)
}

fn game_root_error(root: &Path, error: std::io::Error) -> ApiError {
    ApiError {
        code: "IO_ERROR".into(),
        message: format!("无法读取游戏目录（{}）：{error}", root.display()),
        retryable: true,
        field_errors: HashMap::new(),
    }
}

/// 校验并持久化启动设置补丁。
#[tauri::command]
pub fn save_settings(
    app: AppHandle,
    backend: State<'_, Backend>,
    patch: AppSettingsPatch,
    expected_revision: u64,
) -> Result<SavedInfo, ApiError> {
    if let Some(ram) = patch.ram {
        if !(512..=65536).contains(&ram) {
            return Err(ApiError::validation(
                "设置校验失败。",
                "ram",
                "内存必须在 512–65536 MB 之间。",
            ));
        }
    }
    if let Some(width) = patch.width {
        if !(640..=7680).contains(&width) {
            return Err(ApiError::validation(
                "设置校验失败。",
                "width",
                "窗口宽度必须在 640–7680 之间。",
            ));
        }
    }
    if let Some(height) = patch.height {
        if !(480..=4320).contains(&height) {
            return Err(ApiError::validation(
                "设置校验失败。",
                "height",
                "窗口高度必须在 480–4320 之间。",
            ));
        }
    }
    let mut changed = vec![ResourceKeyInfo {
        kind: "settings".into(),
        instance_id: None,
    }];
    let account_id = {
        let mut data = backend.data.lock().unwrap();
        data.ensure_settings_revision(expected_revision)?;
        let mut config = data.state.config.clone();
        let catalog = patch
            .root
            .filter(|value| !value.is_empty())
            .map(|root| select_game_root(&mut config, root))
            .transpose()?;
        if let Some(java) = patch.java {
            config.java = java;
        }
        if let Some(ram) = patch.ram {
            config.ram = ram as i32;
        }
        if let Some(width) = patch.width {
            config.width = width as i32;
        }
        if let Some(height) = patch.height {
            config.height = height as i32;
        }
        if let Some(value) = patch.auto_memory {
            config.auto_memory = value;
        }
        if let Some(value) = patch.auto_chinese {
            config.auto_chinese = value;
        }
        if let Some(value) = patch.anonymous {
            config.anonymous = value;
        }
        backend.save_config(&mut data, config, expected_revision)?;
        if let Some(catalog) = catalog {
            data.replace_instance_catalog(catalog);
            changed.extend(["instances", "overview"].map(|kind| ResourceKeyInfo {
                kind: kind.into(),
                instance_id: None,
            }));
            changed.push(ResourceKeyInfo {
                kind: "instanceMods".into(),
                instance_id: data.state.selected_instance_id.clone(),
            });
        }
        let state = &mut data.state;
        let account_id = patch.player.filter(|value| !value.is_empty()).and_then(|player| {
            let account = state.accounts.iter_mut().find(|account| {
                state.active_account_id.as_deref() == Some(account.id.as_str())
                    && account.kind == "offline"
                    && account.player != player
            })?;
            account.player = player;
            Some(account.id.clone())
        });
        if account_id.is_some() {
            data.touch(StateResource::Accounts);
        }
        account_id
    };
    if account_id.is_some() {
        changed.extend(["accounts", "avatar"].map(|kind| ResourceKeyInfo {
            kind: kind.into(),
            instance_id: None,
        }));
    }
    Ok(SavedInfo::changed(
        "设置已保存。",
        changed,
        account_id.into_iter().collect(),
    ))
}

/// 扫描本机可用的 Java 运行时。
#[tauri::command]
#[allow(unused_variables)]
pub fn scan_java(backend: State<'_, Backend>) -> Result<Vec<JavaInfo>, ApiError> {
    Err(ApiError::unsupported("scan_java"))
}

/// 保存自定义数据源地址，可同时启用。
#[tauri::command]
pub fn save_custom_sources(
    app: AppHandle,
    backend: State<'_, Backend>,
    sources: DataSourceSet,
    expected_revision: u64,
    activate: Option<bool>,
) -> Result<SavedInfo, ApiError> {
    let activate = activate.unwrap_or(false);
    {
        let mut data = backend.data.lock().unwrap();
        let mut config = data.state.config.clone();
        config.custom_data_sources = sources;
        if activate {
            config.data_source_mode = "custom".into();
        }
        backend.save_config(&mut data, config, expected_revision)?;
    }
    Ok(SavedInfo::changed(
        if activate {
            "已保存并切换到自定义数据源。"
        } else {
            "已保存自定义数据源。"
        },
        vec![ResourceKeyInfo {
            kind: "settings".into(),
            instance_id: None,
        }],
        Vec::new(),
    ))
}

/// 切换默认或自定义数据源。
#[tauri::command]
pub fn use_data_sources(
    app: AppHandle,
    backend: State<'_, Backend>,
    mode: String,
    expected_revision: u64,
) -> Result<SavedInfo, ApiError> {
    if !matches!(mode.as_str(), "default" | "custom") {
        return Err(ApiError::validation("切换数据源失败。", "mode", "未知的数据源模式。"));
    }
    {
        let mut data = backend.data.lock().unwrap();
        let mut config = data.state.config.clone();
        config.data_source_mode = mode.clone();
        backend.save_config(&mut data, config, expected_revision)?;
    }
    let message = if mode == "custom" {
        "已切换为自定义数据源。"
    } else {
        "已恢复默认数据源。"
    };
    Ok(SavedInfo::changed(
        message,
        vec![ResourceKeyInfo {
            kind: "settings".into(),
            instance_id: None,
        }],
        Vec::new(),
    ))
}

/// 测试数据源地址是否可访问。
#[tauri::command]
#[allow(unused_variables)]
pub fn test_data_source(name: String, url: String) -> Result<TestResultInfo, ApiError> {
    Err(ApiError::unsupported("test_data_source"))
}

/// 恢复默认窗口尺寸。
#[tauri::command]
#[allow(unused_variables)]
pub fn reset_window() -> Result<SavedInfo, ApiError> {
    Err(ApiError::unsupported("reset_window"))
}

/// 打开指定目录（root / config / saves / version）。
#[tauri::command]
#[allow(unused_variables)]
pub fn open_folder(kind: String) -> Result<SavedInfo, ApiError> {
    Err(ApiError::unsupported("open_folder"))
}

/// 读取启动器运行日志。
#[tauri::command]
pub fn read_log(backend: State<'_, Backend>) -> String {
    backend.data.lock().unwrap().log.clone()
}

/// 读取运行环境信息。
#[tauri::command]
pub fn get_system_info(backend: State<'_, Backend>) -> SystemInfoInfo {
    let root = backend.data.lock().unwrap().state.config.root().to_owned();
    SystemInfoInfo {
        app: "0.22.3".into(),
        dotnet: ".NET 8.0.11".into(),
        webview: "WebView2 130.0.2849.68".into(),
        os: DEVICE_OS.into(),
        root,
        data: backend.config_dir().to_string_lossy().into_owned(),
    }
}

/// 校验社区身份或管理员密钥。
#[tauri::command]
#[allow(unused_variables)]
pub fn verify_key(role: String, key: String) -> Result<KeyResultInfo, ApiError> {
    Err(ApiError::unsupported("verify_key"))
}

// ---------------------------------------------------------------- 存档

/// 列出本机存档备份。
#[tauri::command]
pub fn list_save_backups(backend: State<'_, Backend>) -> Vec<SaveBackupInfo> {
    backend.data.lock().unwrap().backups.clone()
}

/// 立即备份当前实例存档。
#[tauri::command]
#[allow(unused_variables)]
pub fn backup_saves(app: AppHandle, backend: State<'_, Backend>) -> Result<SavedInfo, ApiError> {
    Err(ApiError::unsupported("backup_saves"))
}

/// 从指定备份恢复某个世界。
#[tauri::command]
#[allow(unused_variables)]
pub fn restore_save(id: String, world: String) -> Result<SavedInfo, ApiError> {
    Err(ApiError::unsupported("restore_save"))
}

// ---------------------------------------------------------------- 任务与游戏

/// 列出任务队列与当前进度。
#[tauri::command]
pub fn list_tasks(backend: State<'_, Backend>) -> ResourceSnapshot {
    let data = backend.data.lock().unwrap();
    let tasks: Vec<TaskInfo> = data
        .tasks
        .iter()
        .cloned()
        .map(|mut task| {
            task.allowed_actions = task.actions();
            task
        })
        .collect();
    data.resource_snapshot(data.revisions.tasks, tasks)
}

/// 对任务执行暂停/继续/取消/删除。
#[tauri::command]
pub fn task_action(
    app: AppHandle,
    backend: State<'_, Backend>,
    id: String,
    action: String,
) -> Result<SavedInfo, ApiError> {
    let tasks = {
        let mut data = backend.data.lock().unwrap();
        let task = data
            .tasks
            .iter_mut()
            .find(|task| task.id == id)
            .ok_or_else(|| ApiError {
                code: "NOT_FOUND".into(),
                message: "任务不存在。".into(),
                retryable: false,
                field_errors: HashMap::new(),
            })?;
        task.apply_action(&action)?;
        task.updated_at = OffsetDateTime::now_utc()
            .format(&Rfc3339)
            .unwrap_or_default();
        if action == "delete" {
            data.tasks.retain(|task| task.id != id);
        }
        data.touch(StateResource::Tasks);
        data.tasks.clone()
    };
    emit(&app, EVENT_TASKS, tasks);
    Ok(SavedInfo::changed(
        "任务操作已接受。",
        vec![ResourceKeyInfo {
            kind: "tasks".into(),
            instance_id: None,
        }],
        vec![id],
    ))
}

/// 列出由本次启动器启动、仍在运行的游戏。
#[tauri::command]
pub fn list_game_sessions(backend: State<'_, Backend>) -> ResourceSnapshot {
    let data = backend.data.lock().unwrap();
    data.resource_snapshot(data.revisions.game_sessions, data.games.clone())
}

/// 请求关闭指定游戏进程。
#[tauri::command]
#[allow(unused_variables)]
pub fn stop_game(
    app: AppHandle,
    backend: State<'_, Backend>,
    session_id: String,
) -> Result<SavedInfo, ApiError> {
    Err(ApiError::unsupported("stop_game"))
}

// ---------------------------------------------------------------- 内容

/// 按分类与关键词搜索资源项目。
#[tauri::command]
pub fn search_content(
    backend: State<'_, Backend>,
    category: String,
    query: String,
    game: Option<String>,
    loader: Option<String>,
) -> ContentSearchResult {
    let keyword = query.to_lowercase();
    let items = backend
        .data
        .lock()
        .unwrap()
        .content
        .iter()
        .filter(|item| {
            keyword.is_empty()
                || item.name.to_lowercase().contains(&keyword)
                || item.description.to_lowercase().contains(&keyword)
                || item
                    .tags
                    .iter()
                    .any(|tag| tag.to_lowercase().contains(&keyword))
        })
        .filter(|item| category.is_empty() || item.category == category)
        .filter(|item| match game.as_deref() {
            Some(game) if !game.is_empty() => item.game_versions.iter().any(|entry| entry == game),
            _ => true,
        })
        .filter(|item| match loader.as_deref() {
            Some(loader) if !loader.is_empty() => item.loaders.iter().any(|entry| entry == loader),
            _ => true,
        })
        .cloned()
        .collect();
    ContentSearchResult {
        category,
        request_id: "req-1".into(),
        items,
    }
}

/// 读取项目的兼容版本与安装令牌。
#[tauri::command]
pub fn list_content_versions(
    backend: State<'_, Backend>,
    id: String,
    request_id: Option<String>,
    game: Option<String>,
    loader: Option<String>,
    #[allow(unused_variables)] category: Option<String>,
    #[allow(unused_variables)] instance: Option<String>,
) -> ResourceVersionsInfo {
    let name = backend
        .data
        .lock()
        .unwrap()
        .content
        .iter()
        .find(|item| item.id == id)
        .map(|item| item.name.clone())
        .unwrap_or_else(|| id.clone());
    let versions = ["1.21.1", "1.20.6", "1.20.1"]
        .into_iter()
        .enumerate()
        .map(|(index, version)| ResourceVersionInfo {
            id: format!("{id}-{index}"),
            name: format!("{name} {version}"),
            files: vec![format!("{id}-{version}.jar")],
        })
        .collect();
    ResourceVersionsInfo {
        request_id: request_id.unwrap_or_else(|| id.clone()),
        token: format!("token-{id}"),
        game: game.unwrap_or_else(|| "1.21.1".into()),
        loader: loader.unwrap_or_else(|| "Fabric".into()),
        items: versions,
    }
}

/// 将资源版本加入下载队列。
#[tauri::command]
#[allow(unused_variables)]
pub fn queue_content_version(
    token: String,
    version_id: String,
    #[allow(unused_variables)] mode: Option<String>,
    #[allow(unused_variables)] path: Option<String>,
) -> Result<SavedInfo, ApiError> {
    Err(ApiError::unsupported("queue_content_version"))
}

/// 收藏或取消收藏资源项目。
#[tauri::command]
pub fn toggle_bookmark(
    app: AppHandle,
    backend: State<'_, Backend>,
    id: String,
    name: String,
    description: String,
    kind: String,
    #[allow(unused_variables)] author: Option<String>,
) -> SavedInfo {
    let state = {
        let mut data = backend.data.lock().unwrap();
        let exists = data.state.favorites.iter().any(|item| item.id == id);
        if exists {
            data.state.favorites.retain(|item| item.id != id);
        } else {
            data.state.favorites.push(FavoriteInfo {
                id: id.clone(),
                kind,
                name: name.clone(),
                description,
            });
        }
        data.mutating_snapshot(&[StateResource::Bookmarks])
    };
    let _ = &state;
    if state.favorites.iter().any(|item| item.name == name) {
        SavedInfo::changed(
            format!("已收藏 {name}。"),
            vec![ResourceKeyInfo {
                kind: "bookmarks".into(),
                instance_id: None,
            }],
            vec![id],
        )
    } else {
        SavedInfo::changed(
            format!("已取消收藏 {name}。"),
            vec![ResourceKeyInfo {
                kind: "bookmarks".into(),
                instance_id: None,
            }],
            vec![id],
        )
    }
}

/// 读取原版版本清单。
#[tauri::command]
pub fn list_vanilla_versions(backend: State<'_, Backend>) -> Vec<VanillaVersionInfo> {
    backend.data.lock().unwrap().vanilla.clone()
}

/// 安装指定原版版本。
#[tauri::command]
#[allow(unused_variables)]
pub fn install_vanilla(minecraft_version: String) -> Result<SavedInfo, ApiError> {
    Err(ApiError::unsupported("install_vanilla"))
}

/// 读取指定游戏版本可用的加载器版本。
#[tauri::command]
pub fn list_loaders(minecraft_version: String, loader: String) -> LoaderListInfo {
    let versions = match loader.to_lowercase().as_str() {
        "quilt" => vec!["0.24.0", "0.23.2", "0.23.1"],
        "forge" => vec!["47.2.0", "47.1.3", "47.0.19"],
        "neoforge" => vec!["21.1.72", "21.1.66", "21.1.59"],
        _ => vec!["0.16.9", "0.16.8", "0.16.5", "0.15.11"],
    };
    LoaderListInfo {
        minecraft_version,
        loader,
        versions: versions.into_iter().map(String::from).collect(),
    }
}

/// 安装指定加载器版本。
#[tauri::command]
#[allow(unused_variables)]
pub fn install_loader(
    minecraft_version: String,
    loader: String,
    loader_version: String,
) -> Result<SavedInfo, ApiError> {
    Err(ApiError::unsupported("install_loader"))
}

/// 读取整合包预览信息。
#[tauri::command]
#[allow(unused_variables)]
pub fn preview_pack(
    backend: State<'_, Backend>,
    path: String,
) -> Result<PackPreviewInfo, ApiError> {
    Err(ApiError::unsupported("preview_pack"))
}

/// 确认导入整合包到指定目录。
#[tauri::command]
#[allow(unused_variables)]
pub fn confirm_pack(
    plan_id: String,
    name: String,
    directory: String,
) -> Result<SavedInfo, ApiError> {
    Err(ApiError::unsupported("confirm_pack"))
}

// ---------------------------------------------------------------- 实例维护

/// 生成绑定实例修订号且十分钟有效的卸载计划。
#[tauri::command]
pub fn preview_uninstall(
    backend: State<'_, Backend>,
    instance_id: String,
) -> Result<UninstallPreviewInfo, ApiError> {
    backend
        .data
        .lock()
        .unwrap()
        .create_uninstall_plan(&instance_id, Duration::minutes(10))
}

/// 校验卸载计划；演示后端不执行文件删除。
#[tauri::command]
pub fn apply_uninstall(
    backend: State<'_, Backend>,
    plan_id: String,
) -> Result<SavedInfo, ApiError> {
    let data = backend.data.lock().unwrap();
    data.validate_plan(&plan_id, "uninstall", data.revisions.instances)?;
    Err(ApiError::unsupported("apply_uninstall"))
}

/// 克隆实例，可选择包含存档与服务器列表。
#[tauri::command]
#[allow(unused_variables)]
pub fn clone_instance(
    instance_id: String,
    display_name: String,
    #[allow(unused_variables)] saves: Option<bool>,
    #[allow(unused_variables)] servers: Option<bool>,
) -> Result<SavedInfo, ApiError> {
    Err(ApiError::unsupported("clone_instance"))
}

/// 将实例导出为整合包 ZIP。
#[tauri::command]
#[allow(unused_variables)]
pub fn export_instance(
    instance_id: String,
    path: String,
    #[allow(unused_variables)] servers: Option<bool>,
    #[allow(unused_variables)] saves: Option<bool>,
) -> Result<SavedInfo, ApiError> {
    Err(ApiError::unsupported("export_instance"))
}

/// 检查实例中由启动器管理的内容更新。
#[tauri::command]
#[allow(unused_variables)]
pub fn check_updates(
    backend: State<'_, Backend>,
    instance_id: String,
) -> Result<ContentUpdatesInfo, ApiError> {
    Err(ApiError::unsupported("check_updates"))
}

/// 为所选项目生成更新预览。
#[tauri::command]
#[allow(unused_variables)]
pub fn preview_updates(
    backend: State<'_, Backend>,
    check_id: String,
    #[allow(unused_variables)] projects: Option<Vec<String>>,
) -> Result<ContentUpdatePreviewInfo, ApiError> {
    Err(ApiError::unsupported("preview_updates"))
}

/// 按预览结果把更新加入队列。
#[tauri::command]
#[allow(unused_variables)]
pub fn apply_updates(plan_id: String) -> Result<SavedInfo, ApiError> {
    Err(ApiError::unsupported("apply_updates"))
}

/// 启用或禁用模组。
#[tauri::command]
pub fn toggle_mod(
    app: AppHandle,
    backend: State<'_, Backend>,
    instance_id: String,
    mod_id: String,
    enabled: bool,
) -> Result<SavedInfo, ApiError> {
    let state = {
        let mut data = backend.data.lock().unwrap();
        if data.state.selected_instance_id.as_deref() != Some(&instance_id) {
            return Err(ApiError {
                code: "CONFLICT".into(),
                message: "请先切换到目标实例。".into(),
                retryable: true,
                field_errors: HashMap::new(),
            });
        }
        let item = data
            .state
            .mods
            .iter_mut()
            .find(|item| item.id == mod_id)
            .ok_or_else(|| ApiError {
                code: "NOT_FOUND".into(),
                message: "模组不存在。".into(),
                retryable: false,
                field_errors: HashMap::new(),
            })?;
        item.enabled = enabled;
        data.mutating_snapshot(&[StateResource::InstanceMods])
    };
    drop(state);
    Ok(SavedInfo::changed(
        if enabled {
            "模组已启用。"
        } else {
            "模组已禁用。"
        },
        vec![ResourceKeyInfo {
            kind: "instanceMods".into(),
            instance_id: Some(instance_id),
        }],
        vec![mod_id],
    ))
}

/// 删除模组文件。
#[tauri::command]
pub fn delete_mod(
    app: AppHandle,
    backend: State<'_, Backend>,
    instance_id: String,
    mod_id: String,
) -> Result<SavedInfo, ApiError> {
    let state = {
        let mut data = backend.data.lock().unwrap();
        if data.state.selected_instance_id.as_deref() != Some(&instance_id) {
            return Err(ApiError {
                code: "CONFLICT".into(),
                message: "请先切换到目标实例。".into(),
                retryable: true,
                field_errors: HashMap::new(),
            });
        }
        let before = data.state.mods.len();
        data.state.mods.retain(|item| item.id != mod_id);
        if data.state.mods.len() == before {
            return Err(ApiError {
                code: "NOT_FOUND".into(),
                message: "模组不存在。".into(),
                retryable: false,
                field_errors: HashMap::new(),
            });
        }
        data.mutating_snapshot(&[StateResource::InstanceMods])
    };
    drop(state);
    Ok(SavedInfo::changed(
        "模组已删除。",
        vec![ResourceKeyInfo {
            kind: "instanceMods".into(),
            instance_id: Some(instance_id),
        }],
        vec![mod_id],
    ))
}

/// 读取实例启动设置与继承状态。
#[tauri::command]
pub fn read_launch_options(
    backend: State<'_, Backend>,
    instance_id: String,
    request_id: Option<String>,
) -> LaunchOptionsInfo {
    let data = backend.data.lock().unwrap();
    let config = &data.state.config;
    LaunchOptionsInfo {
        request_id: request_id.unwrap_or_else(|| instance_id.clone()),
        token: format!("launch-{instance_id}"),
        instance_id: instance_id.clone(),
        display_name: instance_id,
        inherited: false,
        settings: LaunchSettings {
            auto_memory: config.auto_memory,
            full_screen: false,
            ram_mb: config.ram as i64,
            width: config.width as i64,
            height: config.height as i64,
            java: config.java.clone(),
            jvm_arguments: String::new(),
            window_title: "MCUG Launcher".into(),
            server: String::new(),
            auto_chinese: config.auto_chinese,
        },
    }
}

/// 保存实例启动设置。
#[tauri::command]
#[allow(unused_variables)]
pub fn save_launch_options(
    app: AppHandle,
    backend: State<'_, Backend>,
    token: String,
    #[allow(unused_variables)] instance_id: String,
    #[allow(unused_variables)] display_name: String,
    settings: LaunchSettings,
    #[allow(unused_variables)] inherit: Option<bool>,
) -> Result<SavedInfo, ApiError> {
    Err(ApiError::unsupported("save_launch_options"))
}

// ---------------------------------------------------------------- 社区

/// 读取社区公告、推荐与语录。
#[tauri::command]
pub fn get_community(backend: State<'_, Backend>) -> ResourceSnapshot {
    let data = backend.data.lock().unwrap();
    data.resource_snapshot(data.revisions.community, data.community.clone())
}

/// 重新拉取社区内容。
#[tauri::command]
pub fn refresh_community(backend: State<'_, Backend>) -> ResourceSnapshot {
    get_community(backend)
}

/// 覆写本机推荐列表。
#[tauri::command]
pub fn save_recommendations(
    app: AppHandle,
    backend: State<'_, Backend>,
    items: Vec<RecommendationInfo>,
) -> SavedInfo {
    let community = {
        let mut data = backend.data.lock().unwrap();
        data.community.recommendations = items;
        data.touch(StateResource::Community);
        data.community.clone()
    };
    emit(&app, EVENT_COMMUNITY, community);
    SavedInfo::changed(
        "本地内容已更新。",
        vec![ResourceKeyInfo {
            kind: "community".into(),
            instance_id: None,
        }],
        Vec::new(),
    )
}
