//! Ctrl+Alt+C：强制切回官方前端，不退出启动器。
//!
//! 第三方前端跑在 WebView2 里。如果它白屏、没有可用的退出入口，或者干脆把页面
//! 卡死，用户就没有办法回到官方界面。这里用 Win32 的 `RegisterHotKey` 注册系统级
//! 快捷键，在独立线程里收 `WM_HOTKEY` —— 不经过 WebView，渲染进程卡住也能触发。
//!
//! 触发后：清掉进程内的 `MCUGL_FRONTEND` 覆盖，清掉配置里选中的前端，然后原地
//! 重建主窗口（`switch_frontend`）。进程不退出，启动器和已加载的数据都还在。

use tauri::{AppHandle, Manager};

use crate::{
    frontend::{self, FrontendState},
    report_warning,
    state::Backend,
};

pub const SHORTCUT_LABEL: &str = "Ctrl+Alt+C";

/// 切回官方前端。必须在**非事件循环线程**上调用（建窗会阻塞等待事件循环处理）。
fn force_official(app: &AppHandle) {
    let Some(state) = app.try_state::<FrontendState>() else {
        return;
    };
    // 已经在官方前端时不做任何事，避免误触后无谓重建窗口。
    if state.current().is_none() {
        return;
    }

    eprintln!("[mcugl] 收到 {SHORTCUT_LABEL}：强制切回官方前端");

    // SAFETY: 修改进程环境变量。这次写入只影响本进程内后续的解析；即使与其它
    // 读取并发，也不会造成内存不安全。
    unsafe {
        std::env::remove_var(frontend::FRONTEND_ENV);
    }

    if let Some(backend) = app.try_state::<Backend>() {
        if let Ok(mut data) = backend.data.lock() {
            if data.state.config.frontend.is_some() {
                data.state.config.frontend = None;
                if let Err(error) = data.state.config.save(backend.config_dir()) {
                    eprintln!("[mcugl] 清除前端选择失败：{error}");
                }
            }
        }
    }

    if let Err(error) = crate::switch_frontend(app, None) {
        report_warning(
            app,
            "无法切回官方前端",
            &format!("{error}\n\n可以关闭启动器后重新打开。"),
        );
    }
}

/// 注册全局快捷键；失败只打日志，不影响启动。
pub fn spawn(app: AppHandle) {
    #[cfg(windows)]
    {
        let _ = std::thread::Builder::new()
            .name("mcugl-shortcut".into())
            .spawn(move || windows_hotkey(app));
    }
    #[cfg(not(windows))]
    {
        let _ = app;
    }
}

#[cfg(windows)]
fn windows_hotkey(app: AppHandle) {
    use std::ptr;

    use windows_sys::Win32::UI::{
        Input::KeyboardAndMouse::{
            RegisterHotKey, UnregisterHotKey, MOD_ALT, MOD_CONTROL, MOD_NOREPEAT, VK_C,
        },
        WindowsAndMessaging::{GetMessageW, MSG, WM_HOTKEY},
    };

    const HOTKEY_ID: i32 = 0x4D43; // "MC"

    // SAFETY: `hwnd` 为 null 表示把快捷键绑定到当前线程；线程一直活到进程结束。
    let registered = unsafe {
        RegisterHotKey(
            ptr::null_mut(),
            HOTKEY_ID,
            MOD_CONTROL | MOD_ALT | MOD_NOREPEAT,
            VK_C as u32,
        ) != 0
    };
    if !registered {
        eprintln!("[mcugl] 无法注册快捷键 {SHORTCUT_LABEL}（可能已被其它程序占用）");
        return;
    }
    eprintln!("[mcugl] 已注册快捷键 {SHORTCUT_LABEL}：强制切回官方前端");

    let mut message = MSG::default();
    loop {
        // SAFETY: 从本线程的消息队列取消息；`message` 由系统填充。
        let result = unsafe { GetMessageW(&mut message, ptr::null_mut(), 0, 0) };
        if result <= 0 {
            break;
        }
        if message.message == WM_HOTKEY && message.wParam == HOTKEY_ID as usize {
            // 直接在本线程切前端：建窗会向事件循环投递并由本线程等待结果，
            // 若交回主线程执行（run_on_main_thread）反而会和事件循环互相等待。
            force_official(&app);
        }
    }

    // SAFETY: 与注册时同一线程、同一 id。
    unsafe {
        UnregisterHotKey(ptr::null_mut(), HOTKEY_ID);
    }
}
