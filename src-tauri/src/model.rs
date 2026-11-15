//! 前后端共享的数据模型。
//!
//! 字段与 `src/types/launcher.ts` 一一对应；序列化为 camelCase，前端可直接使用。
//! 内核重写时保留这些结构体即可维持 UI 契约不变。

use serde::{Deserialize, Serialize};
use std::collections::HashMap;
pub use crate::schema_dto::generated::common::{ResourceKeyInfo, SavedInfo};
pub use crate::schema_dto::generated::tasks::LaunchAcceptedInfo;
pub use crate::schema_dto::generated::content::{
    LoaderListInfo, ResourceVersionInfo, ResourceVersionsInfo, VanillaVersionInfo,
};
pub use crate::schema_dto::generated::accounts::{AvatarInfo, MuaProfileInfo};
pub use crate::schema_dto::generated::content::{ContentItem, ContentSearchResult};
pub use crate::schema_dto::generated::instances::{
    DataSourceSet, FavoriteInfo, InstanceCatalogInfo, InstanceInfo, LauncherOverview, ModInfo,
    SavedServerInfo,
    VersionChoice, VersionGroupInfo,
};

/// 可缓存资源的版本化快照。
#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ResourceSnapshot<T> {
    pub epoch: String,
    pub revision: u64,
    pub data: T,
}

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

#[derive(Clone, Debug, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct AccountInfo {
    pub id: String,
    pub kind: String,
    pub player: String,
    pub status: String,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
