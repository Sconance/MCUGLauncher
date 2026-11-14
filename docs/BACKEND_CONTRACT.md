# 前后端契约（Tauri 命令与事件）

前端只通过 `@tauri-apps/api/core` 的 `invoke` 调用后端，通过 `@tauri-apps/api/event` 的 `listen` 接收状态推送。本文档是唯一的契约清单：改动命令名、参数或事件时必须同步修改本文档、`src/api/commands/*.ts` 与 `src/types/launcher.ts`。

## 1. 约定

- **命令名**：`snake_case`，动词在前，例如 `get_launcher_state`、`save_server`。
- **参数**：前端传 `camelCase` 键（Tauri 默认映射到 Rust 的 `snake_case` 参数），可省略的参数在 Rust 侧声明为 `Option<T>`。
- **返回值**：`camelCase` 的强类型 DTO，与 `src/types/launcher.ts` 一致；失败通过 `Err(String)` 抛出，前端包装为 `BackendError`。
- **状态**：任何改变全局状态的命令都必须 `emit` 对应事件；前端以事件为唯一数据源，不在页面里维护第二份状态。
- **事件名**：`launcher://<domain>`，只使用字母、数字、`-`、`/`、`:`、`_`。

前端统一入口：

```ts
import { call } from "../api/invoke";
const state = await call<LauncherState>("get_launcher_state");
```

## 2. 命令

### 状态

| 命令 | 参数 | 返回 | 说明 |
| --- | --- | --- | --- |
| `get_launcher_state` | — | `LauncherState` | 获取完整状态快照 |
| `select_instance` | `name` | `SavedInfo` | 切换当前实例，推送 `state` |
| `select_version` | `version` | `SavedInfo` | 切换当前版本，推送 `state` |

### 启动

| 命令 | 参数 | 返回 | 说明 |
| --- | --- | --- | --- |
| `launch_game` | `version` | `LaunchResultInfo` | 启动指定版本 |
| `launch_to_server` | `instance`, `serverAddress` | `LaunchResultInfo` | 用实例直连服务器启动 |

### 服务器

| 命令 | 参数 | 返回 | 说明 |
| --- | --- | --- | --- |
| `save_server` | `name`, `address`, `oldAddress?`, `instance?` | `ServerResultInfo` | 新增或更新收藏，推送 `state` |
| `delete_server` | `address` | `SavedInfo` | 删除收藏，推送 `state` |
| `ping_server` | `address` | `ServerPingInfo` | 查询在线状态 |

### 账号

| 命令 | 参数 | 返回 | 说明 |
| --- | --- | --- | --- |
| `read_avatar` | — | `AvatarInfo` | 当前头像 |
| `login_microsoft` | — | `AuthResultInfo` | 微软登录，推送 `state`、`avatar` |
| `login_mua` | `account`, `password` | `AuthResultInfo` | MUA 登录，推送 `state`、`avatar` |
| `list_mua_profiles` | — | `MuaProfileInfo[]` | 待选角色 |
| `switch_account_mode` | `mode` | `AccountSwitchResultInfo` | 切换账号模式，推送 `state`、`avatar` |
| `forget_account` | `mode` | `AuthResultInfo` | 移除本机账号，推送 `state`、`avatar` |
| `set_offline_skin` | `path` | `AvatarInfo` | 设置/清除本地皮肤，推送 `avatar` |
| `select_mua_profile` | `token`, `profileId` | `SavedInfo` | 选择 MUA 角色 |

### 设置与系统

| 命令 | 参数 | 返回 | 说明 |
| --- | --- | --- | --- |
| `save_settings` | `patch`（`AppSettingsPatch`） | `SavedInfo` | 部分字段更新，推送 `state` |
| `scan_java` | — | `JavaInfo[]` | 扫描本机 Java |
| `save_custom_sources` | `sources`（`DataSourceSet`） | `SavedInfo` | 保存自定义数据源 |
| `use_data_sources` | `mode` | `SavedInfo` | 切换默认/自定义，推送 `state` |
| `test_data_source` | `name`, `url` | `TestResultInfo` | 测试数据源 |
| `reset_window` | — | `SavedInfo` | 恢复默认窗口尺寸 |
| `open_folder` | `kind` | `SavedInfo` | 打开目录（`root`/`config`/`saves`/`version`） |
| `read_log` | — | `string` | 读取启动器日志 |
| `get_system_info` | — | `SystemInfoInfo` | 运行环境信息 |
| `verify_key` | `role`, `key` | `KeyResultInfo` | 校验社区/管理员密钥 |

### 存档

| 命令 | 参数 | 返回 | 说明 |
| --- | --- | --- | --- |
| `list_save_backups` | — | `SaveBackupInfo[]` | 存档备份列表 |
| `backup_saves` | — | `SavedInfo` | 立即备份，推送 `toast` |
| `restore_save` | `id`, `world` | `SavedInfo` | 从备份恢复 |

### 任务与游戏

