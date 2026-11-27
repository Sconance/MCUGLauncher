//! 游戏启动命令。
//!
//! 启动请求显式绑定 `instanceId` 与 `accountId`，返回值只表示请求已接受；
//! 真实进程生命周期由游戏会话资源和准备任务资源报告。

#[allow(unused_imports)]
pub use super::implementation::launch_to_server;

use std::{
    fs, io,
    path::PathBuf,
    thread,
    time::{Duration, Instant},
};

use tauri::{AppHandle, Manager, State};
use time::{OffsetDateTime, format_description::well_known::Rfc3339};

use super::implementation::generated_id;
use crate::{
    core::{
        game_launch::{
            LaunchLog, LaunchOptions, PlayerProfile, VersionProfile, offline_profile,
            prepare_launch,
        },
        java_runtime::{scan_java_runtimes, select_java},
    },
    demo::StateResource,
    launcher_config::LauncherConfig,
    model::{
        AccountInfo, ApiError, GameSessionInfo, JavaInfo, LaunchAcceptedInfo, ResourceKeyInfo,
        SavedInfo, TaskInfo,
    },
    schema_dto::generated::tasks::{
        ResourceKeyInfo as TaskResource, TaskInfoError, TaskResultInfo,
    },
    state::Backend,
};

struct LaunchRequest {
    session_id: String,
    task_id: String,
    instance: PathBuf,
    name: String,
    config: LauncherConfig,
    player: PlayerProfile,
}

#[tauri::command]
pub async fn scan_java(backend: State<'_, Backend>) -> Result<Vec<JavaInfo>, ApiError> {
    let configured = backend.data.lock().unwrap().state.config.java.clone();
    let managed_dir = backend.config_dir().join("java-runtimes");
    tauri::async_runtime::spawn_blocking(move || {
        scan_java_runtimes(&configured, &managed_dir)
            .into_iter()
            .map(|runtime| JavaInfo {
                path: runtime.executable.to_string_lossy().into_owned(),
                major: i64::from(runtime.major),
                arch: runtime.arch,
                vendor: runtime.vendor,
                error: String::new(),
            })
            .collect()
    })
    .await
    .map_err(|error| api_error("INTERNAL_ERROR", error.to_string()))
}

#[tauri::command]
pub fn launch_game(
    app: AppHandle,
    backend: State<'_, Backend>,
    instance_id: String,
    account_id: String,
) -> Result<LaunchAcceptedInfo, ApiError> {
    let mut data = backend.data.lock().unwrap();
    let instance = data
        .state
        .instances
        .iter()
        .find(|instance| instance.id == instance_id && !instance.is_placeholder)
        .ok_or_else(|| api_error("NOT_FOUND", "当前目录没有该实例。"))?;
    if instance.loader != "Vanilla" {
        return Err(api_error("UNSUPPORTED", "当前只支持原版实例启动。"));
    }
    let path = instance
        .id
        .strip_prefix("local:")
        .ok_or_else(|| api_error("NOT_FOUND", "无法定位该实例的本地路径。"))?;
    let account = data
        .state
        .accounts
        .iter()
        .find(|account| account.id == account_id)
        .ok_or_else(|| api_error("NOT_FOUND", "所选账号不存在。"))?;
    let player = if account.kind == "offline" {
        offline_profile(&account.player).map_err(|error| {
            ApiError::validation("离线账号无法启动游戏。", "accountId", error.to_string())
        })?
    } else {
        resolve_online_profile(account)?
    };
    if data.games.iter().any(|game| {
        game.instance_id == instance_id && !matches!(game.status.as_str(), "failed" | "exited")
    }) {
        return Err(api_error("BUSY", "该实例正在启动或运行。"));
    }

    let session_id = generated_id("game");
    let task_id = generated_id("launch");
    let request = LaunchRequest {
        session_id: session_id.clone(),
        task_id: task_id.clone(),
        instance: PathBuf::from(path),
        name: instance.name.clone(),
        config: data.state.config.clone(),
        player,
    };
    let now = timestamp();
    data.tasks.insert(
        0,
        TaskInfo {
            id: task_id.clone(),
            name: format!("启动 Minecraft {}", request.name),
            kind: "launch".into(),
            instance_id: Some(instance_id.clone()),
            status: "queued".into(),
            phase: "waiting".into(),
            created_at: now.clone(),
            updated_at: now,
            ..Default::default()
        },
    );
    data.games.insert(
        0,
        GameSessionInfo {
            session_id: session_id.clone(),
            instance_id,
            account_id,
            status: "preparing".into(),
            pid: None,
            exit_code: None,
            error: None,
        },
    );
    if let Err(error) = thread::Builder::new()
        .name("mcugl-game".into())
        .spawn(move || run_game(&app, request))
    {
        data.tasks.retain(|task| task.id != task_id);
        data.games.retain(|game| game.session_id != session_id);
        return Err(api_error("INTERNAL_ERROR", error.to_string()));
    }
    data.touch(StateResource::Tasks);
    data.touch(StateResource::GameSessions);
    Ok(LaunchAcceptedInfo {
        session_id,
        task_id,
    })
}

