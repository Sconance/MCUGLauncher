# 前后端契约（Tauri 命令与事件）

> 本文记录当前 V2 接口，是前后端实现的基准。设计原则与迁移验收记录见 [BACKEND_V2.md](BACKEND_V2.md)。

前端只通过 `@tauri-apps/api/core` 的 `invoke` 调用后端：**所有读取与写入都是命令**。前端不依赖事件推送；需要刷新时由页面在命令返回后调用对应的查询命令。事件仅作为后端可选的通知能力保留。

本文档是唯一的契约清单：改动命令名、参数或事件时必须同步修改本文档、`src/api/commands/*.ts` 与 `src/types/launcher.ts`。

第三方前端整体替换前端时使用的对外接口（`kernel_manifest`、本地目录加载、`window.__MCUGL__`、版本兼容规则）见 [KERNEL_API_V1.md](KERNEL_API_V1.md)（协议标识 `mcugl.kernel/v1`）。本文档仍是方法语义的基准。

第三方前端的安装与分发格式（`.mcuglf`）、导入/切换/卸载命令以及它们各自的错误码见 [FRONTEND_PACKAGE.md](FRONTEND_PACKAGE.md)。这四个命令是宿主管理接口，不属于内核方法表。

DTO 的结构以 [schemas/](../schemas/) 下的领域 Schema 为单一机器可读来源；TypeScript 通过生成文件消费 Schema，Rust 通过 Typify 在编译期生成类型，本文档只描述命令语义。

## 当前演示能力限制

安装、启动、在线登录、系统交互、网络探测、备份恢复和部分计划执行尚未接入真实内核，相应命令返回结构化 `UNSUPPORTED` 错误，操作不会执行。表中的成功类型定义真实适配器接入后必须遵守的返回契约，不表示演示后端已具备该能力。

任务状态使用小写 `queued`、`running`、`paused`、`cancelling`、`succeeded`、`failed`、`cancelled`；`allowedActions` 是后端计算的可执行动作。提交阶段禁止中断，取消请求不等于清理完成，终态才可删除。未知任务返回 `NOT_FOUND`，非法转换返回 `CONFLICT`。演示队列仅在内存中保存，重启重置。

`TaskInfo` 包含 `kind`、可空 `instanceId`、`status`、`phase`、可空结构化 `progress`、`allowedActions`、可空 `error/result` 以及 UTC RFC3339 的 `createdAt/updatedAt`。未知总量使用 `progress.total = null`，前端显示不确定进度，不伪造百分比。

## 1. 约定

- **命令名**：`snake_case`，动词在前，例如 `get_overview`、`save_server`。
- **参数**：前端传 `camelCase` 键（Tauri 默认映射到 Rust 的 `snake_case` 参数），可省略的参数在 Rust 侧声明为 `Option<T>`。
- **返回值**：`camelCase` 的强类型 DTO，与 `src/types/launcher.ts` 一致；失败通过 `Err(ApiError)` 抛出，前端包装为 `BackendError`。
- **错误**：统一为 `{ code, message, retryable, fieldErrors? }`。表单校验失败时 `fieldErrors` 返回字段到错误消息的映射。当前错误码包括 `VALIDATION_ERROR`、`NOT_FOUND`、`CONFLICT`、`BUSY`、`AUTH_REQUIRED`、`NETWORK_ERROR`、`IO_ERROR`、`PLAN_EXPIRED`、`INTERNAL_ERROR` 与 `UNSUPPORTED`。
- **写入结果**：写命令返回 `{ message, changed, entityIds }`；`changed` 列出受影响的资源键，前端随后按资源键读取对应独立资源。
- **事件（可选）**：后端可以继续 `emit`，但前端当前不订阅；内核重写时不需要实现事件。

前端统一入口：

```ts
import { call } from "../api/invoke";
const overview = await call<ResourceSnapshot<LauncherOverview>>("get_overview");
```

## 2. 命令

### 状态

