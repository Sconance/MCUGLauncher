# 当前架构

## 多游戏目录

`AppConfig.GameRoots` 保存全部目录，`Root` 为当前目录；`InstanceTarget(Root,Name)` 唯一定位受管理实例。
`GameDirectoryCatalog` 复用 VersionCatalog 扫描所有目录并单独报告离线、权限或元数据错误。
状态的 `GameDirectories` 返回全部结果，旧 `Instances/VersionGroups/Mods` 保留当前目录语义。
新的强类型目录管理、启动、克隆、导出、模组和卸载预览接口直接复用现有用例与 Core；后台任务在入队时冻结目录，不跟随当前选择。
收藏增加 GameRoot，旧数据加载时固定当时的根目录；配置重载保留离线磁盘路径。
TUI 已按目录分组接入上述功能，Avalonia 主页收藏已支持完整跨目录绑定，其余实例管理仍保留当前目录展示。详见 [多目录组件与接口](components/GameDirectories.md)。

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


## 服务器收藏的实例绑定

`Models.SavedServer.Instance` 保存当前游戏根目录中的实例键，不保存完整路径，不增加编号。
旧配置缺少该字段时默认为空。`Contracts.SavedServerInfo.Instance` 同步暴露只读状态快照。

`ILauncherService.SaveServerAsync(name, address, oldAddress, instance)` 使用既有强类型 handler。
省略或传 null 保留原绑定，空字符串清除绑定；新增收藏未提供实例时保持未绑定。
因此既有 Avalonia/TUI 的三参数调用不会丢失收藏的实例。界面和 Core 无需修改。

主页收藏进服通过 `ILauncherService.LaunchManagedInstanceAsync(new InstanceTarget(GameRoot,Instance),Address)` 接入。
`LaunchToServerFeatureHandler` 调用 `GameCommands`，复用身份校验、任务互斥、实例快照和进程管理。
实例必须明确指定，未绑定或已删除的实例返回失败，不会回退到当前选中实例；服务器地址由
Core `ServerEndpoint.Parse` 校验并规范化。共享的目录预留会阻止普通启动和收藏启动并发准备同一实例。

地址只覆盖本次启动快照的 `LaunchSettings.Server`，不永久改写全局/实例/收藏设置；
普通 `LaunchAsync` 保持原有行为。参数生成继续交给 `Core.LaunchService`。
调用方直接读取所点击收藏的 `GameRoot`、`Instance` 和 `Address` 后传给接口，展示 `LaunchResultInfo.Ok/Message`；地址规范化仅处理连接参数，不用于反查收藏目录。
Avalonia 主页已通过服务器卡片的“打开”按钮调用该接口；未绑定时引导编辑收藏。
编辑窗口从 `State.GameDirectories` 读取实例列表，显示目录并按目录和名称共同选中，经 `SaveServerAsync` 同时保存实例和根目录；不可用目标保留原绑定。
TUI 服务器收藏页暂未接入这一操作。
