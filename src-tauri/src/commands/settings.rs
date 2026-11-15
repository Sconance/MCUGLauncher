//! 设置、系统诊断、数据源和密钥命令。
//!
//! 设置写入必须携带 `expectedRevision`，由后端负责并发冲突校验。

#[allow(unused_imports)]
pub use super::implementation::{
    get_system_info, open_folder, read_log, reset_window, save_custom_sources, save_settings,
    scan_java, test_data_source, use_data_sources, verify_key,
};
