mod commands;
mod demo;
mod model;
mod state;

use state::Backend;

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_opener::init())
        .manage(Backend::new())
        .invoke_handler(tauri::generate_handler![
            // 状态
            commands::implementation::get_overview,
            commands::implementation::get_settings,
            commands::implementation::list_accounts,
            commands::implementation::list_instances,
            commands::implementation::list_instance_mods,
            commands::implementation::list_servers,
            commands::implementation::list_bookmarks,
            commands::implementation::select_instance,
            // 启动
            commands::implementation::launch_game,
            commands::implementation::launch_to_server,
            // 服务器
            commands::implementation::save_server,
            commands::implementation::delete_server,
            commands::implementation::ping_server,
            // 账号
            commands::implementation::read_avatar,
            commands::implementation::start_login,
            commands::implementation::get_login_session,
            commands::implementation::select_login_profile,
            commands::implementation::cancel_login,
            commands::implementation::select_account,
            commands::implementation::forget_account,
            commands::implementation::set_offline_skin,
            // 设置与系统
            commands::implementation::save_settings,
            commands::implementation::scan_java,
            commands::implementation::save_custom_sources,
            commands::implementation::use_data_sources,
            commands::implementation::test_data_source,
            commands::implementation::reset_window,
            commands::implementation::open_folder,
            commands::implementation::read_log,
            commands::implementation::get_system_info,
            commands::implementation::verify_key,
            // 存档
            commands::implementation::list_save_backups,
            commands::implementation::backup_saves,
            commands::implementation::restore_save,
            // 任务与游戏
            commands::implementation::list_tasks,
            commands::implementation::task_action,
            commands::implementation::list_game_sessions,
            commands::implementation::stop_game,
            // 内容与版本
            commands::implementation::search_content,
            commands::implementation::list_content_versions,
            commands::implementation::queue_content_version,
            commands::implementation::toggle_bookmark,
            commands::implementation::list_vanilla_versions,
            commands::implementation::install_vanilla,
            commands::implementation::list_loaders,
            commands::implementation::install_loader,
            commands::implementation::preview_pack,
            commands::implementation::confirm_pack,
            // 实例维护
            commands::implementation::preview_uninstall,
            commands::implementation::apply_uninstall,
            commands::implementation::clone_instance,
            commands::implementation::export_instance,
            commands::implementation::check_updates,
            commands::implementation::preview_updates,
            commands::implementation::apply_updates,
            commands::implementation::toggle_mod,
            commands::implementation::delete_mod,
            commands::implementation::read_launch_options,
            commands::implementation::save_launch_options,
            // 社区
            commands::implementation::get_community,
            commands::implementation::refresh_community,
            commands::implementation::save_recommendations,
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
