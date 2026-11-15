//! Tauri 命令层：前端唯一的后端入口。
//!
//! 约定：
//! - 命令名使用 snake_case，前端通过 `invoke("命令名", { camelCase 参数 })` 调用；
//! - 命令返回语义化结果（SavedInfo / LaunchResultInfo 等），失败通过 `Err(ApiError)` 抛出；
//! - 状态变更由前端在命令返回后重新调用查询命令获取，事件仅作为可选的推送优化。

use serde::Serialize;
use std::time::{SystemTime, UNIX_EPOCH};
use tauri::{AppHandle, Emitter, State};

use crate::{demo::avatar_for, model::*, state::Backend};

pub const EVENT_STATE: &str = "launcher://state";
pub const EVENT_TASKS: &str = "launcher://tasks";
pub const EVENT_GAMES: &str = "launcher://games";
pub const EVENT_COMMUNITY: &str = "launcher://community";
pub const EVENT_AVATAR: &str = "launcher://avatar";
pub const EVENT_TOAST: &str = "launcher://toast";
#[allow(dead_code)]
pub const EVENT_ERROR: &str = "launcher://error";

fn emit<T: Serialize + Clone>(app: &AppHandle, event: &str, payload: T) {
    let _ = app.emit(event, payload);
}

fn clamp(value: i32, min: i32, max: i32) -> i32 {
    value.min(max).max(min)
}

fn generated_id(prefix: &str) -> String {
    let value = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|duration| duration.as_nanos())
        .unwrap_or_default();
    format!("{prefix}-{value}")
}

// ---------------------------------------------------------------- 状态

/// 返回启动器完整状态快照。
#[tauri::command]
pub fn get_launcher_state(backend: State<'_, Backend>) -> ResourceSnapshot<LauncherState> {
    backend.data.lock().unwrap().versioned_snapshot()
}

/// 切换当前实例并持久化选择。
#[tauri::command]
pub fn select_instance(
    app: AppHandle,
    backend: State<'_, Backend>,
    instance_id: String,
) -> Result<SavedInfo, ApiError> {
    let state = {
        let mut data = backend.data.lock().unwrap();
        if !data.state.instances.iter().any(|item| item.id == instance_id) {
            return Err(ApiError {
                code: "NOT_FOUND",
                message: "实例不存在。".into(),
                retryable: false,
            });
        }
        data.state.selected_instance_id = Some(instance_id.clone());
        data.mutating_snapshot()
    };
    emit(&app, EVENT_STATE, state);
    Ok(SavedInfo::new("当前实例已切换。"))
}

// ---------------------------------------------------------------- 启动

/// 启动指定版本；内核重写后接入真实启动流程。
#[tauri::command]
#[allow(unused_variables)]
pub fn launch_game(instance_id: String) -> Result<LaunchResultInfo, ApiError> {
    Err(ApiError::unsupported("launch_game"))
}

/// 使用指定实例直连服务器启动。
#[tauri::command]
#[allow(unused_variables)]
pub fn launch_to_server(instance_id: String, server_address: String) -> Result<LaunchResultInfo, ApiError> {
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
) -> Result<ServerResultInfo, ApiError> {
    let mut message = "服务器已添加。".to_string();
    let mut ok = true;
    let state = {
        let mut data = backend.data.lock().unwrap();
        if let Some(id) = id.filter(|value| !value.is_empty()) {
            if let Some(server) = data
                .state
                .servers
                .iter_mut()
                .find(|item| item.id == id)
            {
                server.name = name.clone();
                server.address = address.clone();
                server.instance_id = instance_id.clone();
                message = "服务器已更新。".to_string();
            } else {
                return Err(ApiError { code: "NOT_FOUND", message: "服务器收藏不存在。".into(), retryable: false });
            }
        } else if data
            .state
            .servers
            .iter()
            .any(|item| item.address == address)
        {
            ok = false;
            message = "这个地址已经在收藏里了。".to_string();
        } else {
            data.state.servers.push(SavedServerInfo {
                id: generated_id("server"),
                name,
                address,
                instance_id,
            });
        }
        data.mutating_snapshot()
    };
    if ok {
        emit(&app, EVENT_STATE, state);
    }
    Ok(ServerResultInfo { ok, message })
}