/// 正版与其他登录方式接入后在这里校验、刷新并返回账号凭据。
fn resolve_online_profile(_account: &AccountInfo) -> Result<PlayerProfile, ApiError> {
    Err(api_error(
        "UNSUPPORTED",
        "当前账号的登录启动尚未接入，请使用离线账号。",
    ))
}

#[tauri::command]
pub fn stop_game(backend: State<'_, Backend>, session_id: String) -> Result<SavedInfo, ApiError> {
    let mut data = backend.data.lock().unwrap();
    let game = data
        .games
        .iter_mut()
        .find(|game| game.session_id == session_id)
        .ok_or_else(|| api_error("NOT_FOUND", "游戏会话不存在。"))?;
    match game.status.as_str() {
        "starting" | "running" => game.status = "stopping".into(),
        "stopping" => {}
        _ => return Err(api_error("CONFLICT", "该游戏会话尚未运行或已经退出。")),
    }
    data.touch(StateResource::GameSessions);
    Ok(SavedInfo::changed(
        "停止请求已接受。",
        vec![ResourceKeyInfo {
            kind: "gameSessions".into(),
            instance_id: None,
        }],
        vec![session_id],
    ))
}

fn run_game(app: &AppHandle, request: LaunchRequest) {
    let backend = app.state::<Backend>();
    let prepared = (|| {
        launch_phase(&backend, &request.task_id, "selectingJava")?;
        let version =
            VersionProfile::read(&request.instance.join(format!("{}.json", request.name)))?;
        let java = select_java(
            &request.config.java,
            version.required_java(),
            &backend.config_dir().join("java-runtimes"),
        )?;
        launch_phase(&backend, &request.task_id, "preparingFiles")?;
        let root = request
            .instance
            .parent()
            .and_then(|versions| versions.parent())
            .ok_or_else(|| io::Error::new(io::ErrorKind::InvalidData, "实例目录结构无效。"))?;
        prepare_launch(
            &version,
            LaunchOptions {
                root,
                version_dir: &request.instance,
                instance_name: &request.name,
                java: &java,
                player: &request.player,
                ram_mb: request.config.ram.clamp(512, 65536) as u32,
                width: request.config.width.clamp(640, 7680) as u32,
                height: request.config.height.clamp(480, 4320) as u32,
            },
        )
    })();

    let mut data = backend.data.lock().unwrap();
    let Some(task_index) = data
        .tasks
        .iter()
        .position(|task| task.id == request.task_id)
    else {
        return;
    };
    let Some(game_index) = data
        .games
        .iter()
        .position(|game| game.session_id == request.session_id)
    else {
        return;
    };
    let cancelled = data.tasks[task_index].status == "cancelling";
    if cancelled {
        drop(prepared);
        data.tasks[task_index].status = "cancelled".into();
        data.tasks[task_index].phase = "cancelled".into();
        data.tasks[task_index].updated_at = timestamp();
        data.games[game_index].status = "exited".into();
        data.touch(StateResource::Tasks);
        data.touch(StateResource::GameSessions);
        return;
    }

    let logs = backend.config_dir().join("game-logs");
    let log_path = logs.join(format!("{}.log", request.session_id));
    data.games[game_index].status = "starting".into();
    let spawned = prepared.and_then(|launch| {
        fs::create_dir_all(&logs)?;
        launch.spawn(&log_path)
    });
    let (mut child, _natives) = match spawned {
        Ok(process) => process,
        Err(error) => {
            let error = launch_io_error(error);
            let task = &mut data.tasks[task_index];
            task.status = "failed".into();
            task.phase = "failed".into();
            task.updated_at = timestamp();
            task.error = Some(TaskInfoError {
                code: error.code.clone(),
                message: error.message.clone(),
                retryable: error.retryable,
                field_errors: Default::default(),
            });
            let game = &mut data.games[game_index];
            game.status = "failed".into();
            game.error = error_object(&error);
            data.touch(StateResource::Tasks);
            data.touch(StateResource::GameSessions);
            return;
        }
    };
    let task = &mut data.tasks[task_index];
    task.status = "succeeded".into();
    task.phase = "succeeded".into();
    task.updated_at = timestamp();
    task.result = Some(TaskResultInfo {
        changed: vec![TaskResource {
            kind: "gameSessions".into(),
            instance_id: None,
        }],
        entity_ids: vec![request.session_id.clone()],
    });
    data.games[game_index].pid = Some(i64::from(child.id()));
    data.touch(StateResource::Tasks);
    data.touch(StateResource::GameSessions);
    drop(data);

    let mut killed = false;
    let mut log = LaunchLog::open(&log_path).ok();
    let spawned_at = Instant::now();
    let mut ready = false;
    let exit = loop {
        match child.try_wait() {
            Ok(Some(status)) => break Ok(status),
            Err(error) => {
                let _ = child.kill();
                let _ = child.wait();
                break Err(error);
            }
            Ok(None) => {}
        }
        if !ready {
            ready = log.as_mut().is_some_and(|log| log.ready().unwrap_or(false))
                || spawned_at.elapsed() >= Duration::from_secs(30);
        }
        let mut data = backend.data.lock().unwrap();
        if let Some(game) = data
            .games
            .iter_mut()
            .find(|game| game.session_id == request.session_id)
        {
            if game.status == "starting" && ready {
                game.status = "running".into();
                data.touch(StateResource::GameSessions);
            } else if game.status == "stopping" && !killed {
                match child.kill() {
                    Ok(()) => killed = true,
                    Err(error) => {
                        game.status = if ready { "running" } else { "starting" }.into();
                        game.error = error_object(&launch_io_error(error));
                        data.touch(StateResource::GameSessions);
                    }
                }
            }
        }
        drop(data);
        thread::sleep(Duration::from_millis(150));
    };

    let mut data = backend.data.lock().unwrap();
    if let Some(game) = data
        .games
        .iter_mut()
        .find(|game| game.session_id == request.session_id)
    {
        game.pid = None;
        game.exit_code = exit
            .as_ref()
            .ok()
            .and_then(|status| status.code())
            .map(i64::from);
        if exit.as_ref().is_ok_and(|status| status.success() || killed) {
            game.status = "exited".into();
            game.error = None;
        } else {
            game.status = "failed".into();
            let message = match exit {
                Ok(status) => format!(
                    "Minecraft 异常退出（{status}），日志：{}",
                    log_path.display()
                ),
                Err(error) => format!(
                    "无法读取游戏进程状态：{error}，日志：{}",
                    log_path.display()
                ),
            };
            game.error = error_object(&api_error("IO_ERROR", message));
        }
        data.touch(StateResource::GameSessions);
    }
}

