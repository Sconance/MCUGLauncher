//! 原生路径选择命令。调用方负责处理选中的路径。

use std::path::PathBuf;

use tauri::{Window, async_runtime};
use tauri_plugin_dialog::DialogExt;

use crate::model::ApiError;

enum Selection {
    File,
    #[cfg(desktop)]
    Folder,
}

#[tauri::command]
pub async fn pick_file(
    window: Window,
    title: Option<String>,
    default_directory: Option<PathBuf>,
) -> Result<Option<String>, ApiError> {
    pick_path(window, title, default_directory, Selection::File).await
}

#[cfg(desktop)]
#[tauri::command]
pub async fn pick_folder(
    window: Window,
    title: Option<String>,
    default_directory: Option<PathBuf>,
) -> Result<Option<String>, ApiError> {
    pick_path(window, title, default_directory, Selection::Folder).await
}

async fn pick_path(
    window: Window,
    title: Option<String>,
    default_directory: Option<PathBuf>,
    selection: Selection,
) -> Result<Option<String>, ApiError> {
    let mut dialog = window.dialog().file();
    #[cfg(any(windows, target_os = "macos"))]
    {
        dialog = dialog.set_parent(&window);
    }
    if let Some(title) = title {
        dialog = dialog.set_title(title);
    }
    if let Some(directory) = default_directory {
        dialog = dialog.set_directory(directory);
    }

    let (sender, mut receiver) = async_runtime::channel(1);
    let on_selection = move |path| {
        let _ = sender.try_send(path);
    };
    match selection {
        Selection::File => dialog.pick_file(on_selection),
        #[cfg(desktop)]
        Selection::Folder => dialog.pick_folder(on_selection),
    }

    let selected = receiver.recv().await.ok_or_else(|| ApiError {
        code: "INTERNAL_ERROR".into(),
        message: "系统选择器未返回结果。".into(),
        retryable: true,
        field_errors: Default::default(),
    })?;
    selected
        .map(|path| {
            path.into_path()
                .ok()
                .and_then(|path| path.into_os_string().into_string().ok())
                .ok_or_else(|| ApiError {
                    code: "IO_ERROR".into(),
                    message: "所选路径无法表示为本地路径字符串。".into(),
                    retryable: false,
                    field_errors: Default::default(),
                })
        })
        .transpose()
}