/// 删除服务器收藏。
#[tauri::command]
pub fn delete_server(app: AppHandle, backend: State<'_, Backend>, id: String) -> Result<SavedInfo, ApiError> {
    let state = {
        let mut data = backend.data.lock().unwrap();
        let before = data.state.servers.len();
        data.state.servers.retain(|item| item.id != id);
        if data.state.servers.len() == before {
            return Err(ApiError { code: "NOT_FOUND", message: "服务器收藏不存在。".into(), retryable: false });
        }
        data.mutating_snapshot()
    };
    emit(&app, EVENT_STATE, state);
    Ok(SavedInfo::new("服务器已删除。"))
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

/// 执行微软账号登录。
#[tauri::command]
#[allow(unused_variables)]
pub fn login_microsoft(app: AppHandle, backend: State<'_, Backend>) -> Result<AuthResultInfo, ApiError> {
    Err(ApiError::unsupported("login_microsoft"))
}

/// 使用账号密码登录 MUA。
#[tauri::command]
#[allow(unused_variables)]
pub fn login_mua(
    app: AppHandle,
    backend: State<'_, Backend>,
    account: String,
    _password: String,
) -> Result<AuthResultInfo, ApiError> {
    Err(ApiError::unsupported("login_mua"))
}

/// 返回 MUA 登录后的可选角色。
#[tauri::command]
pub fn list_mua_profiles() -> Vec<MuaProfileInfo> {
    vec![
        MuaProfileInfo {
            id: "p1".into(),
            name: "MuaTester".into(),
        },
        MuaProfileInfo {
            id: "p2".into(),
            name: "OtherTester".into(),
        },
    ]
}

/// 切换账号模式（microsoft / mua / offline）。
#[tauri::command]
pub fn switch_account_mode(
    app: AppHandle,
    backend: State<'_, Backend>,
    mode: String,
) -> AccountSwitchResultInfo {
    let (state, avatar) = {
        let mut data = backend.data.lock().unwrap();
        data.state.account_type = mode.clone();
        let avatar = avatar_for(&data.state.player, &mode);
        data.avatar = avatar.clone();
        (data.mutating_snapshot(), avatar)
    };
    emit(&app, EVENT_STATE, state);
    emit(&app, EVENT_AVATAR, avatar);
    AccountSwitchResultInfo {
        account: mode.clone(),
        needs_login: false,
        message: format!("已切换到 {mode} 账号。"),
    }
}

/// 移除本机保存的指定账号。
#[tauri::command]
pub fn forget_account(app: AppHandle, backend: State<'_, Backend>, mode: String) -> AuthResultInfo {
    let (state, avatar) = {
        let mut data = backend.data.lock().unwrap();
        data.state.account_type = "offline".into();
        let avatar = avatar_for(&data.state.player, "offline");
        data.avatar = avatar.clone();
        (data.mutating_snapshot(), avatar)
    };
    emit(&app, EVENT_STATE, state);
    emit(&app, EVENT_AVATAR, avatar);
    AuthResultInfo {
        ok: Some(true),
        pending: false,
        message: format!("已移除 {mode} 账号。"),
    }
}

/// 设置或清除离线皮肤；空 `path` 表示清除。
#[tauri::command]
#[allow(unused_variables)]
pub fn set_offline_skin(app: AppHandle, backend: State<'_, Backend>, path: String) -> Result<AvatarInfo, ApiError> {
    Err(ApiError::unsupported("set_offline_skin"))
}

/// 选择 MUA 角色完成登录。
#[tauri::command]
#[allow(unused_variables)]
pub fn select_mua_profile(_token: String, profile_id: String) -> Result<SavedInfo, ApiError> {
    Err(ApiError::unsupported("select_mua_profile"))
}

// ---------------------------------------------------------------- 设置

/// 按补丁更新启动设置并钳制数值范围。
#[tauri::command]
pub fn save_settings(
    app: AppHandle,
    backend: State<'_, Backend>,
    patch: AppSettingsPatch,
    expected_revision: u64,
) -> Result<SavedInfo, ApiError> {
    let state = {
        let mut data = backend.data.lock().unwrap();
        data.ensure_state_revision(expected_revision)?;
        let state = &mut data.state;
        if let Some(root) = patch.root.filter(|value| !value.is_empty()) {
            if root != state.root {
                state.game_roots.push(state.root.clone());
                state.game_roots.push(root.clone());
                state.game_roots.sort();
                state.game_roots.dedup();
                state.root = root;
            }
        }
        if let Some(java) = patch.java {
            state.java = java;
        }
        if let Some(ram) = patch.ram {
            state.ram = clamp(ram, 512, 65536);
        }
        if let Some(width) = patch.width {
            state.width = clamp(width, 640, 7680);
        }
        if let Some(height) = patch.height {
            state.height = clamp(height, 480, 4320);
        }
        if let Some(value) = patch.auto_memory {
            state.auto_memory = value;
        }
        if let Some(value) = patch.auto_chinese {
            state.auto_chinese = value;
        }
        if let Some(value) = patch.anonymous {
            state.anonymous = value;
        }
        if let Some(player) = patch.player.filter(|value| !value.is_empty()) {
            if state.account_type == "offline" {
                state.player = player;
            }
        }
        data.mutating_snapshot()
    };
    emit(&app, EVENT_STATE, state);
    Ok(SavedInfo::new("设置已保存。"))
}

/// 扫描本机可用的 Java 运行时。
#[tauri::command]
#[allow(unused_variables)]
pub fn scan_java(backend: State<'_, Backend>) -> Result<Vec<JavaInfo>, ApiError> {
    Err(ApiError::unsupported("scan_java"))
}

/// 保存自定义数据源地址。
#[tauri::command]
pub fn save_custom_sources(
    app: AppHandle,
    backend: State<'_, Backend>,
    sources: DataSourceSet,
) -> SavedInfo {
    let state = {
        let mut data = backend.data.lock().unwrap();
        data.state.custom_data_sources = sources;
        data.mutating_snapshot()
    };
    emit(&app, EVENT_STATE, state);
    SavedInfo::new("已保存自定义数据源，可点击「使用自定义」切换。")
}

/// 切换默认或自定义数据源。
#[tauri::command]
pub fn use_data_sources(app: AppHandle, backend: State<'_, Backend>, mode: String) -> SavedInfo {
    let state = {
        let mut data = backend.data.lock().unwrap();
        data.state.data_source_mode = mode.clone();
        data.mutating_snapshot()
    };
    emit(&app, EVENT_STATE, state);
    let message = if mode == "custom" {
        "已切换为自定义数据源。"
    } else {
        "已恢复默认数据源。"
    };
    SavedInfo::new(message)
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
    let root = backend.data.lock().unwrap().state.root.clone();
    SystemInfoInfo {
        app: "0.22.3".into(),
        dotnet: ".NET 8.0.11".into(),
        webview: "WebView2 130.0.2849.68".into(),
        os: "Windows 11 23H2 (22631.4460)".into(),
        root,
        data: "C:/Users/XiaoMu/AppData/Roaming/MCUGLauncher".into(),
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
pub fn list_tasks(backend: State<'_, Backend>) -> Vec<TaskInfo> {
    backend.data.lock().unwrap().tasks.iter().cloned().map(|mut task| { task.allowed_actions = task.actions(); task }).collect()
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
        let task = data.tasks.iter_mut().find(|task| task.id == id)
            .ok_or_else(|| ApiError { code: "NOT_FOUND", message: "任务不存在。".into(), retryable: false })?;
        task.apply_action(&action)?;
        if action == "delete" { data.tasks.retain(|task| task.id != id); }
        data.tasks.clone()
    };
    emit(&app, EVENT_TASKS, tasks);
    Ok(SavedInfo::new("任务操作已接受。"))
}

/// 列出由本次启动器启动、仍在运行的游戏。
#[tauri::command]
pub fn list_games(backend: State<'_, Backend>) -> Vec<GameInfo> {
    backend.data.lock().unwrap().games.clone()
}

/// 请求关闭指定游戏进程。
#[tauri::command]
#[allow(unused_variables)]
pub fn stop_game(app: AppHandle, backend: State<'_, Backend>, id: String) -> Result<SavedInfo, ApiError> {
    Err(ApiError::unsupported("stop_game"))
}

// ---------------------------------------------------------------- 内容

/// 按分类与关键词搜索资源项目。
#[tauri::command]
pub fn search_content(
    backend: State<'_, Backend>,
    category: String,
    query: String,
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
        .cloned()
        .collect();
    ContentSearchResult {
        category,
        request_id: "req-1".into(),
        items,
        error: String::new(),
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
) -> ResourceVersionsResult {
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
    ResourceVersionsResult {
        ok: true,
        result: Some(ResourceVersionsInfo {
            request_id: request_id.unwrap_or_else(|| id.clone()),
            token: format!("token-{id}"),
            game: game.unwrap_or_else(|| "1.21.1".into()),
            loader: loader.unwrap_or_else(|| "Fabric".into()),
            items: versions,
        }),
        error: String::new(),
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
                id,
                kind,
                name: name.clone(),
                description,
            });
        }
        data.mutating_snapshot()
    };
    emit(&app, EVENT_STATE, &state);
    if state.favorites.iter().any(|item| item.name == name) {
        SavedInfo::new(format!("已收藏 {name}。"))
    } else {
        SavedInfo::new(format!("已取消收藏 {name}。"))
    }
}

