//! Tauri 命令按业务领域分组。
//!
//! 每个领域模块只暴露稳定的命令入口；具体实现暂时集中在 `implementation`，
//! 便于在真实内核接入时逐领域替换，而不改变前端命令名和 DTO。

pub mod accounts;
pub mod community;
pub mod content;
pub mod frontends;
pub mod implementation;
pub mod instances;
pub mod kernel;
pub mod launch;
pub mod path_picker;
pub mod saves;
pub mod servers;
pub mod settings;
pub mod state;
pub mod tasks;
