//! 内核自描述：把「这个内核提供什么」变成一条命令。
//!
//! 第三方前端启动后先调 `kernel_manifest`，拿到方法表、API 版本、传输方式、
//! 当前后端能力边界，再决定怎么渲染。方法表与 `lib.rs` 的注册表由测试强绑定，
//! 新增命令却忘了登记会让测试直接失败，避免「文档说有、实际没有」。

use std::collections::BTreeMap;

use serde::Serialize;
use tauri::State;

use crate::{
    frontend::{Frontend, FrontendInfo, FrontendState, KERNEL_API_VERSION, KERNEL_PROTOCOL},
    state::Backend,
};

/// 与 `lib.rs` 中 `generate_handler!` 注册的命令一一对应（snake_case）。
pub const METHODS: &[&str] = &[
    // 状态
    "get_overview",
    "get_settings",
    "list_accounts",
    "list_instances",
    "list_all_instances",
    "list_instance_mods",
    "list_servers",
    "list_bookmarks",
    "select_instance",
    // 启动
    "launch_game",
    "launch_to_server",
    // 服务器
    "save_server",
    "delete_server",
    "ping_server",
    // 账号
    "read_avatar",
    "start_login",
    "get_login_session",
    "select_login_profile",
    "cancel_login",
    "select_account",
    "forget_account",
    "set_offline_skin",
    // 设置与系统
    "save_settings",
    "remove_game_root",
    "scan_java",
    "save_custom_sources",
    "use_data_sources",
    "test_data_source",
    "reset_window",
    "open_folder",
    "read_log",
    "get_system_info",
    "verify_key",
    "pick_file",
    "pick_folder",
    // 存档
    "list_save_backups",
    "backup_saves",
    "restore_save",
    // 任务与游戏
    "list_tasks",
    "task_action",
    "list_game_sessions",
    "stop_game",
    // 内容与版本
    "search_content",
    "list_content_versions",
    "queue_content_version",
    "toggle_bookmark",
    "list_vanilla_versions",
    "install_vanilla",
    "list_loaders",
    "install_loader",
    "preview_pack",
    "confirm_pack",
    // 实例维护
    "preview_uninstall",
    "apply_uninstall",
    "clone_instance",
    "export_instance",
    "check_updates",
    "preview_updates",
    "apply_updates",
    "toggle_mod",
    "delete_mod",
    "read_launch_options",
    "save_launch_options",
    // 社区
    "get_community",
    "refresh_community",
    "save_recommendations",
];

/// 演示后端里明确返回 `UNSUPPORTED` 的命令。真实内核接入后逐条删掉即可，
/// `capabilities` 会随之自动变成 `true`。
const DEMO_UNSUPPORTED: &[&str] = &[
    "launch_game",
    "launch_to_server",
    "ping_server",
    "start_login",
    "set_offline_skin",
    "get_login_session",
    "select_login_profile",
    "cancel_login",
    "scan_java",
    "test_data_source",
    "reset_window",
    "open_folder",
    "verify_key",
    "backup_saves",
    "restore_save",
    "stop_game",
    "queue_content_version",
    "install_vanilla",
    "install_loader",
    "preview_pack",
    "confirm_pack",
    "apply_uninstall",
    "clone_instance",
    "export_instance",
    "check_updates",
    "preview_updates",
    "apply_updates",
    "save_launch_options",
];

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct Limits {
    /// 安装/更新计划的有效期（秒）。
    pub plan_ttl_seconds: u64,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct KernelManifest {
    /// 协议标识，例如 `mcugl.kernel/v1`。
    pub protocol: &'static str,
    pub api_version: &'static str,
    pub app_version: String,
    /// 当前后端实现：`demo` = 演示数据，能力见 `capabilities`。
    pub backend: &'static str,
    pub transports: &'static [&'static str],
    pub methods: &'static [&'static str],
    /// 事件主题，v1 先留空（事件流属 P1，前端只轮询也能工作）。
    pub events: &'static [&'static str],
    /// 方法名 → 当前后端是否真正实现（false = 调用返回 `UNSUPPORTED`）。
    pub capabilities: BTreeMap<&'static str, bool>,
    pub limits: Limits,
    /// 当前挂载的第三方前端；官方打包前端时为 null。
    pub frontend: Option<FrontendInfo>,
}

#[tauri::command]
pub fn kernel_manifest(
    backend: State<'_, Backend>,
    frontend: State<'_, FrontendState>,
) -> KernelManifest {
    let capabilities = METHODS
        .iter()
        .map(|method| (*method, !DEMO_UNSUPPORTED.contains(method)))
        .collect();
    // 与界面显示的产品版本保持一致（demo 数据里是 0.22.3，Cargo 版本只是脚手架版本）。
    let app_version = backend
        .data
        .lock()
        .map(|data| data.state.app_version.clone())
        .unwrap_or_default();
    let current_frontend = frontend.current();
    KernelManifest {
        protocol: KERNEL_PROTOCOL,
        api_version: KERNEL_API_VERSION,
        app_version,
        backend: "demo",
        transports: &["tauri-ipc"],
        methods: METHODS,
        events: &[],
        capabilities,
        limits: Limits {
            plan_ttl_seconds: 600,
        },
        frontend: current_frontend.as_ref().map(Frontend::info),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    /// 内核元命令与宿主自己的前端包管理命令不在对第三方公开的方法表里。
    const NON_KERNEL_COMMANDS: &[&str] = &[
        "kernel_manifest",
        "install_frontend",
        "list_frontends",
        "read_frontend_icon",
        "use_frontend",
        "reload_frontend",
        "remove_frontend",
    ];

    /// 从 `lib.rs` 的 `generate_handler![...]` 里抠出注册的命令名。
    fn registered_commands() -> Vec<&'static str> {
        let source = include_str!("../lib.rs");
        let start = source.find("generate_handler![").expect("找不到 generate_handler");
        let block = &source[start..];
        let mut names = Vec::new();
        for line in block.lines() {
            let line = line.trim();
            if !line.starts_with("commands::") {
                continue;
            }
            let name = line
                .rsplit("::")
                .next()
                .unwrap_or_default()
                .trim_end_matches(',')
                .trim();
            if !name.is_empty() && !NON_KERNEL_COMMANDS.contains(&name) {
                names.push(name);
            }
        }
        names
    }

    #[test]
    fn methods_match_registered_commands() {
        let mut registered = registered_commands();
        registered.sort_unstable();
        let mut declared = METHODS.to_vec();
        declared.sort_unstable();
        assert_eq!(declared, registered, "METHODS 与 lib.rs 注册表不一致");
    }

    #[test]
    fn every_unsupported_method_exists() {
        for method in DEMO_UNSUPPORTED {
            assert!(
                METHODS.contains(method),
                "capabilities 里的 {method} 不在方法表中"
            );
        }
    }

    #[test]
    fn manifest_only_lists_real_methods() {
        assert_eq!(METHODS.len(), 66);
    }
}
