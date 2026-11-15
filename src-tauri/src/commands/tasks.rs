//! 长任务和游戏会话查询/控制命令。
//!
//! 任务动作由后端状态机校验；游戏停止只表示停止请求，不等于进程已经退出。

#[allow(unused_imports)]
pub use super::implementation::{list_game_sessions, list_tasks, stop_game, task_action};
