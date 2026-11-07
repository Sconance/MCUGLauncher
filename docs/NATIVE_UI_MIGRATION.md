# 原生 Avalonia UI 迁移计划

目标：用 Avalonia 原生控件替换 `Ui/` 下的 HTML/CSS/JS，并把中间层从 JSON 协议适配为类型化服务调用。登录窗口保留 WebView2（OAuth 必须），其余页面不再加载 WebView。

## 目标架构

```text
Views (Avalonia XAML + code-behind)
  ├─ ShellWindow：导航、标题栏、窗口生命周期
  ├─ HomeView / DownloadView / SettingsView / ...
  └─ 仅依赖 Contracts.ILauncherService

Contracts（App/Contracts）
  ├─ ILauncherService：统一类型化入口
  ├─ LauncherState、事件 DTO、窗口位置与校验边界
  └─ UI 不接触 AppConfig、JSON 或命令字符串

UseCases.LauncherService
  ├─ 实现 ILauncherService，统一 UI 线程调度
  ├─ 内部包装 LauncherApplication 命令分发
  └─ 复用现有 UseCases / LauncherContext

Core / Services：保持不变
```

### 中间层现状

`LauncherApplication.HandleAsync(string json)` 仍是 App 内部命令分发实现，但不再是 UI 入口。
页面只调用 `ILauncherService`：

- 强类型方法（如 `LaunchAsync`、`SaveSettingsAsync`、`SearchContentAsync`）；
- 状态由 `LauncherState` 共享 DTO 推送，`LauncherStateBuilder` 直接构建，不再经过匿名 JSON；
- 任务、游戏、账号、内容等事件均为类型化事件；
- UI 线程调度由 `LauncherService` 注入的 `dispatch` 负责；
- JSON 只作为 `LauncherService` 内部传输细节，不暴露给页面。

## 页面映射

| 现有 JS | 目标 View |
| --- | --- |
| `home-page.js` | `HomeView` |
| `community-pages.js` | `CommunityView` |
| `resource-pages.js` | `DownloadView` + `ResourceDetailView` |
| `instance-pages.js` | `InstancesView` + `InstanceSettingsView` |
| `settings-page.js` / `settings-actions.js` | `SettingsView`（分栏） |
| `tasks.js` | `TasksView` |
| `games.js` | `RunningGamesView` |
| `bookmarks.js` | `BookmarksView` |
| `saves.js` | `SavesView` |
| `instance-tools.js` | `InstanceMaintenanceView` |
| `accounts.js` / `account-modals.js` / `mua-profiles.js` | `AccountView` + 对话框 |
| `data-sources.js` | `DataSourcesView` |
| `install.js` / `pack-import.js` | `InstallView` |
| `launch-options.js` | `LaunchOptionsView` |
| `directory.js` | `DirectoryView` |
| `host.js` / `state.js` / `navigation.js` | 由 ViewModel + 导航服务替代 |

## 阶段

1. **壳与服务契约**：`ShellWindow`（顶栏 + 内容区）、导航、`ILauncherService` 与状态事件；首页/设置两个页面先行。
2. **核心页面**：下载、实例、任务、账号、运行游戏。
3. **外围页面**：社区、收藏、存档、数据源、维护、安装。
4. **删除 WebView 主界面**：移除 `Ui/` 资源、`bridge.js`、WebUiChecks；仅保留登录窗口 WebView2。
5. **验收**：真实账号、下载安装、启动/多开、DPI/缩放与键盘操作。

## 风险与现状

- 页面已全部迁移为原生 View；业务入口已统一为 `ILauncherService`；
- `LauncherApplication` 内部仍暂存命令字符串/JSON 分发实现，但已不对页面暴露；
- 原生 UI 已有静态边界检查，但尚无 Avalonia Headless/截图自动化测试；
- 多 DPI、键盘操作与真实账号/游戏仍需发布前人工验收；
- 登录窗口仍依赖 WebView2，属于合理例外。

## 进展

阶段 1 已落地：

- `ILauncherService` + `LauncherService`：统一类型化入口，包装 `LauncherApplication`；共享
  `LauncherState` 覆盖账号、实例、任务、服务器、收藏、版本分组与数据源，并通过
  `StateChanged` / `TasksChanged` 等事件推送；
- `ShellWindow`：原生侧栏 + `PageHost` 页面宿主，可列出实例、启动、搜索/选择资源版本、查看任务；
- `SettingsView`：启动、Java、数据源、界面、服务器收藏、存档、恢复、日志、诊断、社区身份、关于；
- `InstancesView`：实例切换、启动、模组启停/删除、实例启动参数（继承或独立）、版本文件夹、
  克隆/导出/卸载预览/内容更新；
- `HomeView`：账号卡、实例选择/启动、服务器自动查询与增删改、公告/推荐/语录展示；
- `DownloadView`：原版版本列表/安装、加载器（Fabric/Quilt/Forge/NeoForge）、五类资源搜索/
  详情/版本选择/安装、整合包预览与导入；
- `AccountView`：头像读取、离线 PNG 皮肤选择/清除、正版/MUA/离线切换、MUA 多角色选择；
- `GamesView`：运行中游戏列表与关闭；
- `BookmarksView`：收藏列表、取消收藏、按兼容版本安装到实例或目录；
- `CommunityView`：管理员密钥与推荐本地管理；公告只读展示单篇 Markdown，并可打开完整正文；
- 主页公告支持本地 data URL 配图；
- 设置页：已保存游戏目录切换、添加并切换；
- **WebView 主界面已删除**：`Ui/`、`MainWindow`、`bridge`、`WebUiChecks` 与 Web 边界脚本已移除；
  新增 `scripts/check-native-boundaries.py`，WebView2 仅保留在微软 OAuth 登录窗口。

远程公告配图已通过有界下载接入；界面已按 0.22.3-fix 前端样式重构（顶栏、浅色画布、卡片、
按钮/输入框、列表与分段导航）。剩余事项：多 DPI/逐页截图复核；功能与登录/游戏验收仍需在真实环境补测。
