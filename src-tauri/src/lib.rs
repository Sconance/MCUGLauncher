mod commands;
mod demo;
mod frontend;
mod frontends;
mod launcher_config;
mod local_instances;
mod model;
mod schema_dto;
#[cfg(windows)]
mod shortcut;
mod state;

use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::{Arc, Mutex};
use std::time::Duration;

use frontend::Frontend;
use state::Backend;
use tauri::{AppHandle, Manager, WebviewUrl, WebviewWindow, WebviewWindowBuilder};
use tauri_plugin_dialog::{DialogExt, MessageDialogKind};

pub(crate) const DEVICE_OS: &str = std::env::consts::OS;

/// 主窗口的默认 label（创建时会在后面加序号，见 `create_main_window`）。
const MAIN_LABEL: &str = "main";

/// 每次建窗递增，保证「重启前端」时新旧窗口不会撞 label。
static WINDOW_GENERATION: AtomicU64 = AtomicU64::new(0);

/// 创建主窗口：窗口定义写在这里而不是 `tauri.conf.json`，
/// 这样 WebView2 创建失败时可以重试 / 换 profile，而不是直接被 Tauri panic 掉。
fn build_main_window(
    app: &AppHandle,
    label: &str,
    data_directory: Option<PathBuf>,
    frontend: Option<&Frontend>,
) -> tauri::Result<tauri::WebviewWindow> {
    let url = match frontend {
        Some(frontend) => WebviewUrl::CustomProtocol(frontend.url()?),
        None => WebviewUrl::App("index.html".into()),
    };
    let mut builder = WebviewWindowBuilder::new(app, label, url)
        .title("MCUG Launcher")
        .inner_size(1000.0, 600.0)
        .min_inner_size(1000.0, 600.0)
        .center()
        .decorations(true)
        .resizable(true);
    if let Some(directory) = data_directory {
        builder = builder.data_directory(directory);
    }
    // 第三方前端在页面脚本运行前就知道自己挂在内核上（见 docs/KERNEL_API_V1.md）。
    if let Some(frontend) = frontend {
        builder = builder.initialization_script(frontend.initialization_script());
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
fn ensure_writable_dir(dir: &Path) -> Result<(), String> {
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

/// 显式指定数据目录的环境变量。设置后只认它，不再静默换目录——
/// 否则用户以为配置写到了 A，实际落在 B，排查时非常难查。
const DATA_DIR_ENV: &str = "MCUGL_DATA_DIR";

/// 读 `MCUGL_DATA_DIR`；空字符串等同于未设置（PowerShell 里 `$env:X = ""` 常见）。
fn explicit_data_dir() -> Option<PathBuf> {
    std::env::var_os(DATA_DIR_ENV)
        .filter(|value| !value.is_empty())
        .map(PathBuf::from)
}

/// 数据目录（配置、日志）候选清单，按优先级排列：
/// 1. `MCUGL_DATA_DIR`（显式覆盖）；
/// 2. 平台标准目录：Windows `%LOCALAPPDATA%\com.mcug.launcher`，其他 `app_data_dir`；
/// 3. `%LOCALAPPDATA%\MCUGLauncher`（与 WebView2 profile 同级，便于整体迁移/清理）；
/// 4. 程序所在目录的 `mcugl-data`（便携版、`tauri dev` 的 `target\debug`）；
/// 5. 临时目录下的 `MCUGLauncher-data`（最后的兜底）。
fn data_dir_candidates(app: &AppHandle) -> Vec<PathBuf> {
    if let Some(custom) = explicit_data_dir() {
        return vec![custom];
    }

    let mut dirs = Vec::new();
    let standard = if DEVICE_OS == "windows" {
        app.path().app_local_data_dir().ok()
    } else {
        app.path().app_data_dir().ok()
    };
    if let Some(dir) = standard {
        dirs.push(dir);
    }
    if let Ok(local) = app.path().local_data_dir() {
        dirs.push(local.join("MCUGLauncher"));
    }
    if let Some(sidecar) = std::env::current_exe()
        .ok()
        .and_then(|exe| exe.parent().map(|dir| dir.join("mcugl-data")))
    {
        dirs.push(sidecar);
    }
    dirs.push(std::env::temp_dir().join("MCUGLauncher-data"));

    // 标准目录与 `%LOCALAPPDATA%\MCUGLauncher` 在非 Windows 上可能重合，去重后再试。
    let mut unique: Vec<PathBuf> = Vec::new();
    for dir in dirs {
        if !unique.iter().any(|seen| seen == &dir) {
            unique.push(dir);
        }
    }
    unique
}

/// 按优先级挑第一个真正可写的数据目录。`explicit` 为真时只试第一个，失败直接返回原因。
fn pick_writable_dir(
    candidates: impl IntoIterator<Item = PathBuf>,
    explicit: bool,
) -> Result<PathBuf, String> {
    let mut failures: Vec<String> = Vec::new();
    for dir in candidates {
        match ensure_writable_dir(&dir) {
            Ok(()) => return Ok(dir),
            Err(reason) => {
                failures.push(format!("{}（{reason}）", dir.display()));
                if explicit {
                    break;
                }
            }
        }
    }
    Err(failures.join("；"))
}

fn resolve_data_dir(app: &AppHandle) -> Result<PathBuf, String> {
    let explicit = explicit_data_dir().is_some();
    let dir = pick_writable_dir(data_dir_candidates(app), explicit)?;
    eprintln!("[mcugl] 数据目录：{}", dir.display());
    Ok(dir)
}

/// 启动期致命错误：打日志 + 原生弹窗，然后退出。
///
/// 关键点是**不要**把错误 `return Err` 给 Tauri 的 setup hook：tauri-2.12.5 会把它
/// 变成 `panic!("Failed to setup app: ...")`，用户只看到一屏 panic 栈，既不知道
/// 哪个目录不可写，也没机会改环境变量。
fn report_fatal(app: &AppHandle, title: &str, detail: &str) {
    eprintln!("[mcugl] {title}：{detail}");
    let _ = app
        .dialog()
        .message(detail)
        .kind(MessageDialogKind::Error)
        .title(title)
        .blocking_show();
    app.exit(1);
}

pub(crate) fn report_warning(app: &AppHandle, title: &str, detail: &str) {
    eprintln!("[mcugl] {title}：{detail}");
    let _ = app
        .dialog()
        .message(detail)
        .kind(MessageDialogKind::Warning)
        .title(title)
        .blocking_show();
}

fn create_main_window(
    app: &AppHandle,
    frontend: Option<&Frontend>,
) -> tauri::Result<WebviewWindow> {
    let generation = WINDOW_GENERATION.fetch_add(1, Ordering::Relaxed);
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
        if let Err(reason) = ensure_writable_dir(dir) {
            eprintln!("[mcugl] profile 不可用：{}（{reason}）", dir.display());
            continue;
        }
        for attempt in 0..*tries {
            if attempt > 0 {
                std::thread::sleep(Duration::from_millis(400));
            }
            let label = format!("{MAIN_LABEL}-{generation}-{label_index}");
            label_index += 1;
            match build_main_window(app, &label, Some(dir.clone()), frontend) {
                Ok(window) => {
                    let _ = window.show();
                    eprintln!("[mcugl] 主窗口已创建，WebView2 profile：{}", dir.display());
                    return Ok(window);
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
            eprintln!(
                "[mcugl] 该 profile 不可用，改试下一个：{}",
                ladder[index + 1].0.display()
            );
        }
    }

    Err(last_error.expect("至少尝试过一次创建窗口"))
}

/// 解析「这次应该加载哪个前端」：`MCUGL_FRONTEND` > 设置里选中的已安装前端 > 官方前端。
/// 启动和「重启前端」共用同一套顺序，避免两边行为漂移。
pub(crate) fn resolve_selected_frontend(
    config_dir: &Path,
    selected: Option<String>,
) -> Result<Option<Frontend>, String> {
    match frontend::resolve_from_env(config_dir) {
        Ok(Some(frontend)) => Ok(Some(frontend)),
        Ok(None) => match selected {
            Some(identifier) => frontends::resolve_active(config_dir, &identifier).map(Some),
            None => Ok(None),
        },
        Err(reason) => Err(reason),
    }
}

/// 不停进程换前端：更新共享槽位 → 建新主窗口 → 销毁旧窗口。
///
/// 顺序不能反：Tauri 在最后一个窗口关闭时会退出应用，所以必须先有新窗口，
/// 再销毁旧的。新窗口也只在旧窗口销毁后才抢焦点，避免闪一下又跳回去。
pub(crate) fn switch_frontend(app: &AppHandle, next: Option<Frontend>) -> Result<(), String> {
    let state = app
        .try_state::<frontend::FrontendState>()
        .ok_or_else(|| "前端状态尚未初始化".to_string())?;
    let previous = state.current();
    state.set(next.clone());

    match create_main_window(app, next.as_ref()) {
        Ok(window) => {
            let new_label = window.label().to_string();
            for (label, old) in app.webview_windows() {
                if label != new_label {
                    let _ = old.hide();
                    let _ = old.destroy();
                }
            }
            let _ = window.show();
            let _ = window.set_focus();
            eprintln!(
                "[mcugl] 前端已切换：{}",
                next.as_ref()
                    .map(|frontend| format!("{} {}", frontend.manifest.name, frontend.manifest.version))
                    .unwrap_or_else(|| "官方前端".to_string())
            );
            Ok(())
        }
        Err(error) => {
            // 建窗失败就退回去，旧窗口还在，界面不会丢。
            state.set(previous);
            Err(format!("创建新窗口失败：{error}"))
        }
    }
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    eprintln!("[mcugl] device_os={DEVICE_OS}");
    // 已安装的前端来自数据目录里的配置，只能在 setup 里解析；但 `mcugl://` 自定义
    // 协议必须在建 Builder 时注册。用共享槽位把 setup 解析出来的前端交给协议处理器。
    //
    // 例外：`MCUGL_FRONTEND` 指向 `.mcuglf` 文件时，协议处理器需要在窗口创建前就有
    // 内容可服（WebView2 可能在 setup 走完前发起请求），所以开发包在这里先解出来。
    let mut provisional_frontend: Option<Frontend> = None;
    let mut startup_error: Option<String> = None;
    if let Some(package) = frontend::env_package() {
        let data_dir = explicit_data_dir()
            .unwrap_or_else(|| std::env::temp_dir().join("MCUGLauncher-dev"));
        match frontends::resolve_dev_package(&data_dir, &package) {
            Ok(frontend) => provisional_frontend = Some(frontend),
            Err(reason) => {
                startup_error = Some(format!(
                    "无法加载 MCUGL_FRONTEND 指定的前端包（{}）：\n\n{reason}\n\n\
                     移除 MCUGL_FRONTEND 可恢复官方前端。",
                    package.display()
                ));
            }
        }
    }
    let frontend_slot: Arc<Mutex<Option<Frontend>>> = Arc::new(Mutex::new(provisional_frontend));
    let protocol_frontend = Arc::clone(&frontend_slot);
    let setup_frontend = Arc::clone(&frontend_slot);
    let setup_provisional = Arc::clone(&frontend_slot);
    tauri::Builder::default()
        .register_uri_scheme_protocol(frontend::FRONTEND_SCHEME, move |_ctx, request| {
            let frontend = protocol_frontend
                .lock()
                .map(|slot| slot.clone())
                .unwrap_or_default();
            frontend::respond(frontend.as_ref(), &request)
        })
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_opener::init())
        .setup(move |app| {
            let handle = app.handle();
            if let Some(detail) = &startup_error {
                report_fatal(handle, "MCUG Launcher 无法加载第三方前端", detail);
                return Ok(());
            }
            let paths = handle.path();
            let config_dir = match resolve_data_dir(handle) {
                Ok(dir) => dir,
                Err(reason) => {
                    report_fatal(
                        handle,
                        "MCUG Launcher 无法启动",
                        &format!(
                            "没有找到可写的数据目录，配置和日志无法保存。\n\n{reason}\n\n\
                             可以设置环境变量 MCUGL_DATA_DIR 指定一个可写目录后重试。"
                        ),
                    );
                    return Ok(());
                }
            };
            let default_root = || {
                match DEVICE_OS {
                    "windows" => paths.data_dir().map(|dir| dir.join(".minecraft")),
                    "macos" => paths.data_dir().map(|dir| dir.join("minecraft")),
                    _ => paths.home_dir().map(|dir| dir.join(".minecraft")),
                }
                .map_err(std::io::Error::other)
            };
            let backend = match Backend::open(config_dir.clone(), default_root) {
                Ok(backend) => backend,
                Err(error) => {
                    report_fatal(
                        handle,
                        "MCUG Launcher 无法启动",
                        &format!("读取/初始化配置失败（{}）：{error}", config_dir.display()),
                    );
                    return Ok(());
                }
            };
            // 配置里选中的前端优先于官方前端，但低于 MCUGL_FRONTEND（开发期覆盖）。
            let selected_frontend = backend
                .data
                .lock()
                .ok()
                .and_then(|data| data.state.config.frontend.clone());
            app.manage(backend);

            // 开发包（MCUGL_FRONTEND 指向 .mcuglf）已在建 Builder 前解好；其余走目录解析。
            let frontend = if setup_provisional
                .lock()
                .map(|slot| slot.is_some())
                .unwrap_or(false)
            {
                setup_provisional.lock().unwrap().clone()
            } else {
                match frontend::resolve_from_env(&config_dir) {
                    Ok(Some(frontend)) => Some(frontend),
                    Ok(None) => match selected_frontend {
                        Some(identifier) => {
                            match frontends::resolve_active(&config_dir, &identifier) {
                                Ok(frontend) => Some(frontend),
                                Err(reason) => {
                                    report_warning(
                                        handle,
                                        "已选择的前端无法加载",
                                        &format!(
                                            "{reason}\n\n本次启动改用官方前端。可以在「设置 → 开发者选项 → \
                                             前端」里重新选择或移除。"
                                        ),
                                    );
                                    None
                                }
                            }
                        }
                        None => None,
                    },
                    Err(reason) => {
                        report_fatal(
                            handle,
                            "MCUG Launcher 无法加载第三方前端",
                            &format!(
                                "{reason}\n\n不设置 MCUGL_FRONTEND 时使用随程序打包的官方前端。"
                            ),
                        );
                        return Ok(());
                    }
                }
            };

            if let Some(frontend) = &frontend {
                eprintln!(
                    "[mcugl] 前端（{}）：{} {}（{}）",
                    frontend.mode,
                    frontend.manifest.name,
                    frontend.manifest.version,
                    frontend.dir.display()
                );
            }
            if let Ok(mut slot) = setup_frontend.lock() {
                *slot = frontend.clone();
            }
            app.manage(frontend::FrontendState(Arc::clone(&frontend_slot)));
            // 第三方前端没有退出入口时，Ctrl+Alt+C 是唯一的逃生门。
            // 即使这次启动的是官方前端也要注册：用户可能随后在设置里切到第三方，
            // 此时处理器会先检查槽位，官方界面下直接忽略。
            #[cfg(windows)]
            shortcut::spawn(handle.clone());
            if let Err(error) = create_main_window(handle, frontend.as_ref()) {
                report_fatal(
                    handle,
                    "MCUG Launcher 无法启动",
                    &format!("主窗口创建失败：{error}"),
                );
            }
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            // 内核
            commands::kernel::kernel_manifest,
            // 前端包（宿主管理接口，不属于内核方法表）
            commands::frontends::install_frontend,
            commands::frontends::list_frontends,
            commands::frontends::read_frontend_icon,
            commands::frontends::use_frontend,
            commands::frontends::reload_frontend,
            commands::frontends::remove_frontend,
            // 状态
            commands::implementation::get_overview,
            commands::implementation::get_settings,
            commands::implementation::list_accounts,
            commands::implementation::list_instances,
            commands::implementation::list_all_instances,
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
            commands::implementation::remove_game_root,
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

#[cfg(test)]
mod tests {
    use super::pick_writable_dir;
    use std::path::PathBuf;

    /// 造一个「同名文件占位」的路径：`create_dir_all` 必然失败，用来模拟不可写目录。
    fn blocked_path() -> (tempfile::TempDir, PathBuf) {
        let guard = tempfile::tempdir().unwrap();
        let path = guard.path().join("blocked");
        std::fs::write(&path, b"x").unwrap();
        (guard, path)
    }

    #[test]
    fn falls_back_to_next_writable_directory() {
        let (_guard, blocked) = blocked_path();
        let good = tempfile::tempdir().unwrap();
        let picked = pick_writable_dir([blocked, good.path().to_path_buf()], false)
            .expect("应退到第二个目录");
        assert_eq!(picked, good.path());
    }

    #[test]
    fn explicit_directory_does_not_silently_fall_back() {
        let (_guard, blocked) = blocked_path();
        let good = tempfile::tempdir().unwrap();
        let error = pick_writable_dir([blocked, good.path().to_path_buf()], true).unwrap_err();
        assert!(error.contains("blocked"), "{error}");
    }

    #[test]
    fn reports_every_candidate_when_none_is_writable() {
        let (_guard, blocked) = blocked_path();
        let error = pick_writable_dir([blocked.clone(), blocked], false).unwrap_err();
        assert_eq!(error.matches("blocked").count(), 2, "{error}");
    }
}
