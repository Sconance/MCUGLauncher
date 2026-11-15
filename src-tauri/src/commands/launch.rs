//! 游戏启动命令。
//!
//! 启动请求显式绑定 `instanceId` 与 `accountId`，返回值只表示请求已接受；
//! 真实进程生命周期由游戏会话资源和准备任务资源报告。

#[allow(unused_imports)]
pub use super::implementation::{launch_game, launch_to_server};
