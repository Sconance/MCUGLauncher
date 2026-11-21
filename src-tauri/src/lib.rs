mod commands;
mod demo;
mod launcher_config;
mod local_instances;
mod model;
mod schema_dto;
mod state;

use std::path::{Path, PathBuf};
use std::time::Duration;

use state::Backend;
use tauri::{AppHandle, Manager, WebviewUrl, WebviewWindowBuilder};

pub(crate) const DEVICE_OS: &str = std::env::consts::OS;

/// 主窗口的默认 label（创建时会在后面加序号，见 `create_main_window`）。
const MAIN_LABEL: &str = "main";

/// 创建主窗口：窗口定义写在这里而不是 `tauri.conf.json`，
/// 这样 WebView2 创建失败时可以重试 / 换 profile，而不是直接被 Tauri panic 掉。
fn build_main_window(
    app: &AppHandle,
    label: &str,
    data_directory: Option<PathBuf>,
) -> tauri::Result<tauri::WebviewWindow> {
    let mut builder = WebviewWindowBuilder::new(app, label, WebviewUrl::App("index.html".into()))
        .title("MCUG Launcher")
        .inner_size(1000.0, 600.0)
        .min_inner_size(1000.0, 600.0)
        .center()
        .decorations(true)
        .resizable(true);
    if let Some(directory) = data_directory {
        builder = builder.data_directory(directory);
    }
    builder.build()
}

/// `0x800700AA（请求的资源在使用中）`：上一次的 `msedgewebview2.exe` 没退干净，还占着 WebView2 的
/// user data folder。常见于 `tauri dev` 重启、WebStorm 停止运行，或者被别的工具（沙箱/任务管理器）
/// 直接杀掉宿主进程——WebView2 的浏览器进程会从进程树里脱离、继续活着，把 profile 锁住。
///
/// 因此 profile 不用 `%LOCALAPPDATA%\com.mcug.launcher\EBWebView`（被历史锁住/损伤的那个），
/// 并按下面的阶梯选目录，任一步能起来就停：
/// 1. **dev / 便携场景**：程序所在目录下的 `webview2-profile`（`target\debug` 一定可写，
///    不受 `%LOCALAPPDATA%` 被策略/沙箱限制的影响）；
/// 2. `%LOCALAPPDATA%\MCUGLauncher\webview2`（安装版正常走这条）；
/// 3. 同一父目录下换一个全新名字（避开被占用的旧 profile）。
/// 主 profile 会重试 3 次（旧浏览器进程通常 1~2 秒内退出）；每步都会打路径和错误。
fn webview_profile_base(app: &AppHandle) -> PathBuf {
    if let Some(custom) = std::env::var_os("MCUGL_WEBVIEW_PROFILE") {
        return PathBuf::from(custom);
    }
    if let Ok(local) = app.path().local_data_dir() {
        return local.join("MCUGLauncher").join("webview2");
    }
    app.path()
        .app_local_data_dir()
        .map(|dir| dir.join("webview2"))
        .unwrap_or_else(|_| PathBuf::from("webview2"))
}

/// 建目录并写一个探针文件，提前暴露「拒绝访问」这类问题，并把是哪一步失败说清楚。
fn prepare_profile_dir(dir: &Path) -> Result<(), String> {
    if let Err(error) = std::fs::create_dir_all(dir) {
        return Err(format!("建目录失败: {error}"));
    }
    let probe = dir.join(".mcugl-write-probe");
    match std::fs::write(&probe, b"ok") {
        Ok(()) => {
            let _ = std::fs::remove_file(&probe);
            Ok(())
        }
        Err(error) => Err(format!("写探针失败: {error}")),
    }
}

fn create_main_window(app: &AppHandle) -> tauri::Result<()> {
    let custom = std::env::var_os("MCUGL_WEBVIEW_PROFILE").map(PathBuf::from);
    let base = webview_profile_base(app);
    let stamp = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map(|d| d.as_millis())
        .unwrap_or_default();
    let base_name = base
        .file_name()
        .and_then(|s| s.to_str())
        .unwrap_or("webview2")
        .to_string();
    let fresh = base.with_file_name(format!("{base_name}-{stamp}"));

    let mut ladder: Vec<(PathBuf, u32)> = Vec::new();
    if custom.is_some() {
        // 显式指定目录（排查/自测）：只按它来，再退到它的新名字
        ladder.push((base.clone(), 3));
        ladder.push((fresh, 1));
    } else {
        // dev 下优先用程序旁边的目录：不受 %LOCALAPPDATA% 写权限影响
        if let Some(sidecar) = std::env::current_exe()
            .ok()
            .and_then(|exe| exe.parent().map(|dir| dir.join("webview2-profile")))
        {
            ladder.push((sidecar, 3));
        }
        ladder.push((base.clone(), 1));
        ladder.push((fresh, 1));
    }

    let mut last_error: Option<tauri::Error> = None;
    let mut label_index = 0u32;

    for (index, (dir, tries)) in ladder.iter().enumerate() {
        if let Err(reason) = prepare_profile_dir(dir) {
            eprintln!("[mcugl] profile 不可用：{}（{reason}）", dir.display());
            continue;
        }
        for attempt in 0..*tries {
            if attempt > 0 {
                std::thread::sleep(Duration::from_millis(400));
            }
            let label = format!("{MAIN_LABEL}-{label_index}");
            label_index += 1;
            match build_main_window(app, &label, Some(dir.clone())) {
                Ok(window) => {
                    let _ = window.show();
                    eprintln!("[mcugl] 主窗口已创建，WebView2 profile：{}", dir.display());
                    return Ok(());
                }
                Err(error) => {
                    eprintln!(
                        "[mcugl] 创建主窗口失败（{}，第 {}/{} 次）：{error}",
                        dir.display(),
                        attempt + 1,
                        tries
                    );
                    // 失败时可能已经建出了空窗口，先销毁，避免重试撞 label
                    if let Some(window) = app.get_webview_window(&label) {
                        let _ = window.destroy();
                    }
                    last_error = Some(error);
                }
            }
        }
        if index + 1 < ladder.len() {
            eprintln!("[mcugl] 该 profile 不可用，改试下一个：{}", ladder[index + 1].0.display());
        }
    }

    Err(last_error.expect("至少尝试过一次创建窗口"))
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    eprintln!("[mcugl] device_os={DEVICE_OS}");
    tauri::Builder::default()
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_opener::init())
        .setup(|app| {
            let paths = app.path();
            let config_dir = if DEVICE_OS == "windows" {
                paths.app_local_data_dir()?
            } else {
                paths.app_data_dir()?
            };
            let backend = Backend::open(config_dir, || {
                match DEVICE_OS {
                    "windows" => paths.data_dir().map(|dir| dir.join(".minecraft")),
                    "macos" => paths.data_dir().map(|dir| dir.join("minecraft")),
                    _ => paths.home_dir().map(|dir| dir.join(".minecraft")),
                }
                .map_err(std::io::Error::other)
            })?;
            app.manage(backend);
            create_main_window(app.handle())?;
            Ok(())
        })
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
            commands::path_picker::pick_file,
            #[cfg(desktop)]
            commands::path_picker::pick_folder,
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
