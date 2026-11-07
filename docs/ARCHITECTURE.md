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


## 服务器收藏的实例绑定

`Models.SavedServer.Instance` 保存当前游戏根目录中的实例键，不保存完整路径，不增加编号。
旧配置缺少该字段时默认为空。`Contracts.SavedServerInfo.Instance` 同步暴露只读状态快照。

`ILauncherService.SaveServerAsync(name, address, oldAddress, instance)` 使用既有强类型 handler。
省略或传 null 保留原绑定，空字符串清除绑定；新增收藏未提供实例时保持未绑定。
因此既有 Avalonia/TUI 的三参数调用不会丢失收藏的实例。界面和 Core 无需修改。

收藏进服已通过 `ILauncherService.LaunchToServerAsync(instance, serverAddress)` 接入。
`LaunchToServerFeatureHandler` 调用 `GameCommands`，复用身份校验、任务互斥、实例快照和进程管理。
实例必须明确指定，未绑定或已删除的实例返回失败，不会回退到当前选中实例；服务器地址由
Core `ServerEndpoint.Parse` 校验并规范化。共享的目录预留会阻止普通启动和收藏启动并发准备同一实例。

地址只覆盖本次启动快照的 `LaunchSettings.Server`，不永久改写全局/实例/收藏设置；
普通 `LaunchAsync` 保持原有行为。参数生成继续交给 `Core.LaunchService`。
调用方读取收藏的 `Instance` 和 `Address` 后传给接口，并展示 `LaunchResultInfo.Ok/Message`。
当前任务没有修改 Avalonia 或 TUI 页面，按钮可在独立的前端任务中接入。