| 命令 | 参数 | 返回 | 说明 |
| --- | --- | --- | --- |
| `list_tasks` | — | `TaskInfo[]` | 任务列表 |
| `task_action` | `id`, `action`（`pause`/`resume`/`cancel`/`delete`） | `SavedInfo` | 任务操作，推送 `tasks` |
| `list_games` | — | `GameInfo[]` | 运行中游戏 |
| `stop_game` | `id` | `SavedInfo` | 关闭游戏，推送 `games` |

### 内容与版本

| 命令 | 参数 | 返回 | 说明 |
| --- | --- | --- | --- |
| `search_content` | `category`, `query` | `ContentSearchResult` | 搜索资源 |
| `list_content_versions` | `id`, `requestId?` | `ResourceVersionsResult` | 兼容版本 |
| `queue_content_version` | `token`, `versionId`, `mode?`, `path?` | `SavedInfo` | 加入下载队列 |
| `toggle_bookmark` | `id`, `name`, `description`, `kind` | `SavedInfo` | 收藏/取消收藏，推送 `state` |
| `list_vanilla_versions` | — | `VanillaVersionsResult` | 原版版本清单 |
| `install_vanilla` | `version` | `SavedInfo` | 安装原版 |
| `list_loaders` | `version`, `loader` | `LoaderListResult` | 加载器版本 |
| `install_loader` | `version`, `loader`, `loaderVersion` | `SavedInfo` | 安装加载器 |
| `preview_pack` | `path` | `PackPreviewInfo` | 读取整合包预览 |
| `confirm_pack` | `token`, `name`, `directory` | `SavedInfo` | 确认导入整合包 |

### 实例维护

| 命令 | 参数 | 返回 | 说明 |
| --- | --- | --- | --- |
| `preview_uninstall` | `version` | `UninstallPreviewInfo` | 生成卸载预览 |
| `apply_uninstall` | `token` | `SavedInfo` | 执行卸载 |
| `clone_instance` | `version`, `name`, `saves?`, `servers?` | `SavedInfo` | 克隆实例 |
| `export_instance` | `version`, `path`, `servers?`, `saves?` | `SavedInfo` | 导出整合包 |
| `check_updates` | `version` | `ContentUpdatesInfo` | 检查内容更新 |
| `preview_updates` | `token`, `projects?` | `ContentUpdatePreviewInfo` | 生成更新预览 |
| `apply_updates` | `token` | `SavedInfo` | 执行更新 |
| `toggle_mod` | `file`, `enabled` | `SavedInfo` | 启用/禁用模组，推送 `state` |
| `delete_mod` | `file` | `SavedInfo` | 删除模组，推送 `state` |
| `read_launch_options` | `version`, `requestId?` | `LaunchOptionsInfo` | 读取实例启动设置 |
| `save_launch_options` | `token`, `version`, `name`, `settings`, `inherit?` | `SavedInfo` | 保存启动设置，推送 `state` |

### 社区

| 命令 | 参数 | 返回 | 说明 |
| --- | --- | --- | --- |
| `get_community` | — | `CommunityData` | 公告/推荐/语录 |
| `refresh_community` | — | `CommunityData` | 刷新社区内容 |
| `save_recommendations` | `items` | `SavedInfo` | 保存推荐列表，推送 `community` |

## 3. 事件

| 事件 | 载荷 | 触发时机 |
| --- | --- | --- |
| `launcher://state` | `LauncherState` | 任何全局状态变化（实例、账号、设置、收藏、模组等） |
| `launcher://tasks` | `TaskInfo[]` | 任务队列变化 |
| `launcher://games` | `GameInfo[]` | 游戏启动/退出 |
| `launcher://community` | `CommunityData` | 社区内容刷新或推荐被修改 |
| `launcher://avatar` | `AvatarInfo` | 账号切换或皮肤变更 |
| `launcher://toast` | `ToastInfo` | 需要以提示形式展示的消息 |
| `launcher://error` | `ToastInfo` | 后端主动上报的错误（当前预留） |

前端在 `src/state/launcher.tsx` 订阅以上全部事件，页面通过 `useLauncher()` 读取 `state/tasks/games/community/avatar`。

## 4. 内核重写指引

当前 `src-tauri/src/demo.rs` 是内存演示数据，`commands.rs` 里的实现只保证契约可运行。替换内核时：

1. 用真实配置、账号与队列替换 `demo::Data`，或改写 `state::Backend` 的持有方式；
2. 逐个实现 `commands.rs` 中的命令体，保持命令名、参数名与返回类型不变；
3. 修改状态的命令继续 `emit` 对应事件，前端不需要改；
4. 长任务使用 `OperationCoordinator` 一类的队列，进度通过 `tasks` 事件推送；新增流式进度时优先使用 Tauri `Channel`；
5. 若确需修改契约，同时更新本文件、`src/api/commands/*`、`src/types/launcher.ts` 与 `src-tauri/src/model.rs`。

## 5. 待补命令（原生交互）

当前文件/目录选择由前端的 `PathPrompt` 弹层模拟。接入 `tauri-plugin-dialog` 后计划增加：

| 命令 / 插件 | 用途 |
| --- | --- |
| `pick_file` | 选择 Java、皮肤、整合包、本地导入文件 |
| `pick_folder` | 选择游戏目录、下载目录 |
| `open_folder` | 当前返回提示，后续调用系统文件管理器 |