/// 演示内核的内部聚合存储，不是前端 DTO；查询接口必须返回独立资源快照。
pub struct BackendState {
    pub app_version: String,
    pub active_account_id: Option<String>,
    pub accounts: Vec<AccountInfo>,
    pub root: String,
    pub game_roots: Vec<String>,
    pub java: String,
    pub width: i32,
    pub height: i32,
    pub ram: i32,
    pub auto_memory: bool,
    pub anonymous: bool,
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
    pub data_sources: DataSourceSet,
    pub default_data_sources: DataSourceSet,
    pub custom_data_sources: DataSourceSet,
    pub data_source_mode: String,
    pub auto_chinese: bool,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct SettingsInfo {
    pub root: String,
    pub game_roots: Vec<String>,
    pub java: String,
    pub width: i32,
    pub height: i32,
    pub ram: i32,
    pub auto_memory: bool,
    pub anonymous: bool,
    pub has_community_key: bool,
    pub has_admin_key: bool,
    pub skin_station: String,
    pub cape: String,
    pub data_sources: DataSourceSet,
    pub default_data_sources: DataSourceSet,
    pub custom_data_sources: DataSourceSet,
    pub data_source_mode: String,
    pub auto_chinese: bool,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct TaskProgressInfo {
    pub completed: u64,
    pub total: Option<u64>,
    pub unit: String,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct TaskResultInfo {
    pub changed: Vec<ResourceKeyInfo>,
    pub entity_ids: Vec<String>,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct TaskInfo {
    pub id: String,
    pub name: String,
    pub kind: String,
    pub instance_id: Option<String>,
    pub status: String,
    pub phase: String,
    pub progress: Option<TaskProgressInfo>,
    pub allowed_actions: Vec<String>,
    pub error: Option<ApiError>,
    pub result: Option<TaskResultInfo>,
    pub created_at: String,
    pub updated_at: String,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct GameSessionInfo {
    pub session_id: String,
    pub instance_id: String,
    pub account_id: String,
    pub status: String,
    pub pid: Option<u32>,
    pub exit_code: Option<i32>,
    pub error: Option<ApiError>,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct ServerPingInfo {
    pub id: String,
    pub online: bool,
    pub players: u32,
    pub max: u32,
    pub ping: u32,
    pub motd: String,
    pub version: String,
    pub error: String,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct AnnouncementInfo {
    pub id: String,
    pub title: String,
    pub body: String,
    pub tag: String,
    pub author: String,
    pub published_at: Option<String>,
    pub image_url: String,
    pub markdown: String,
}

#[derive(Clone, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct RecommendationInfo {
    pub id: String,
    pub r#type: String,
    pub name: String,
    pub author: String,
    pub description: String,
    pub tags: Vec<String>,
    pub version: String,
    pub size: String,
    pub url: String,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct QuoteInfo {
    pub id: String,
    pub r#type: String,
    pub text: String,
    pub author: String,
    pub source: String,
    pub image_url: String,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct CommunityData {
    pub announcements: Vec<AnnouncementInfo>,
    pub recommendations: Vec<RecommendationInfo>,
    pub quotes: Vec<QuoteInfo>,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct JavaInfo {
    pub path: String,
    pub major: i32,
    pub arch: String,
    pub vendor: String,
    pub error: String,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct SaveBackupInfo {
    pub id: String,
    pub created: String,
    pub worlds: Vec<String>,
}

/// 登录会话只暴露当前阶段所需的字段。
#[derive(Clone, Serialize)]
#[allow(dead_code)]
#[serde(
    tag = "state",
    rename_all = "camelCase",
    rename_all_fields = "camelCase"
)]
pub enum LoginSessionInfo {
    AwaitingAuthorization {
        auth_session_id: String,
        authorization_url: String,
    },
    AwaitingProfile {
        auth_session_id: String,
        profiles: Vec<MuaProfileInfo>,
    },
    Succeeded {
        auth_session_id: String,
        account_id: String,
    },
    Failed {
        auth_session_id: String,
        error: ApiError,
    },
    Cancelled {
        auth_session_id: String,
    },
    Expired {
        auth_session_id: String,
    },
}

#[derive(Clone, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct LaunchSettings {
    pub auto_memory: bool,
    pub full_screen: bool,
    pub ram_mb: i32,
    pub width: i32,
    pub height: i32,
    pub java: String,
    pub jvm_arguments: String,
    pub window_title: String,
    pub server: String,
    pub auto_chinese: bool,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct LaunchOptionsInfo {
    pub request_id: String,
    pub token: String,
    pub instance_id: String,
    pub display_name: String,
    pub inherited: bool,
    pub settings: LaunchSettings,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct UninstallPreviewInfo {
    pub plan_id: String,
    pub target_id: String,
    pub target_revision: u64,
    pub expires_at: String,
    pub name: String,
    pub notice: String,
    pub paths: Vec<String>,
    pub shared: bool,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct ContentUpdateInfo {
    pub id: String,
    pub name: String,
    pub before: String,
    pub after: String,
    pub available: bool,
    pub error: String,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct ContentUpdatesInfo {
    pub check_id: String,
    pub minecraft_version: String,
    pub items: Vec<ContentUpdateInfo>,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct ContentChangeInfo {
    pub project: String,
    pub before: String,
    pub after: String,
    pub path: String,
    pub old_path: String,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct ContentUpdatePreviewInfo {
    pub plan_id: String,
    pub target_id: String,
    pub target_revision: u64,
    pub expires_at: String,
    pub minecraft_version: String,
    pub bytes: i64,
    pub changes: Vec<ContentChangeInfo>,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct PackPreviewInfo {
    pub plan_id: String,
    pub target_id: String,
    pub target_revision: u64,
    pub expires_at: String,
    pub name: String,
    pub root: String,
    pub game: String,
    pub loader: String,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct SystemInfoInfo {
    pub app: String,
    pub dotnet: String,
    pub webview: String,
    pub os: String,
    pub root: String,
    pub data: String,
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

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct KeyResultInfo {
    pub role: String,
    pub ok: bool,
    pub message: String,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct TestResultInfo {
    pub name: String,
    pub url: String,
    pub ok: bool,
    pub message: String,
    pub ms: i64,
}

/// `save_settings` 的补丁参数：只覆盖调用方明确给出的字段。
#[derive(Clone, Deserialize, Default)]
#[serde(rename_all = "camelCase", default)]
pub struct AppSettingsPatch {
    pub root: Option<String>,
    pub java: Option<String>,
    pub ram: Option<i32>,
    pub width: Option<i32>,
    pub height: Option<i32>,
    pub auto_memory: Option<bool>,
    pub auto_chinese: Option<bool>,
    pub anonymous: Option<bool>,
    pub player: Option<String>,
}

/// 命令失败的结构化结果；前端依据 code 判断，不解析提示文本。
#[derive(Clone, Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ApiError {
    pub code: &'static str,
    pub message: String,
    pub retryable: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub field_errors: Option<HashMap<String, String>>,
}

impl ApiError {
    pub fn unsupported(command: &str) -> Self {
        Self {
            code: "UNSUPPORTED",
            message: format!("演示后端尚未实现 {command}，操作未执行。"),
            retryable: false,
            field_errors: None,
        }
    }

    pub fn validation(message: impl Into<String>, field: &str, detail: impl Into<String>) -> Self {
        let mut field_errors = HashMap::new();
        field_errors.insert(field.into(), detail.into());
        Self {
            code: "VALIDATION_ERROR",
            message: message.into(),
            retryable: false,
            field_errors: Some(field_errors),
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
                code: "CONFLICT",
                message: "当前任务状态不允许此操作。".into(),
                retryable: false,
                field_errors: None,
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
        let session = LoginSessionInfo::AwaitingAuthorization {
            auth_session_id: "auth-1".into(),
            authorization_url: "https://example.invalid/login".into(),
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
