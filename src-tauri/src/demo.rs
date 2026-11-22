//! 内核重写期间的演示数据。
//!
//! 全部集中在 `Data` 里，替换为真实实现时可以整块删除。

use crate::{launcher_config::LauncherConfig, model::*};
use std::collections::HashMap;
use std::time::{SystemTime, UNIX_EPOCH};
use time::{Duration, OffsetDateTime, format_description::well_known::Rfc3339};

#[derive(Debug)]
pub struct PlanRecord {
    pub kind: String,
    pub target_id: String,
    pub target_revision: u64,
    pub expires_at: OffsetDateTime,
}
#[derive(Clone, Copy)]
pub enum StateResource {
    Overview,
    Settings,
    Accounts,
    Instances,
    InstanceMods,
    Servers,
    Bookmarks,
    Tasks,
    Community,
}

pub struct ResourceRevisions {
    pub overview: u64,
    pub settings: u64,
    pub accounts: u64,
    pub instances: u64,
    pub instance_mods: u64,
    pub servers: u64,
    pub bookmarks: u64,
    pub tasks: u64,
    pub game_sessions: u64,
    pub community: u64,
}

impl Default for ResourceRevisions {
    fn default() -> Self {
        Self {
            overview: 1,
            settings: 1,
            accounts: 1,
            instances: 1,
            instance_mods: 1,
            servers: 1,
            bookmarks: 1,
            tasks: 1,
            game_sessions: 1,
            community: 1,
        }
    }
}

pub struct Data {
    pub epoch: String,
    pub revisions: ResourceRevisions,
    pub state: BackendState,
    pub tasks: Vec<TaskInfo>,
    pub games: Vec<GameSessionInfo>,
    pub community: CommunityData,
    pub avatar: AvatarInfo,
    pub backups: Vec<SaveBackupInfo>,
    pub log: String,
    pub content: Vec<ContentItem>,
    pub vanilla: Vec<VanillaVersionInfo>,
    pub plans: HashMap<String, PlanRecord>,
    next_plan_id: u64,
}

impl Data {
    pub fn demo() -> Self {
        Self {
            epoch: SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .map(|duration| format!("epoch-{}", duration.as_nanos()))
                .unwrap_or_else(|_| "epoch-demo".into()),
            revisions: ResourceRevisions::default(),
            state: demo_state(),
            tasks: demo_tasks(),
            games: vec![GameSessionInfo {
                session_id: "session-demo-running".into(),
                instance_id: "instance-mcug-1211".into(),
                account_id: "account-demo-microsoft".into(),
                status: "running".into(),
                pid: Some(24188),
                exit_code: None,
                error: None,
            }],
            community: demo_community(),
            avatar: avatar_for("XiaoMu", "microsoft"),
            backups: vec![
                SaveBackupInfo {
                    id: "b1".into(),
                    created: "2026-11-12 21:04".into(),
                    worlds: vec!["新世界".into(), "SkyBlock".into()],
                },
                SaveBackupInfo {
                    id: "b2".into(),
                    created: "2026-11-10 19:22".into(),
                    worlds: vec!["新世界".into()],
                },
            ],
            log: [
                "[2026-11-14 20:12:04] launcher 0.22.3 started",
                "[2026-11-14 20:12:05] config loaded: D:/Games/Minecraft/MCUG",
                "[2026-11-14 20:12:06] java 21.0.4 detected",
                "[2026-11-14 20:12:09] account: microsoft · XiaoMu",
                "[2026-11-14 20:12:12] task queue recovered: 1 running, 1 paused",
            ]
            .join("\n"),
            content: demo_content(),
            vanilla: demo_vanilla(),
            plans: HashMap::new(),
            next_plan_id: 1,
        }
    }

    pub fn snapshot(&self) -> BackendState {
        self.state.clone()
    }

    pub fn mutating_snapshot(&mut self, resources: &[StateResource]) -> BackendState {
        for resource in resources {
            self.touch(*resource);
        }
        self.snapshot()
    }

    pub fn resource_snapshot<T: serde::Serialize>(
        &self,
        revision: u64,
        data: T,
    ) -> ResourceSnapshot {
        ResourceSnapshot {
            epoch: self.epoch.clone(),
            revision,
            data: serde_json::to_value(data).unwrap_or(serde_json::Value::Null),
        }
    }