fn launch_phase(backend: &Backend, id: &str, phase: &str) -> io::Result<()> {
    let mut data = backend.data.lock().unwrap();
    let task = data
        .tasks
        .iter_mut()
        .find(|task| task.id == id)
        .filter(|task| task.status != "cancelling")
        .ok_or_else(|| io::Error::new(io::ErrorKind::Interrupted, "启动已取消。"))?;
    task.status = "running".into();
    task.phase = phase.into();
    task.updated_at = timestamp();
    data.touch(StateResource::Tasks);
    Ok(())
}

fn timestamp() -> String {
    OffsetDateTime::now_utc()
        .format(&Rfc3339)
        .unwrap_or_default()
}

fn launch_io_error(error: io::Error) -> ApiError {
    let code = match error.kind() {
        io::ErrorKind::NotFound => "NOT_FOUND",
        io::ErrorKind::Unsupported => "UNSUPPORTED",
        io::ErrorKind::InvalidInput => "VALIDATION_ERROR",
        _ => "IO_ERROR",
    };
    api_error(code, error.to_string())
}

fn api_error(code: &str, message: impl Into<String>) -> ApiError {
    ApiError {
        code: code.into(),
        message: message.into(),
        retryable: matches!(code, "BUSY" | "IO_ERROR"),
        field_errors: Default::default(),
    }
}

fn error_object(error: &ApiError) -> Option<serde_json::Map<String, serde_json::Value>> {
    serde_json::to_value(error).ok()?.as_object().cloned()
}