/// 读取原版版本清单。
#[tauri::command]
pub fn list_vanilla_versions(backend: State<'_, Backend>) -> VanillaVersionsResult {
    VanillaVersionsResult {
        ok: true,
        items: backend.data.lock().unwrap().vanilla.clone(),
        error: String::new(),
    }
}

/// 安装指定原版版本。
#[tauri::command]
#[allow(unused_variables)]
pub fn install_vanilla(version: String) -> Result<SavedInfo, ApiError> {
    Err(ApiError::unsupported("install_vanilla"))
}

/// 读取指定游戏版本可用的加载器版本。
#[tauri::command]
pub fn list_loaders(version: String, loader: String) -> LoaderListResult {
    let versions = match loader.to_lowercase().as_str() {
        "quilt" => vec!["0.24.0", "0.23.2", "0.23.1"],
        "forge" => vec!["47.2.0", "47.1.3", "47.0.19"],
        "neoforge" => vec!["21.1.72", "21.1.66", "21.1.59"],
        _ => vec!["0.16.9", "0.16.8", "0.16.5", "0.15.11"],
    };
    LoaderListResult {
        ok: true,
        result: Some(LoaderListInfo {
            version,
            loader,
            versions: versions.into_iter().map(String::from).collect(),
        }),
        error: String::new(),
    }
}