| 命令 | 参数 | 返回 | 说明 |
| --- | --- | --- | --- |
| `get_overview` | — | `ResourceSnapshot<LauncherOverview>` | 应用版本与当前实例、账号 ID |
| `get_settings` | — | `ResourceSnapshot<SettingsInfo>` | 全局设置 |
| `list_accounts` | — | `ResourceSnapshot<AccountInfo[]>` | 本机账号 |
| `list_instances` | — | `ResourceSnapshot<InstanceCatalogInfo>` | 当前游戏根目录的本地实例与版本分组 |
| `list_all_instances` | — | `InstanceCatalogInfo` | 扫描全部已登记游戏目录并合并返回本地实例 |
| `list_instance_mods` | `instanceId` | `ResourceSnapshot<ModInfo[]>` | 指定实例模组 |
| `list_servers` | — | `ResourceSnapshot<SavedServerInfo[]>` | 服务器收藏 |
| `list_bookmarks` | — | `ResourceSnapshot<FavoriteInfo[]>` | 内容收藏 |
| `select_instance` | `instanceId` | `SavedInfo` | 按稳定 ID 切换当前实例；不存在时返回 `NOT_FOUND` |

### 启动

| 命令 | 参数 | 返回 | 说明 |
| --- | --- | --- | --- |
| `launch_game` | `instanceId`, `accountId` | `LaunchAcceptedInfo` | 接受后返回会话与准备任务 ID；演示后端返回 `UNSUPPORTED` |
| `launch_to_server` | `instanceId`, `accountId`, `serverAddress` | `LaunchAcceptedInfo` | 接受直连启动；演示后端返回 `UNSUPPORTED` |

### 服务器

| 命令 | 参数 | 返回 | 说明 |
| --- | --- | --- | --- |
| `save_server` | `name`, `address`, `id?`, `instanceId?` | `SavedInfo` | 按稳定 ID 新增或更新，重复地址返回 `CONFLICT` |
| `delete_server` | `id` | `SavedInfo` | 按稳定 ID 删除收藏；不存在时返回 `NOT_FOUND` |
| `ping_server` | `address` | `ServerPingInfo` | 查询在线状态 |

### 账号

| 命令 | 参数 | 返回 | 说明 |
| --- | --- | --- | --- |
| `read_avatar` | — | `AvatarInfo` | 当前头像 |
| `start_login` | `provider`, `account?`, `password?` | `LoginSessionInfo` | 创建登录会话；演示后端返回 `UNSUPPORTED` |
| `get_login_session` | `authSessionId` | `LoginSessionInfo` | 查询登录会话阶段 |
| `select_login_profile` | `authSessionId`, `profileId` | `LoginSessionInfo` | 为会话选择 MUA 角色 |
| `cancel_login` | `authSessionId` | `LoginSessionInfo` | 取消登录会话 |
| `select_account` | `accountId` | `SavedInfo` | 按稳定 ID 切换当前账号，推送 `state`、`avatar` |
| `forget_account` | `accountId` | `SavedInfo` | 按稳定 ID 移除在线账号；离线账号不可删除 |
| `set_offline_skin` | `path` | `AvatarInfo` | 设置/清除本地皮肤，推送 `avatar` |

### 设置与系统

| 命令 | 参数 | 返回 | 说明 |
| --- | --- | --- | --- |
| `save_settings` | `patch`（`AppSettingsPatch`）、`expectedRevision` | `SavedInfo` | 按预期修订号更新，过期返回 `CONFLICT`。传入 `root` 时登记或切换游戏目录并刷新该目录的实例 |
| `remove_game_root` | `root`, `expectedRevision` | `SavedInfo` | 从已登记目录列表移除路径，不删除磁盘文件；移除当前目录时切换到列表首项并刷新实例 |
| `scan_java` | — | `JavaInfo[]` | 扫描本机 Java |
| `save_custom_sources` | `sources`（`DataSourceSet`）、`expectedRevision`、`activate?` | `SavedInfo` | 保存自定义数据源，`activate` 为真时在同一次写入中启用 |
| `use_data_sources` | `mode`（`default` / `custom`）、`expectedRevision` | `SavedInfo` | 切换默认或自定义数据源 |
| `test_data_source` | `name`, `url` | `TestResultInfo` | 测试数据源 |
| `reset_window` | — | `SavedInfo` | 恢复默认窗口尺寸 |
| `open_folder` | `kind` | `SavedInfo` | 打开目录（`root`/`config`/`saves`/`version`） |
| `read_log` | — | `string` | 读取启动器日志 |
| `get_system_info` | — | `SystemInfoInfo` | `os` 为二进制的目标操作系统标识，`data` 为实际的启动器配置目录 |
| `verify_key` | `role`, `key` | `KeyResultInfo` | 校验社区/管理员密钥 |

