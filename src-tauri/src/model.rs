//! 前后端共享的数据模型。
//!
//! 字段与 `src/types/launcher.ts` 一一对应；序列化为 camelCase，前端可直接使用。
//! 内核重写时保留这些结构体即可维持 UI 契约不变。

pub use crate::schema_dto::generated::accounts::AccountInfo;
pub use crate::schema_dto::generated::accounts::AvatarInfo;
pub use crate::schema_dto::generated::accounts::LoginSessionInfo;
pub use crate::schema_dto::generated::common::ResourceSnapshot;
pub use crate::schema_dto::generated::common::{ApiError, ResourceKeyInfo, SavedInfo};
pub use crate::schema_dto::generated::content::{
    AnnouncementInfo, CommunityData, QuoteInfo, RecommendationInfo,
};
pub use crate::schema_dto::generated::content::{ContentItem, ContentSearchResult};
pub use crate::schema_dto::generated::content::{
    LaunchOptionsInfo, LaunchSettings, LoaderListInfo, ResourceVersionInfo, ResourceVersionsInfo,
    VanillaVersionInfo,
};
pub use crate::schema_dto::generated::operations::AppSettingsPatch;
pub use crate::schema_dto::generated::operations::{JavaInfo, ServerPingInfo};
pub use crate::schema_dto::generated::operations::{
    KeyResultInfo, SaveBackupInfo, SystemInfoInfo, TestResultInfo,
};
pub use crate::schema_dto::generated::tasks::LaunchAcceptedInfo;
pub use crate::schema_dto::generated::tasks::TaskProgressInfo;
pub use crate::schema_dto::generated::tasks::{GameSessionInfo, TaskInfo};
use serde::Serialize;
use std::collections::HashMap;

use crate::launcher_config::LauncherConfig;

impl Default for TaskInfo {
    fn default() -> Self {
        Self {
            id: String::new(),
            name: String::new(),
            kind: String::new(),
            instance_id: None,
            status: String::new(),
            phase: String::new(),
            progress: None,
            allowed_actions: Vec::new(),
            error: None,
            result: None,
            created_at: String::new(),
            updated_at: String::new(),
        }
    }
}
pub use crate::schema_dto::generated::instances::{
    DataSourceSet, FavoriteInfo, InstanceCatalogInfo, InstanceInfo, LauncherOverview, ModInfo,
    SavedServerInfo, SettingsInfo, VersionChoice, VersionGroupInfo,
};
pub use crate::schema_dto::generated::operations::{
    ContentUpdatePreviewInfo, ContentUpdatesInfo, PackPreviewInfo, UninstallPreviewInfo,
};

impl DataSourceSet {
    pub fn demo() -> Self {
        Self {
            announcements: "https://example.com/mcug/announcements.json".into(),
            servers: "https://example.com/mcug/servers.json".into(),
            recommendations: "https://example.com/mcug/recommendations.json".into(),
            quotes: "https://example.com/mcug/quotes.json".into(),
        }
    }
}

impl Default for DataSourceSet {
    fn default() -> Self {
        Self::demo()
    }
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
/// 演示内核的内部聚合存储，不是前端 DTO；查询接口必须返回独立资源快照。
pub struct BackendState {
    pub app_version: String,
    pub active_account_id: Option<String>,
    pub accounts: Vec<AccountInfo>,
    #[serde(flatten)]
    pub(crate) config: LauncherConfig,
    pub has_community_key: bool,
    pub has_admin_key: bool,
    pub skin_station: String,
    pub cape: String,
    pub selected_instance_id: Option<String>,
    pub instances: Vec<InstanceInfo>,
    pub mods: Vec<ModInfo>,
    pub servers: Vec<SavedServerInfo>,
    pub favorites: Vec<FavoriteInfo>,
    pub version_groups: Vec<VersionGroupInfo>,
    pub default_data_sources: DataSourceSet,
}

impl SavedInfo {
    pub fn changed(
        message: impl Into<String>,
        changed: Vec<ResourceKeyInfo>,
        entity_ids: Vec<String>,
    ) -> Self {
        Self {
            message: message.into(),
            changed,
            entity_ids,
        }
    }
}

impl ApiError {
    pub fn unsupported(command: &str) -> Self {
        Self {
            code: "UNSUPPORTED".into(),
            message: format!("演示后端尚未实现 {command}，操作未执行。"),
            retryable: false,
            field_errors: HashMap::new(),
        }
    }

    pub fn validation(message: impl Into<String>, field: &str, detail: impl Into<String>) -> Self {
        let mut field_errors = HashMap::new();
        field_errors.insert(field.into(), detail.into());
        Self {
            code: "VALIDATION_ERROR".into(),
            message: message.into(),
            retryable: false,
            field_errors,
        }
    }
}

impl TaskInfo {
    /// 可执行动作由后端统一计算；提交阶段禁止中断。
    pub fn actions(&self) -> Vec<String> {
        let actions: &[&str] = match self.status.as_str() {
            "succeeded" | "failed" | "cancelled" => &["delete"],
            _ if self.phase == "committing" => &[],
            "running" => &["pause", "cancel"],
            "paused" => &["resume", "cancel"],
            "queued" => &["cancel"],
            _ => &[],
        };
        actions.iter().map(|value| (*value).into()).collect()
    }

    /// 取消请求只进入清理阶段，不提前报告取消完成。
    pub fn apply_action(&mut self, action: &str) -> Result<(), ApiError> {
        if !self.actions().iter().any(|item| item == action) {
            return Err(ApiError {
                code: "CONFLICT".into(),
                message: "当前任务状态不允许此操作。".into(),
                retryable: false,
                field_errors: HashMap::new(),
            });
        }
        self.status = match action {
            "pause" => "paused",
            "resume" => "running",
            "cancel" => "cancelling",
            _ => self.status.as_str(),
        }
        .into();
        self.allowed_actions = self.actions();
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn cancellation_waits_for_cleanup() {
        let mut task = TaskInfo {
            status: "running".into(),
            ..Default::default()
        };
        task.apply_action("cancel").unwrap();
        assert_eq!(task.status, "cancelling");
        assert!(task.apply_action("delete").is_err());
    }

    #[test]
    fn committing_cannot_be_interrupted() {
        let mut task = TaskInfo {
            status: "running".into(),
            phase: "committing".into(),
            ..Default::default()
        };
        assert!(task.apply_action("pause").is_err());
        assert!(task.apply_action("cancel").is_err());
        task.status = "failed".into();
        assert_eq!(task.actions(), vec!["delete"]);
    }

    #[test]
    fn login_session_serializes_as_discriminated_union() {
        let session = LoginSessionInfo::Variant0 {
            auth_session_id: "auth-1".into(),
            authorization_url: "https://example.invalid/login".into(),
            state: serde_json::json!("awaitingAuthorization"),
        };
        let value = serde_json::to_value(session).unwrap();
        assert_eq!(value["state"], "awaitingAuthorization");
        assert_eq!(value["authSessionId"], "auth-1");
        assert!(value.get("profiles").is_none());
    }

    #[test]
    fn validation_error_serializes_field_errors() {
        let error = ApiError::validation("设置校验失败。", "ram", "内存超出范围。");
        let value = serde_json::to_value(error).unwrap();
        assert_eq!(value["code"], "VALIDATION_ERROR");
        assert_eq!(value["fieldErrors"]["ram"], "内存超出范围。");
    }
}
