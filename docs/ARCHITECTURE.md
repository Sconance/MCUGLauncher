# 当前架构

界面使用 Avalonia 原生控件。`ShellWindow` 只负责导航、窗口生命周期和页面宿主；页面只依赖
`Contracts.ILauncherService`，由 `UseCases.LauncherService` 统一实现业务入口。WebView2 仅保留给微软 OAuth 登录窗口。

| 层 | 主要模块 | 职责 |
| --- | --- | --- |
| 契约 | `App/Contracts`：`ILauncherService`、`LauncherState`、事件 DTO、`LaunchLimits`、`WindowPlacement` | UI 与业务共享的唯一类型化边界 |
| 原生壳 | `ShellWindow`、`ShellDesktopHost`、`NativeDialogs` | 窗口、导航、原生确认框与文件选择 |
| 页面 | `Views/*View`（Home、Download、Instances、Account、Games、Bookmarks、Community、Settings、Tasks） | 展示、页面事件与状态订阅 |
| 应用服务 | `UseCases/LauncherService` | 统一类型化入口、UI 线程调度、状态/事件分发 |
| 用例 | `UseCases/LauncherApplication` 及各 Commands | 校验输入、编排账号/游戏/内容/设置/维护 |
| 适配与状态 | `Services`、`Authentication` | 配置、会话缓存、队列适配、外部认证与状态快照 |
| 核心 | `MCUGLauncher.Core` | 校验下载、依赖解析、事务安装、实例和游戏会话 |

内部命令协议只存在于 `LauncherService`/`LauncherApplication` 内部，页面不接触 JSON、配置对象或命令字符串。
WebView2 的唯一用途是 `MicrosoftLoginWindow` 承载微软 OAuth 页面；主界面不再加载 HTML/CSS/JS。

后端组件的分级、依赖声明与 Core 下沉标准见 [后端开发规范](../BACKEND_DEVELOPMENT.md)。

社区用例通过 [CommunityService](components/CommunityService.md) 发布公告数据。清单模式由
`AnnouncementSyncService` 解析数据源、并行下载和校验公告，并在整批通过后提交本地快照。
每个入口有独立的同步时限。缓存写入失败时，社区用例仍可发布已校验的内容并报告错误。

## 行为约定

- 页面只调用 `ILauncherService` 的强类型方法并订阅 `StateChanged`、`TasksChanged` 等领域事件；
- 一次性请求/响应通过接口默认方法返回 `Task<TResponse>`，由 `[FeatureOperation]` 与自动发现的 UseCase handler 配对；无需增加服务消息解析与应用命令分支；
  不得读取 `AppConfig`、不得拼 JSON、不得调用 `HandleAsync`。
- 状态、请求和事件 DTO 位于 `App/Contracts`；`LauncherStateBuilder` 直接生成 `LauncherState`，
  不再维护第二份匿名 JSON schema。
- 数值边界统一使用 `LaunchLimits`；占位实例使用 `InstanceInfo.IsPlaceholder`，不依赖展示文案。
- 后台任务独立于页面生命周期，由 `LauncherTaskQueue` / `OperationCoordinator` 持久化。
- 旧配置字段与数据文件保留；页面暂未提供入口不等于可以丢弃设置。
- 微软登录窗口继续使用 WebView2，其余界面不再依赖浏览器。

## 历史记录

- 0.22.3 的 WebView2/HTML UI 架构与拆分记录保留在
  [0.22.3-fix 架构快照](archive/0.22.3-fix-architecture.md)；
  当前代码已删除 `Ui/` 与 `MainWindow`。