    pub fn touch(&mut self, resource: StateResource) {
        let revision = match resource {
            StateResource::Overview => &mut self.revisions.overview,
            StateResource::Settings => &mut self.revisions.settings,
            StateResource::Accounts => &mut self.revisions.accounts,
            StateResource::Instances => &mut self.revisions.instances,
            StateResource::InstanceMods => &mut self.revisions.instance_mods,
            StateResource::Servers => &mut self.revisions.servers,
            StateResource::Bookmarks => &mut self.revisions.bookmarks,
            StateResource::Tasks => &mut self.revisions.tasks,
            StateResource::Community => &mut self.revisions.community,
        };
        *revision = revision.saturating_add(1);
    }

    pub(crate) fn replace_instance_catalog(&mut self, catalog: InstanceCatalogInfo) {
        self.state.instances = catalog.items;
        self.state.version_groups = catalog.version_groups;
        self.state.mods.clear();
        if self.state.selected_instance_id.as_ref().is_some_and(|id| {
            !self.state.instances.iter().any(|instance| &instance.id == id)
        }) {
            self.state.selected_instance_id = None;
            self.touch(StateResource::Overview);
        }
        self.touch(StateResource::Instances);
        self.touch(StateResource::InstanceMods);
    }

    pub fn ensure_settings_revision(&self, expected: u64) -> Result<(), ApiError> {
        if self.revisions.settings == expected {
            Ok(())
        } else {
            Err(ApiError {
                code: "CONFLICT".into(),
                message: "设置已在其他位置发生变化，请刷新后重试。".into(),
                retryable: true,
                field_errors: HashMap::new(),
            })
        }
    }

    pub fn select_account(&mut self, account_id: &str) -> Result<AccountInfo, ApiError> {
        let account = self
            .state
            .accounts
            .iter()
            .find(|account| account.id == account_id)
            .cloned()
            .ok_or_else(|| ApiError {
                code: "NOT_FOUND".into(),
                message: "账号不存在。".into(),
                retryable: false,
                field_errors: HashMap::new(),
            })?;
        self.state.active_account_id = Some(account.id.clone());
        Ok(account)
    }

    pub fn forget_account(&mut self, account_id: &str) -> Result<AccountInfo, ApiError> {
        let removable = self
            .state
            .accounts
            .iter()
            .any(|account| account.id == account_id && account.kind != "offline");
        if !removable {
            return Err(ApiError {
                code: "NOT_FOUND".into(),
                message: "可移除的账号不存在。".into(),
                retryable: false,
                field_errors: HashMap::new(),
            });
        }
        self.state
            .accounts
            .retain(|account| account.id != account_id);
        let fallback = self
            .state
            .accounts
            .iter()
            .find(|account| account.kind == "offline")
            .cloned()
            .ok_or_else(|| ApiError {
                code: "INTERNAL_ERROR".into(),
                message: "离线账号缺失。".into(),
                retryable: false,
                field_errors: HashMap::new(),
            })?;
        if self.state.active_account_id.as_deref() == Some(account_id) {
            self.state.active_account_id = Some(fallback.id.clone());
        }
        Ok(fallback)
    }

    pub fn create_uninstall_plan(
        &mut self,
        instance_id: &str,
        ttl: Duration,
    ) -> Result<UninstallPreviewInfo, ApiError> {
        let instance = self
            .state
            .instances
            .iter()
            .find(|item| item.id == instance_id)
            .cloned()
            .ok_or_else(|| ApiError {
                code: "NOT_FOUND".into(),
                message: "实例不存在。".into(),
                retryable: false,
                field_errors: HashMap::new(),
            })?;
        let plan_id = format!("plan-uninstall-{}", self.next_plan_id);
        self.next_plan_id = self.next_plan_id.saturating_add(1);
        let expires_at = OffsetDateTime::now_utc() + ttl;
        self.plans.insert(
            plan_id.clone(),
            PlanRecord {
                kind: "uninstall".into(),
                target_id: instance_id.into(),
                target_revision: self.revisions.instances,
                expires_at,
            },
        );
        Ok(UninstallPreviewInfo {
            plan_id,
            target_id: instance_id.into(),
            target_revision: self.revisions.instances as i64,
            expires_at: expires_at.format(&Rfc3339).unwrap_or_default(),
            name: instance.display_name,
            notice: "执行前会再次检查目标实例及修订号。".into(),
            paths: vec![format!("{}/versions/{}", self.state.config.root(), instance.name)],
            shared: false,
        })
    }

