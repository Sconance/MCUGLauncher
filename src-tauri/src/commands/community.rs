//! 社区公告、推荐和语录命令。
//!
//! 社区资源独立维护 revision；社区刷新失败不应影响实例和设置资源。

#[allow(unused_imports)]
pub use super::implementation::{get_community, refresh_community, save_recommendations};