启动时读取 `mcuglauncher.conf.json`，文件缺失时创建默认配置。Linux 使用 `$XDG_DATA_HOME/com.mcug.launcher/`（默认 `~/.local/share/com.mcug.launcher/`），Windows 使用 `%LOCALAPPDATA%\com.mcug.launcher\`，macOS 使用 `~/Library/Application Support/com.mcug.launcher/`。

标准目录不可写时（沙箱、受控文件夹访问、ACL 损坏、便携部署）按以下顺序退到第一个真正可写的目录，并在 `get_system_info.data` 中报告实际使用的位置：`MCUGL_DATA_DIR` 环境变量 → `%LOCALAPPDATA%\MCUGLauncher\` → 程序所在目录的 `mcugl-data\` → 临时目录的 `MCUGLauncher-data\`。设置 `MCUGL_DATA_DIR` 表示显式指定，写不进去会直接报错退出，不再静默换目录。数据目录在启动早期用写探针验证，不可写时弹窗说明原因并退出，而不是 panic。

首次初始化时，默认游戏根目录分别是 Linux 的 `~/.minecraft`、Windows 的 `%APPDATA%\.minecraft` 和 macOS 的 `~/Library/Application Support/minecraft`。此时只保存目录路径。已有配置优先，读取失败时报告错误并保留原文件。设置写入先校验 `expectedRevision`，过期时返回 `CONFLICT`，写盘成功后更新内存状态和修订号。

配置文件的 `gameRoots` 按列表保存目录，第一项为当前目录。启动时会迁移旧文件的 `root` 字段。`get_settings.root` 仍返回当前目录，新增目录使用已存在的绝对路径，`list_instances` 读取当前目录的实例，`list_all_instances` 扫描全部登记目录并合并实例清单。移除目录仅更新配置，不删除磁盘文件。

### 存档

| 命令 | 参数 | 返回 | 说明 |
| --- | --- | --- | --- |
| `list_save_backups` | — | `SaveBackupInfo[]` | 存档备份列表 |
| `backup_saves` | — | `SavedInfo` | 立即备份，推送 `toast` |
| `restore_save` | `id`, `world` | `SavedInfo` | 从备份恢复 |

### 任务与游戏

| 命令 | 参数 | 返回 | 说明 |
| --- | --- | --- | --- |
| `list_tasks` | — | `ResourceSnapshot<TaskInfo[]>` | 带独立修订号的任务列表 |
| `task_action` | `id`, `action`（`pause`/`resume`/`cancel`/`delete`） | `SavedInfo` | 任务操作，推送 `tasks` |
| `list_game_sessions` | — | `ResourceSnapshot<GameSessionInfo[]>` | 带独立修订号的游戏会话 |
| `stop_game` | `sessionId` | `SavedInfo` | 请求停止游戏会话；当前演示后端返回 `UNSUPPORTED` |

### 内容与版本

| 命令 | 参数 | 返回 | 说明 |
| --- | --- | --- | --- |
| `search_content` | `category`, `query` | `ContentSearchResult` | 搜索资源 |
| `list_content_versions` | `id`, `requestId?` | `ResourceVersionsInfo` | 兼容版本 |
| `queue_content_version` | `token`, `versionId`, `mode?`, `path?` | `SavedInfo` | 加入下载队列 |
| `toggle_bookmark` | `id`, `name`, `description`, `kind` | `SavedInfo` | 收藏/取消收藏，推送 `state` |
| `list_vanilla_versions` | — | `VanillaVersionInfo[]` | 原版版本清单 |
| `install_vanilla` | `minecraftVersion` | `SavedInfo` | 安装原版 |
| `list_loaders` | `minecraftVersion`, `loader` | `LoaderListInfo` | 加载器版本 |
| `install_loader` | `minecraftVersion`, `loader`, `loaderVersion` | `SavedInfo` | 安装加载器 |
| `preview_pack` | `path` | `PackPreviewInfo` | 读取整合包计划；演示后端返回 `UNSUPPORTED` |
| `confirm_pack` | `planId`, `name`, `directory` | `SavedInfo` | 按计划确认导入；演示后端返回 `UNSUPPORTED` |

### 实例维护

| 命令 | 参数 | 返回 | 说明 |
| --- | --- | --- | --- |
| `preview_uninstall` | `instanceId` | `UninstallPreviewInfo` | 生成绑定实例修订号、十分钟有效的计划 |
| `apply_uninstall` | `planId` | `SavedInfo` | 校验计划后返回 `UNSUPPORTED`，不删除文件 |
| `clone_instance` | `instanceId`, `displayName`, `saves?`, `servers?` | `SavedInfo` | 克隆实例 |
| `export_instance` | `instanceId`, `path`, `servers?`, `saves?` | `SavedInfo` | 导出整合包 |
| `check_updates` | `instanceId` | `ContentUpdatesInfo` | 检查内容更新 |
| `preview_updates` | `checkId`, `projects?` | `ContentUpdatePreviewInfo` | 生成更新计划；演示后端返回 `UNSUPPORTED` |
| `apply_updates` | `planId` | `SavedInfo` | 按计划执行更新；演示后端返回 `UNSUPPORTED` |
| `toggle_mod` | `instanceId`, `modId`, `enabled` | `SavedInfo` | 按实例和模组 ID 启用/禁用，推送 `state` |
| `delete_mod` | `instanceId`, `modId` | `SavedInfo` | 按实例和模组 ID 删除，推送 `state` |
| `read_launch_options` | `instanceId`, `requestId?` | `LaunchOptionsInfo` | 读取实例启动设置 |
| `save_launch_options` | `token`, `instanceId`, `displayName`, `settings`, `inherit?` | `SavedInfo` | 演示后端返回 `UNSUPPORTED`，不会改写全局设置 |

### 社区

| 命令 | 参数 | 返回 | 说明 |
| --- | --- | --- | --- |
| `get_community` | — | `ResourceSnapshot<CommunityData>` | 公告/推荐/语录 |
| `refresh_community` | — | `ResourceSnapshot<CommunityData>` | 刷新社区内容 |
| `save_recommendations` | `items` | `SavedInfo` | 保存推荐列表，推送 `community` |

## 3. 事件（可选，当前前端不使用）

后端可以继续 `emit` 以下事件作为推送优化；前端不订阅，删除这些 `emit` 不影响 UI。

| 事件 | 载荷 | 触发时机 |
| --- | --- | --- |
| `launcher://tasks` | `TaskInfo[]` | 任务队列变化 |
| `launcher://community` | `CommunityData` | 社区内容刷新或推荐被修改 |
| `launcher://avatar` | `AvatarInfo` | 账号切换或皮肤变更 |

