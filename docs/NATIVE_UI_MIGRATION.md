# 原生 Avalonia UI 迁移计划

目标：用 Avalonia 原生控件替换 `Ui/` 下的 HTML/CSS/JS，并把中间层从 JSON 协议适配为类型化服务调用。登录窗口保留 WebView2（OAuth 必须），其余页面不再加载 WebView。

## 目标架构

```text
Views (Avalonia XAML)
  ├─ ShellView / MainWindow：导航、标题栏、状态栏
  ├─ HomeView / DownloadView / SettingsView / ...
  └─ ViewModels（每页一个，持有状态与命令）

Application Facade（新增）
  ├─ ILauncherFacade：类型化方法（LaunchAsync、InstallAsync、SaveSettings…）
  ├─ 状态事件：StateChanged、TaskChanged、AccountChanged、GameChanged
  └─ 复用现有 UseCases / LauncherContext，不再经过 JSON 字符串

Core / App 库：保持不变
```

### 中间层适配

当前 `LauncherApplication.HandleAsync(string json)` 是唯一的 UI 入口。原生 UI 不应再拼 JSON，新增 `LauncherFacade`：

- 把 68 条命令映射为强类型方法（如 `Task LaunchAsync(string version)`、`Task SaveSettingsAsync(AppConfig patch)`）；
- 状态通过 `event Action<LauncherState> StateChanged` 推送，替代 `Send(new { type = "state" ... })`；
- 任务、游戏、账号状态拆为独立事件；
- UI 线程调度由 Avalonia `Dispatcher.UIThread` 负责；
- JSON 协议暂时保留给 WebView 登录窗口和旧的 WebUiChecks，最终删除。

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

1. **壳与 Facade**：`ShellView`（侧栏 + 内容区）、导航服务、`LauncherFacade` 与状态事件；首页/设置两个页面先行。
2. **核心页面**：下载、实例、任务、账号、运行游戏。
3. **外围页面**：社区、收藏、存档、数据源、维护、安装。
4. **删除 WebView 主界面**：移除 `Ui/` 资源、`bridge.js`、WebUiChecks；仅保留登录窗口 WebView2。
5. **验收**：真实账号、下载安装、启动/多开、DPI/缩放与键盘操作。

## 风险

- 30 个前端模块与 68 条命令需要逐页迁移，不能一次性替换；
- 现有 `WebUiChecks` 只验证 DOM，原生 UI 需要新的 ViewModel/集成测试；
- 状态模型目前是匿名对象，需先定义强类型 `LauncherState`；
- 原生 UI 需要重新实现主题、滚动、响应式布局；
- 登录窗口仍依赖 WebView2，属于合理例外。

## 进展

阶段 1 已落地：

- `LauncherFacade`：类型化消息入口，包装 `LauncherApplication`；`LauncherState` 覆盖账号、实例、
  任务、服务器、收藏、版本分组与数据源，并通过 `StateChanged` / `TasksChanged` 等事件推送；
- `ShellWindow`：原生侧栏 + `PageHost` 页面宿主，可列出实例、启动、搜索/选择资源版本、查看任务；
- `SettingsView`：启动、Java、数据源、界面、服务器收藏、存档、恢复、日志、诊断、社区身份、关于；
- 旧 `MainWindow`（WebView）保留为“经典界面”回退入口。

下一步：实例页（切换、版本设置、启动参数、模组表格）与主页/社区数据；功能全对齐后再删除 WebView。