    pub fn validate_plan(
        &self,
        plan_id: &str,
        kind: &str,
        current_revision: u64,
    ) -> Result<&PlanRecord, ApiError> {
        let plan = self.plans.get(plan_id).ok_or_else(|| ApiError {
            code: "PLAN_EXPIRED".into(),
            message: "操作计划不存在或已经失效。".into(),
            retryable: false,
            field_errors: HashMap::new(),
        })?;
        if plan.expires_at <= OffsetDateTime::now_utc() {
            return Err(ApiError {
                code: "PLAN_EXPIRED".into(),
                message: "操作计划已经过期，请重新预览。".into(),
                retryable: false,
                field_errors: HashMap::new(),
            });
        }
        let target_exists = plan.kind != "uninstall"
            || self
                .state
                .instances
                .iter()
                .any(|instance| instance.id == plan.target_id);
        if plan.kind != kind || plan.target_revision != current_revision || !target_exists {
            return Err(ApiError {
                code: "CONFLICT".into(),
                message: "目标已发生变化，请重新预览。".into(),
                retryable: true,
                field_errors: HashMap::new(),
            });
        }
        Ok(plan)
    }

    pub fn avatar_for_current(&self) -> AvatarInfo {
        let account = self
            .state
            .active_account_id
            .as_deref()
            .and_then(|id| self.state.accounts.iter().find(|account| account.id == id));
        account
            .map(|account| avatar_for(&account.player, &account.kind))
            .unwrap_or_else(|| avatar_for("", "offline"))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn state_revision_changes_only_after_mutation() {
        let mut data = Data::demo();
        let first = data.revisions.settings;
        data.mutating_snapshot(&[StateResource::Settings]);
        assert_eq!(data.revisions.settings, first + 1);
        assert_eq!(data.revisions.overview, 1);
        assert_eq!(data.revisions.tasks, 1);
        assert_eq!(
            data.ensure_settings_revision(first).unwrap_err().code,
            "CONFLICT"
        );

        data.touch(StateResource::Tasks);
        assert_eq!(data.revisions.tasks, 2);
        assert_eq!(data.revisions.settings, first + 1);
    }

    #[test]
    fn accounts_are_selected_and_removed_by_stable_id() {
        let mut data = Data::demo();
        assert_eq!(
            data.select_account("account-demo-offline").unwrap().kind,
            "offline"
        );
        assert_eq!(
            data.state.active_account_id.as_deref(),
            Some("account-demo-offline")
        );
        assert_eq!(
            data.select_account("missing").unwrap_err().code,
            "NOT_FOUND"
        );

        data.select_account("account-demo-microsoft").unwrap();
        let fallback = data.forget_account("account-demo-microsoft").unwrap();
        assert_eq!(fallback.id, "account-demo-offline");
        assert_eq!(
            data.state.active_account_id.as_deref(),
            Some("account-demo-offline")
        );
        assert!(data.forget_account("account-demo-offline").is_err());
    }

    #[test]
    fn operation_plans_expire_and_detect_target_changes() {
        let mut data = Data::demo();
        let valid = data
            .create_uninstall_plan("instance-mcug-1211", Duration::minutes(10))
            .unwrap();
        assert_eq!(
            data.validate_plan(&valid.plan_id, "uninstall", data.revisions.instances)
                .unwrap()
                .target_id,
            valid.target_id
        );

        data.revisions.instances = data.revisions.instances.saturating_add(1);
        assert_eq!(
            data.validate_plan(&valid.plan_id, "uninstall", data.revisions.instances)
                .unwrap_err()
                .code,
            "CONFLICT"
        );

        let expired = data
            .create_uninstall_plan("instance-mcug-1211", Duration::seconds(-1))
            .unwrap();
        assert_eq!(
            data.validate_plan(&expired.plan_id, "uninstall", data.revisions.instances)
                .unwrap_err()
                .code,
            "PLAN_EXPIRED"
        );
    }
}

pub fn avatar_for(player: &str, account: &str) -> AvatarInfo {
    let label = match account {
        "microsoft" => "正版账号 · 在线验证",
        "mua" => "MUA 账号 · 外置登录",
        _ => "离线账号 · 本地启动",
    };
    AvatarInfo {
        image: "/MCUGLogo.svg".into(),
        label: label.into(),
        fallback: true,
        account: account.into(),
        player: player.into(),
    }
}

fn demo_state() -> BackendState {
    let mut config = LauncherConfig::default();
    config.game_roots = vec!["D:/Games/Minecraft/MCUG".into(), "E:/Minecraft/Backup".into()];
    config.java = "C:/Program Files/Eclipse Adoptium/jdk-21.0.4.7-hotspot/bin/javaw.exe".into();
    BackendState {
        app_version: "0.22.3".into(),
        active_account_id: Some("account-demo-microsoft".into()),
        accounts: vec![
            AccountInfo {
                id: "account-demo-microsoft".into(),
                kind: "microsoft".into(),
                player: "XiaoMu".into(),
                status: "ready".into(),
            },
            AccountInfo {
                id: "account-demo-offline".into(),
                kind: "offline".into(),
                player: "LocalPlayer".into(),
                status: "ready".into(),
            },
        ],
        config,
        has_community_key: true,
        has_admin_key: true,
        skin_station: String::new(),
        cape: String::new(),
        selected_instance_id: Some("instance-mcug-1211".into()),
        instances: vec![
            InstanceInfo {
                id: "instance-mcug-1211".into(),
                name: "MCUG-1.21.1".into(),
                loader: "Fabric 0.16.9".into(),
                mods: 42,
                updates: 3,
                display_name: "MCUG 生存服".into(),
                minecraft: "1.21.1".into(),
                is_placeholder: false,
            },
            InstanceInfo {
                id: "instance-create-1201".into(),
                name: "Create-1.20.1".into(),
                loader: "Forge 47.2.0".into(),
                mods: 18,
                updates: 0,
                display_name: "机械动力".into(),
                minecraft: "1.20.1".into(),
                is_placeholder: false,
            },
            InstanceInfo {
                id: "instance-vanilla-1206".into(),
                name: "Vanilla-1.20.6".into(),
                loader: "原版".into(),
                mods: 0,
                updates: 0,
                display_name: "原版 1.20.6".into(),
                minecraft: "1.20.6".into(),
                is_placeholder: false,
            },
        ],
        mods: vec![
            mod_item(
                "sodium",
                "Sodium",
                "sodium-fabric-0.6.13.jar",
                "0.6.13",
                true,
                1_238_272,
            ),
            mod_item(
                "iris",
                "Iris Shaders",
                "iris-1.8.8.jar",
                "1.8.8",
                true,
                2_470_016,
            ),
            mod_item(
                "fabric-api",
                "Fabric API",
                "fabric-api-0.115.0.jar",
                "0.115.0",
                true,
                2_023_424,
            ),
            mod_item(
                "lithium",
                "Lithium",
                "lithium-fabric-0.15.3.jar",
                "0.15.3",
                true,
                1_048_576,
            ),
            mod_item("jei", "JEI", "jei-1.21.1.jar", "19.21.0", false, 1_572_864),
            mod_item(
                "xaero",
                "Xaero's Minimap",
                "xaeros-minimap-24.2.0.jar",
                "24.2.0",
                true,
                1_887_436,
            ),
        ],
        servers: vec![
            SavedServerInfo {
                id: "server-mcug-survival".into(),
                name: "MCUG 生存服".into(),
                address: "play.mcug.cn:25565".into(),
                instance_id: Some("instance-mcug-1211".into()),
            },
            SavedServerInfo {
                id: "server-creative".into(),
                name: "创造服".into(),
                address: "creative.example.com:25566".into(),
                instance_id: None,
            },
        ],
        favorites: vec![
            favorite("sodium", "mod", "Sodium", "渲染优化 · 大幅提升帧率"),
            favorite(
                "iris",
                "mod",
                "Iris Shaders",
                "光影加载器，兼容 OptiFine 光影包",
            ),
            favorite("create", "mod", "Create", "机械动力 · 自动化与齿轮"),
            favorite("terralith", "mod", "Terralith", "地形生成扩展"),
        ],
        version_groups: vec![
            VersionGroupInfo {
                label: "1.21".into(),
                count: 3,
                versions: vec![
                    VersionChoice {
                        name: "1.21.1".into(),
                        meta: "正式版".into(),
                    },
                    VersionChoice {
                        name: "1.21".into(),
                        meta: "正式版".into(),
                    },
                ],
            },
            VersionGroupInfo {
                label: "1.20".into(),
                count: 4,
                versions: vec![
                    VersionChoice {
                        name: "1.20.6".into(),
                        meta: "正式版".into(),
                    },
                    VersionChoice {
                        name: "1.20.1".into(),
                        meta: "正式版".into(),
                    },
                ],
            },
        ],
        default_data_sources: DataSourceSet::demo(),
    }
}

fn mod_item(id: &str, name: &str, file: &str, version: &str, enabled: bool, size: i64) -> ModInfo {
    ModInfo {
        id: id.into(),
        name: name.into(),
        file: file.into(),
        version: version.into(),
        enabled,
        path: format!("mods/{file}"),
        size,
    }
}

fn favorite(id: &str, kind: &str, name: &str, description: &str) -> FavoriteInfo {
    FavoriteInfo {
        id: id.into(),
        kind: kind.into(),
        name: name.into(),
        description: description.into(),
    }
}

fn demo_tasks() -> Vec<TaskInfo> {
    vec![
        TaskInfo {
            id: "t1".into(),
            name: "下载 Sodium 0.6.13".into(),
            kind: "downloadContent".into(),
            instance_id: Some("instance-mcug-1211".into()),
            status: "running".into(),
            phase: "downloading".into(),
            progress: Some(TaskProgressInfo {
                completed: 13_002_342,
                total: Some(28_206_694),
                unit: "bytes".into(),
            }),
            allowed_actions: vec![],
            error: None,
            result: None,
            created_at: "2026-11-15T01:00:00Z".into(),
            updated_at: "2026-11-15T01:02:00Z".into(),
        },
        TaskInfo {
            id: "t2".into(),
            name: "安装 Fabric 0.16.9".into(),
            kind: "installLoader".into(),
            instance_id: Some("instance-mcug-1211".into()),
            status: "queued".into(),
            phase: "waiting".into(),
            progress: None,
            allowed_actions: vec![],
            error: None,
            result: None,
            created_at: "2026-11-15T01:01:00Z".into(),
            updated_at: "2026-11-15T01:01:00Z".into(),
        },
        TaskInfo {
            id: "t3".into(),
            name: "更新 Create 6.0.4".into(),
            kind: "updateContent".into(),
            instance_id: Some("instance-create-1201".into()),
            status: "paused".into(),
            phase: "downloading".into(),
            progress: Some(TaskProgressInfo {
                completed: 72,
                total: Some(100),
                unit: "items".into(),
            }),
            allowed_actions: vec![],
            error: None,
            result: None,
            created_at: "2026-11-15T00:50:00Z".into(),
            updated_at: "2026-11-15T01:03:00Z".into(),
        },
    ]
}

fn demo_community() -> CommunityData {
    CommunityData {
        announcements: vec![
            AnnouncementInfo {
                id: "a1".into(),
                title: "0.22.3 原生界面已上线".into(),
                body: "启动器已切换到原生 Avalonia 界面，账号、下载、实例与社区功能全部保留。遇到问题请在群里反馈。".into(),
                tag: "公告".into(),
                author: "MCUG 管理组".into(),
                published_at: Some("2026-11-08T10:00:00+08:00".into()),
                image_url: String::new(),
                markdown: "# 0.22.3 原生界面已上线\n\n启动器已切换到原生界面，**账号、下载、实例与社区功能全部保留**。\n\n- 微软 / MUA / 离线账号\n- 模组与整合包安装\n- 实例克隆、导出与卸载\n\n> 遇到问题请在群里反馈。".into(),
            },
            AnnouncementInfo {
                id: "a2".into(),
                title: "周末生存服活动".into(),
                body: "周六晚八点开启双倍经验，主城集合。".into(),
                tag: "活动".into(),
                author: "MCUG 管理组".into(),
                published_at: Some("2026-11-06T18:30:00+08:00".into()),
                image_url: String::new(),
                markdown: String::new(),
            },
        ],
        recommendations: vec![
            recommendation("r1", "server", "MCUG 生存服", "群友推荐", "长期稳定的原版生存服，支持基岩版互通。", "play.mcug.cn:25565"),
            recommendation("r2", "server", "机械动力服", "群友推荐", "Create 主题服务器，适合喜欢自动化的玩家。", "create.example.com:25565"),
            recommendation("r3", "material", "Stay True", "群友推荐", "清爽的 1.21 材质包，保留原版风格。", ""),
            recommendation("r4", "shader", "Complementary Reimagined", "群友推荐", "性能与画质平衡的光影包。", ""),
        ],
        quotes: vec![
            QuoteInfo {
                id: "q1".into(),
                type_: "text".into(),
                text: "来都来了，先挖三格。".into(),
                author: "群友".into(),
                source: "群聊".into(),
                image_url: String::new(),
            },
            QuoteInfo {
                id: "q2".into(),
                type_: "text".into(),
                text: "这箱子里的东西不是你的，是我的。".into(),
                author: "服主".into(),
                source: "服务器公告".into(),
                image_url: String::new(),
            },
            QuoteInfo {
                id: "q3".into(),
                type_: "text".into(),
                text: "今晚八点，主城集合。".into(),
                author: "管理组".into(),
                source: "活动通知".into(),
                image_url: String::new(),
            },
        ],
    }
}

fn recommendation(
    id: &str,
    kind: &str,
    name: &str,
    author: &str,
    description: &str,
    url: &str,
) -> RecommendationInfo {
    RecommendationInfo {
        id: id.into(),
        type_: kind.into(),
        name: name.into(),
        author: author.into(),
        description: description.into(),
        tags: vec![],
        version: String::new(),
        size: String::new(),
        url: url.into(),
    }
}

fn demo_content() -> Vec<ContentItem> {
    vec![
        // ---------- 模组 ----------
        content(
            "mod",
            "sodium",
            "Sodium",
            "现代渲染引擎，显著提升帧率并减少卡顿。",
            "CaffeineMC",
            "38.2M",
            &["客户端", "优化"],
            &["1.21.1", "1.20.6", "1.20.1"],
            &["fabric", "neoforge"],
        ),
        content(
            "mod",
            "iris",
            "Iris Shaders",
            "让 Fabric / NeoForge 实例加载光影包的光影加载器模组。",
            "IrisTeam",
            "12.7M",
            &["客户端", "优化"],
            &["1.21.1", "1.20.6", "1.20.1"],
            &["fabric", "neoforge"],
        ),
        content(
            "mod",
            "create",
            "Create",
            "以旋转动力为核心的机械自动化模组。",
            "simibubi",
            "21.4M",
            &["客户端或服务端", "科技", "自动化"],
            &["1.21.1", "1.20.1"],
            &["forge", "neoforge", "fabric"],
        ),
        content(
            "mod",
            "jei",
            "Just Enough Items",
            "查看物品配方与用途。",
            "mezz",
            "45.1M",
            &["客户端或服务端", "工具", "库"],
            &["1.21.1", "1.20.6", "1.20.1"],
            &["forge", "neoforge", "fabric"],
        ),
        content(
            "mod",
            "lithium",
            "Lithium",
            "服务端与客户端通用性能优化。",
            "CaffeineMC",
            "9.8M",
            &["客户端或服务端", "优化"],
            &["1.21.1", "1.20.1"],
            &["fabric", "quilt"],
        ),
        content(
            "mod",
            "terralith",
            "Terralith",
            "超过 100 种新生物群系的地形扩展。",
            "Starmute",
            "6.3M",
            &["客户端或服务端", "世界生成", "冒险"],
            &["1.21.1", "1.20.1"],
            &["fabric", "forge", "neoforge"],
        ),
        // ---------- 整合包 ----------
        content(
            "modpack",
            "fabulously-optimized",
            "Fabulously Optimized",
            "以性能优化为主的轻量整合包，装好即用。",
            "Robotkoer",
            "8.1M",
            &["客户端", "优化"],
            &["1.21.1", "1.20.1"],
            &["fabric"],
        ),
        content(
            "modpack",
            "all-the-mods-10",
            "All the Mods 10",
            "包罗万象的大型科技与魔法整合包。",
            "ATMTeam",
            "4.6M",
            &["客户端或服务端", "科技", "冒险"],
            &["1.21.1"],
            &["neoforge"],
        ),
        // ---------- 资源包 ----------
        content(
            "resourcepack",
            "faithful-32x",
            "Faithful 32x",
            "原版材质的 32× 高分辨率重制。",
            "Faithful Team",
            "26.9M",
            &["原版风格", "高清"],
            &["1.21.1", "1.20.6", "1.20.1"],
            &[],
        ),
        content(
            "resourcepack",
            "stay-true",
            "Stay True",
            "保留原版配色与手感的高清材质包。",
            "Yuruze",
            "5.4M",
            &["原版风格"],
            &["1.21.1", "1.20.1"],
            &[],
        ),
        // ---------- 光影包 ----------
        content(
            "shader",
            "complementary-unbound",
            "Complementary Unbound",
            "画质与性能平衡的通用光影，日夜与天气表现完整。",
            "EminGT",
            "19.3M",
            &["均衡", "Iris", "OptiFine"],
            &["1.21.1", "1.20.6", "1.20.1"],
            &["iris", "optifine"],
        ),
        content(
            "shader",
            "bsl-shaders",
            "BSL Shaders",
            "色彩浓郁、光照柔和的老牌光影。",
            "CaptTatsu",
            "14.2M",
            &["写实", "Iris", "OptiFine"],
            &["1.21.1", "1.20.1"],
            &["iris", "optifine"],
        ),
        content(
            "shader",
            "photon",
            "Photon Shader",
            "以体积光和大气散射见长，只走 Iris。",
            "SixthSurge",
            "6.8M",
            &["写实", "Iris"],
            &["1.21.1", "1.20.1"],
            &["iris"],
        ),
        content(
            "shader",
            "seus-renewed",
            "SEUS Renewed",
            "经典写实光影，依赖 OptiFine 的光照特性。",
            "Sonic Ether",
            "11.5M",
            &["写实", "OptiFine"],
            &["1.21.1", "1.20.1"],
            &["optifine"],
        ),
        // ---------- 数据包 ----------
        content(
            "datapack",
            "incendium",
            "Incendium",
            "把下界改造成全新维度的结构数据包。",
            "Starmute",
            "3.2M",
            &["世界生成", "冒险"],
            &["1.21.1", "1.20.1"],
            &[],
        ),
        content(
            "datapack",
            "vanilla-tweaks",
            "Vanilla Tweaks",
            "大量可自由组合的原版机制微调。",
            "Vanilla Tweaks",
            "7.7M",
            &["工具", "原版"],
            &["1.21.1", "1.20.6", "1.20.1"],
            &[],
        ),
    ]
}

fn content(
    category: &str,
    id: &str,
    name: &str,
    description: &str,
    author: &str,
    downloads: &str,
    tags: &[&str],
    game_versions: &[&str],
    loaders: &[&str],
) -> ContentItem {
    ContentItem {
        id: id.into(),
        category: category.into(),
        name: name.into(),
        description: description.into(),
        author: author.into(),
        icon: String::new(),
        downloads: downloads.into(),
        tags: tags.iter().map(|tag| (*tag).to_string()).collect(),
        game_versions: game_versions.iter().map(|v| (*v).to_string()).collect(),
        loaders: loaders.iter().map(|v| (*v).to_string()).collect(),
    }
}

fn demo_vanilla() -> Vec<VanillaVersionInfo> {
    [
        ("1.21.1", "release", "2024-08-08"),
        ("1.21", "release", "2024-06-13"),
        ("1.20.6", "release", "2024-04-29"),
        ("1.20.4", "release", "2023-12-07"),
        ("1.20.1", "release", "2023-06-12"),
        ("1.19.4", "release", "2023-03-14"),
        ("24w45a", "snapshot", "2024-11-06"),
        ("24w44a", "snapshot", "2024-10-30"),
        ("b1.7.3", "old_beta", "2011-07-08"),
        ("a1.2.6", "old_alpha", "2010-12-02"),
    ]
    .into_iter()
    .map(|(id, kind, released)| VanillaVersionInfo {
        id: id.into(),
        type_: kind.into(),
        released: released.into(),
    })
    .collect()
}