## 4. 内核重写指引

当前 `src-tauri/src/demo.rs` 是内存演示数据，`src-tauri/src/commands/implementation.rs` 里的实现只保证契约可运行；领域入口位于同目录下的分类模块。替换内核时：

1. 用真实配置、账号与队列替换 `demo::Data`，或改写 `state::Backend` 的持有方式；
2. 按领域逐个替换 `src-tauri/src/commands/*.rs` 中的命令实现，保持命令名、参数名与返回类型不变；
3. 不需要实现任何事件；前端在写命令返回后会重新调用查询命令；
4. 长任务使用队列执行，`list_tasks` 返回最新进度；前端全局每 3 秒同步任务、游戏和启动器状态，窗口重新获得焦点时同步；
5. 若确需修改契约，同时更新本文件、`src/api/commands/*`、`src/types/launcher.ts` 与 `src-tauri/src/model.rs`。

## 5. 演示后端之外的原生适配

Rust 侧已接入原生文件和目录选择器。两条命令都接受可选的 `title` 和 `defaultDirectory`，返回选中的本地路径字符串，取消时返回 `null`。游戏目录登记由 `save_settings` 负责。

| 命令 / 插件 | 用途 |
| --- | --- |
| `pick_file` | 选择 Java、皮肤、整合包、本地导入文件 |
| `pick_folder` | 在桌面端选择游戏目录、下载目录 |
| `open_folder` | 当前返回提示，后续调用系统文件管理器 |