/// 安装指定加载器版本。
#[tauri::command]
#[allow(unused_variables)]
pub fn install_loader(version: String, loader: String, loader_version: String) -> Result<SavedInfo, ApiError> {
    Err(ApiError::unsupported("install_loader"))
}

/// 读取整合包预览信息。
#[tauri::command]
#[allow(unused_variables)]
pub fn preview_pack(backend: State<'_, Backend>, path: String) -> Result<PackPreviewInfo, ApiError> {
    Err(ApiError::unsupported("preview_pack"))
}

/// 确认导入整合包到指定目录。
#[tauri::command]
#[allow(unused_variables)]
pub fn confirm_pack(token: String, name: String, directory: String) -> Result<SavedInfo, ApiError> {
    Err(ApiError::unsupported("confirm_pack"))
}

// ---------------------------------------------------------------- 实例维护

/// 生成卸载预览与确认令牌。
#[tauri::command]
#[allow(unused_variables)]
pub fn preview_uninstall(version: String) -> Result<UninstallPreviewInfo, ApiError> {
    Err(ApiError::unsupported("preview_uninstall"))
}

/// 按预览令牌执行卸载。
#[tauri::command]
#[allow(unused_variables)]
pub fn apply_uninstall(token: String) -> Result<SavedInfo, ApiError> {
    Err(ApiError::unsupported("apply_uninstall"))
}

/// 克隆实例，可选择包含存档与服务器列表。
#[tauri::command]
#[allow(unused_variables)]
pub fn clone_instance(
    version: String,
    name: String,
    #[allow(unused_variables)] saves: Option<bool>,
    #[allow(unused_variables)] servers: Option<bool>,
) -> Result<SavedInfo, ApiError> {
    Err(ApiError::unsupported("clone_instance"))
}

/// 将实例导出为整合包 ZIP。
#[tauri::command]
#[allow(unused_variables)]
pub fn export_instance(
    version: String,
    path: String,
    #[allow(unused_variables)] servers: Option<bool>,
    #[allow(unused_variables)] saves: Option<bool>,
) -> Result<SavedInfo, ApiError> {
    Err(ApiError::unsupported("export_instance"))
}

