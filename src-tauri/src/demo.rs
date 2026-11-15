//! 内核重写期间的演示数据。
//!
//! 全部集中在 `Data` 里，替换为真实实现时可以整块删除。

use crate::model::*;

pub struct Data {
    pub state: LauncherState,
    pub tasks: Vec<TaskInfo>,
    pub games: Vec<GameInfo>,
    pub community: CommunityData,
    pub avatar: AvatarInfo,
    pub java: Vec<JavaInfo>,
    pub backups: Vec<SaveBackupInfo>,
    pub log: String,
    pub content: Vec<ContentItem>,
    pub vanilla: Vec<VanillaVersionInfo>,
    pub updates: Vec<ContentUpdateInfo>,
}

impl Data {
    pub fn demo() -> Self {
        Self {
            state: demo_state(),
            tasks: demo_tasks(),
            games: vec![GameInfo {
                id: "g1".into(),
                version: "MCUG-1.21.1".into(),
                player: "XiaoMu".into(),
                pid: 24188,
            }],
            community: demo_community(),
            avatar: avatar_for("XiaoMu", "microsoft"),
            java: demo_java(),
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
            updates: demo_updates(),
        }
    }

    pub fn snapshot(&self) -> LauncherState {
        self.state.clone()
    }

    pub fn avatar_for_current(&self) -> AvatarInfo {
        avatar_for(&self.state.player, &self.state.account_type)
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

fn demo_state() -> LauncherState {
    LauncherState {
        app_version: "0.22.3".into(),
        player: "XiaoMu".into(),
        account_type: "microsoft".into(),
        root: "D:/Games/Minecraft/MCUG".into(),
        game_roots: vec![
            "D:/Games/Minecraft/MCUG".into(),
            "E:/Minecraft/Backup".into(),
        ],
        java: "C:/Program Files/Eclipse Adoptium/jdk-21.0.4.7-hotspot/bin/javaw.exe".into(),
        width: 1280,
        height: 720,
        ram: 8192,
        auto_memory: true,
        anonymous: false,
        has_community_key: true,
        has_admin_key: true,
        skin_station: String::new(),
        cape: String::new(),
        selected_version: "MCUG-1.21.1".into(),
        selected_instance: "MCUG-1.21.1".into(),
        instances: vec![
            InstanceInfo {
                name: "MCUG-1.21.1".into(),
                loader: "Fabric 0.16.9".into(),
                mods: 42,
                updates: 3,
                display_name: "MCUG 生存服".into(),
                minecraft: "1.21.1".into(),
                is_placeholder: false,
            },
            InstanceInfo {
                name: "Create-1.20.1".into(),
                loader: "Forge 47.2.0".into(),
                mods: 18,
                updates: 0,
                display_name: "机械动力".into(),
                minecraft: "1.20.1".into(),
                is_placeholder: false,
            },
            InstanceInfo {
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
                name: "MCUG 生存服".into(),
                address: "play.mcug.cn:25565".into(),
                instance: "MCUG-1.21.1".into(),
            },
            SavedServerInfo {
                name: "创造服".into(),
                address: "creative.example.com:25566".into(),
                instance: String::new(),
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
        data_sources: DataSourceSet::demo(),
        default_data_sources: DataSourceSet::demo(),
        custom_data_sources: DataSourceSet::default(),
        data_source_mode: "default".into(),
        auto_chinese: true,
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
            state: "running".into(),
            percent: 46.0,
            message: "下载中 12.4 MB / 26.9 MB".into(),
            committing: false,
            allowed_actions: vec![],
        },
        TaskInfo {
            id: "t2".into(),
            name: "安装 Fabric 0.16.9".into(),
            state: "queued".into(),
            percent: 0.0,
            message: "等待中".into(),
            committing: false,
            allowed_actions: vec![],
        },
        TaskInfo {
            id: "t3".into(),
            name: "更新 Create 6.0.4".into(),
            state: "paused".into(),
            percent: 72.0,
            message: "已暂停".into(),
            committing: false,
            allowed_actions: vec![],
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
                r#type: "text".into(),
                text: "来都来了，先挖三格。".into(),
                author: "群友".into(),
                source: "群聊".into(),
                image_url: String::new(),
            },
            QuoteInfo {
                id: "q2".into(),
                r#type: "text".into(),
                text: "这箱子里的东西不是你的，是我的。".into(),
                author: "服主".into(),
                source: "服务器公告".into(),
                image_url: String::new(),
            },
            QuoteInfo {
                id: "q3".into(),
                r#type: "text".into(),
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
        r#type: kind.into(),
        name: name.into(),
        author: author.into(),
        description: description.into(),
        tags: vec![],
        version: String::new(),
        size: String::new(),
        url: url.into(),
    }
}

fn demo_java() -> Vec<JavaInfo> {
    vec![
        JavaInfo {
            path: "C:/Program Files/Eclipse Adoptium/jdk-21.0.4.7-hotspot/bin/javaw.exe".into(),
            major: 21,
            arch: "x64".into(),
            vendor: "Eclipse Adoptium".into(),
            error: String::new(),
        },
        JavaInfo {
            path: "C:/Program Files/Java/jdk-17/bin/javaw.exe".into(),
            major: 17,
            arch: "x64".into(),
            vendor: "Oracle".into(),
            error: String::new(),
        },
        JavaInfo {
            path: "C:/Program Files (x86)/Java/jre1.8/bin/javaw.exe".into(),
            major: 8,
            arch: "x86".into(),
            vendor: "Oracle".into(),
            error: String::new(),
        },
    ]
}

fn demo_content() -> Vec<ContentItem> {
    vec![
        content(
            "sodium",
            "Sodium",
            "现代渲染引擎，显著提升帧率并减少卡顿。",
            "CaffeineMC",
            "38.2M",
        ),
        content(
            "iris",
            "Iris Shaders",
            "兼容 OptiFine 光影包的光影加载器。",
            "IrisTeam",
            "12.7M",
        ),
        content(
            "create",
            "Create",
            "以旋转动力为核心的机械自动化模组。",
            "simibubi",
            "21.4M",
        ),
        content(
            "jei",
            "Just Enough Items",
            "查看物品配方与用途。",
            "mezz",
            "45.1M",
        ),
        content(
            "lithium",
            "Lithium",
            "服务端与客户端通用性能优化。",
            "CaffeineMC",
            "9.8M",
        ),
        content(
            "terralith",
            "Terralith",
            "超过 100 种新生物群系的地形扩展。",
            "Starmute",
            "6.3M",
        ),
    ]
}

fn content(id: &str, name: &str, description: &str, author: &str, downloads: &str) -> ContentItem {
    ContentItem {
        id: id.into(),
        name: name.into(),
        description: description.into(),
        author: author.into(),
        icon: String::new(),
        downloads: downloads.into(),
        tags: vec![],
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
        r#type: kind.into(),
        released: released.into(),
    })
    .collect()
}

fn demo_updates() -> Vec<ContentUpdateInfo> {
    [
        ("sodium", "Sodium", "0.6.11", "0.6.13"),
        ("iris", "Iris Shaders", "1.8.5", "1.8.8"),
        ("fabric-api", "Fabric API", "0.114.0", "0.115.0"),
    ]
    .into_iter()
    .map(|(id, name, before, after)| ContentUpdateInfo {
        id: id.into(),
        name: name.into(),
        before: before.into(),
        after: after.into(),
        available: true,
        error: String::new(),
    })
    .collect()
}
