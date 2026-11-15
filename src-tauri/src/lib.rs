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
            commands::get_launcher_state,
            commands::select_instance,
            // 启动
            commands::launch_game,
            commands::launch_to_server,
            // 服务器
            commands::save_server,
            commands::delete_server,
            commands::ping_server,
            // 账号
            commands::read_avatar,
            commands::login_microsoft,
            commands::login_mua,
            commands::list_mua_profiles,
            commands::select_account,
            commands::forget_account,
            commands::set_offline_skin,
            commands::select_mua_profile,
            // 设置与系统
            commands::save_settings,
            commands::scan_java,
            commands::save_custom_sources,
            commands::use_data_sources,
            commands::test_data_source,
            commands::reset_window,
            commands::open_folder,
            commands::read_log,
            commands::get_system_info,
            commands::verify_key,
            // 存档
            commands::list_save_backups,
            commands::backup_saves,
            commands::restore_save,
            // 任务与游戏
            commands::list_tasks,
            commands::task_action,
            commands::list_game_sessions,
            commands::stop_game,
            // 内容与版本
            commands::search_content,
            commands::list_content_versions,
            commands::queue_content_version,
            commands::toggle_bookmark,
            commands::list_vanilla_versions,
            commands::install_vanilla,
            commands::list_loaders,
            commands::install_loader,
            commands::preview_pack,
            commands::confirm_pack,
            // 实例维护
            commands::preview_uninstall,
            commands::apply_uninstall,
            commands::clone_instance,
            commands::export_instance,
            commands::check_updates,
            commands::preview_updates,
            commands::apply_updates,
            commands::toggle_mod,
            commands::delete_mod,
            commands::read_launch_options,
            commands::save_launch_options,
            // 社区
            commands::get_community,
            commands::refresh_community,
            commands::save_recommendations,
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