/// 检查实例中由启动器管理的内容更新。
#[tauri::command]
#[allow(unused_variables)]
pub fn check_updates(backend: State<'_, Backend>, version: String) -> Result<ContentUpdatesInfo, ApiError> {
    Err(ApiError::unsupported("check_updates"))
}

/// 为所选项目生成更新预览。
#[tauri::command]
#[allow(unused_variables)]
pub fn preview_updates(
    backend: State<'_, Backend>,
    token: String,
    #[allow(unused_variables)] projects: Option<Vec<String>>,
) -> Result<ContentUpdatePreviewInfo, ApiError> {
    Err(ApiError::unsupported("preview_updates"))
}

/// 按预览结果把更新加入队列。
#[tauri::command]
#[allow(unused_variables)]
pub fn apply_updates(token: String) -> Result<SavedInfo, ApiError> {
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
            return Err(ApiError { code: "CONFLICT", message: "请先切换到目标实例。".into(), retryable: true });
        }
        let item = data.state.mods.iter_mut().find(|item| item.id == mod_id)
            .ok_or_else(|| ApiError { code: "NOT_FOUND", message: "模组不存在。".into(), retryable: false })?;
        item.enabled = enabled;
        data.mutating_snapshot()
    };
    emit(&app, EVENT_STATE, state);
    Ok(SavedInfo::new(if enabled {
        "模组已启用。"
    } else {
        "模组已禁用。"
    }))
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
            return Err(ApiError { code: "CONFLICT", message: "请先切换到目标实例。".into(), retryable: true });
        }
        let before = data.state.mods.len();
        data.state.mods.retain(|item| item.id != mod_id);
        if data.state.mods.len() == before {
            return Err(ApiError { code: "NOT_FOUND", message: "模组不存在。".into(), retryable: false });
        }
        data.mutating_snapshot()
    };
    emit(&app, EVENT_STATE, state);
    Ok(SavedInfo::new("模组已删除。"))
}

/// 读取实例启动设置与继承状态。
#[tauri::command]
pub fn read_launch_options(
    backend: State<'_, Backend>,
    version: String,
    request_id: Option<String>,
) -> LaunchOptionsInfo {
    let data = backend.data.lock().unwrap();
    LaunchOptionsInfo {
        request_id: request_id.unwrap_or_else(|| version.clone()),
        token: format!("launch-{version}"),
        version: version.clone(),
        name: version,
        inherited: false,
        settings: LaunchSettings {
            auto_memory: data.state.auto_memory,
            full_screen: false,
            ram_mb: data.state.ram,
            width: data.state.width,
            height: data.state.height,
            java: data.state.java.clone(),
            jvm_arguments: String::new(),
            window_title: "MCUG Launcher".into(),
            server: String::new(),
            auto_chinese: data.state.auto_chinese,
        },
    }
}

/// 保存实例启动设置。
#[tauri::command]
pub fn save_launch_options(
    app: AppHandle,
    backend: State<'_, Backend>,
    token: String,
    #[allow(unused_variables)] version: String,
    #[allow(unused_variables)] name: String,
    settings: LaunchSettings,
    #[allow(unused_variables)] inherit: Option<bool>,
) -> SavedInfo {
    let _ = token;
    let state = {
        let mut data = backend.data.lock().unwrap();
        data.state.ram = settings.ram_mb;
        data.state.width = settings.width;
        data.state.height = settings.height;
        data.state.java = settings.java;
        data.mutating_snapshot()
    };
    emit(&app, EVENT_STATE, state);
    SavedInfo::new("启动选项已保存。")
}

// ---------------------------------------------------------------- 社区

/// 读取社区公告、推荐与语录。
#[tauri::command]
pub fn get_community(backend: State<'_, Backend>) -> CommunityData {
    backend.data.lock().unwrap().community.clone()
}

/// 重新拉取社区内容。
#[tauri::command]
pub fn refresh_community(backend: State<'_, Backend>) -> CommunityData {
    backend.data.lock().unwrap().community.clone()
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
        data.community.clone()
    };
    emit(&app, EVENT_COMMUNITY, community);
    SavedInfo::new("本地内容已更新。")
}
