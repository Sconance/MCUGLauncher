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
            commands::get_launcher_state,
            commands::select_instance,
            commands::select_version,
            commands::launch_game,
            commands::launch_to_server,
            commands::save_server,
            commands::delete_server,
            commands::ping_server,
            commands::read_avatar,
            commands::login_microsoft,
            commands::login_mua,
            commands::list_mua_profiles,
            commands::switch_account_mode,
            commands::forget_account,
            commands::set_offline_skin,
            commands::select_mua_profile,
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
            commands::list_save_backups,
            commands::backup_saves,
            commands::restore_save,
            commands::list_tasks,
            commands::task_action,
            commands::list_games,
            commands::stop_game,
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
            commands::get_community,
            commands::refresh_community,
            commands::save_recommendations,
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
