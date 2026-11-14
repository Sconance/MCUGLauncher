use std::sync::Mutex;

use crate::demo::Data;

/// 后端运行时状态。内核重写时把 `Data` 换成真实的配置、账号与队列。
pub struct Backend {
    pub data: Mutex<Data>,
}

impl Backend {
    pub fn new() -> Self {
        Self {
            data: Mutex::new(Data::demo()),
        }
    }
}

impl Default for Backend {
    fn default() -> Self {
        Self::new()
    }
}
