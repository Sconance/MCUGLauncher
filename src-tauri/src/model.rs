//! 前后端共享的数据模型。
//!
//! 字段与 `src/types/launcher.ts` 一一对应；序列化为 camelCase，前端可直接使用。
//! 内核重写时保留这些结构体即可维持 UI 契约不变。

use serde::{Deserialize, Serialize};

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct InstanceInfo {
    pub name: String,
    pub loader: String,
    pub mods: u32,
    pub updates: u32,
    pub display_name: String,
    pub minecraft: String,
    pub is_placeholder: bool,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct ModInfo {
    pub id: String,
    pub name: String,
    pub file: String,
    pub version: String,
    pub enabled: bool,
    pub path: String,
    pub size: i64,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct SavedServerInfo {
    pub name: String,
    pub address: String,
    pub instance: String,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct FavoriteInfo {
    pub id: String,
    pub kind: String,
    pub name: String,
    pub description: String,
}

#[derive(Clone, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct DataSourceSet {
    pub announcements: String,
    pub servers: String,
    pub recommendations: String,
    pub quotes: String,
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

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct VersionChoice {
    pub name: String,
    pub meta: String,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct VersionGroupInfo {
    pub label: String,
    pub count: u32,
    pub versions: Vec<VersionChoice>,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct LauncherState {
    pub app_version: String,
    pub player: String,
    pub account_type: String,
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
    pub selected_version: String,
    pub selected_instance: String,
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
pub struct TaskInfo {
    pub id: String,
    pub name: String,
    pub state: String,
    pub percent: f64,
    pub message: String,
    pub committing: bool,
    pub allowed_actions: Vec<String>,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct GameInfo {
    pub id: String,
    pub version: String,
    pub player: String,
    pub pid: u32,
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

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct AvatarInfo {
    pub image: String,
    pub label: String,
    pub fallback: bool,
    pub account: String,
    pub player: String,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct MuaProfileInfo {
    pub id: String,
    pub name: String,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct ContentItem {
    pub id: String,
    pub name: String,
    pub description: String,
    pub author: String,
    pub icon: String,
    pub downloads: String,
    pub tags: Vec<String>,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct ContentSearchResult {
    pub category: String,
    pub request_id: String,
    pub items: Vec<ContentItem>,
    pub error: String,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct ResourceVersionInfo {
    pub id: String,
    pub name: String,
    pub files: Vec<String>,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct ResourceVersionsInfo {
    pub request_id: String,
    pub token: String,
    pub game: String,
    pub loader: String,
    pub items: Vec<ResourceVersionInfo>,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct ResourceVersionsResult {
    pub ok: bool,
    pub result: Option<ResourceVersionsInfo>,
    pub error: String,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct VanillaVersionInfo {
    pub id: String,
    pub r#type: String,
    pub released: String,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct VanillaVersionsResult {
    pub ok: bool,
    pub items: Vec<VanillaVersionInfo>,
    pub error: String,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct LoaderListInfo {
    pub version: String,
    pub loader: String,
    pub versions: Vec<String>,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct LoaderListResult {
    pub ok: bool,
    pub result: Option<LoaderListInfo>,
    pub error: String,
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
    pub version: String,
    pub name: String,
    pub inherited: bool,
    pub settings: LaunchSettings,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct UninstallPreviewInfo {
    pub token: String,
    pub version: String,
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
    pub token: String,
    pub version: String,
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
    pub token: String,
    pub version: String,
    pub bytes: i64,
    pub changes: Vec<ContentChangeInfo>,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct PackPreviewInfo {
    pub token: String,
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

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct SavedInfo {
    pub message: String,
}

impl SavedInfo {
    pub fn new(message: impl Into<String>) -> Self {
        Self {
            message: message.into(),
        }
    }
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct LaunchResultInfo {
    pub ok: bool,
    pub message: String,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct ServerResultInfo {
    pub ok: bool,
    pub message: String,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct AuthResultInfo {
    pub ok: Option<bool>,
    pub pending: bool,
    pub message: String,
}

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct AccountSwitchResultInfo {
    pub account: String,
    pub needs_login: bool,
    pub message: String,
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

#[derive(Clone, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct ToastInfo {
    pub message: String,
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
}

impl ApiError {
    pub fn unsupported(command: &str) -> Self {
        Self {
            code: "UNSUPPORTED",
            message: format!("演示后端尚未实现 {command}，操作未执行。"),
            retryable: false,
        }
    }
}

impl TaskInfo {
    /// 可执行动作由后端统一计算；提交阶段禁止中断。
    pub fn actions(&self) -> Vec<String> {
        let actions: &[&str] = match self.state.as_str() {
            "succeeded" | "failed" | "cancelled" => &["delete"],
            _ if self.committing => &[],
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
            return Err(ApiError { code: "CONFLICT", message: "当前任务状态不允许此操作。".into(), retryable: false });
        }
        self.state = match action {
            "pause" => "paused",
            "resume" => "running",
            "cancel" => "cancelling",
            _ => self.state.as_str(),
        }.into();
        self.allowed_actions = self.actions();
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn cancellation_waits_for_cleanup() {
        let mut task = TaskInfo { state: "running".into(), ..Default::default() };
        task.apply_action("cancel").unwrap();
        assert_eq!(task.state, "cancelling");
        assert!(task.apply_action("delete").is_err());
    }

    #[test]
    fn committing_cannot_be_interrupted() {
        let mut task = TaskInfo { state: "running".into(), committing: true, ..Default::default() };
        assert!(task.apply_action("pause").is_err());
        assert!(task.apply_action("cancel").is_err());
        task.state = "failed".into();
        assert_eq!(task.actions(), vec!["delete"]);
    }
}
