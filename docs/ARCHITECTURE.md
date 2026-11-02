# 当前架构

界面使用 Avalonia 原生控件。`ShellWindow` 只负责导航、窗口生命周期和页面宿主；业务仍只经过
`LauncherFacade` → `UseCases` → `Services` / `Core`。WebView2 仅保留给微软 OAuth 登录窗口。

| 层 | 主要模块 | 职责 |
| --- | --- | --- |
| 原生壳 | `ShellWindow`、`ShellDesktopHost`、`NativeDialogs` | 窗口、导航、原生确认框与文件选择 |
| 页面 | `Views/*View`（Home、Download、Instances、Account、Games、Bookmarks、Community、Settings） | 展示、页面事件与状态订阅 |
| 门户 | `LauncherFacade`、`LauncherState`、`LauncherEvents` | 类型化命令、消息解析与 UI 线程调度 |
| 用例 | `UseCases/LauncherApplication` 及各 Commands | 校验输入、编排账号/游戏/内容/设置/维护 |
| 适配与状态 | `Services`、`Authentication` | 配置、会话缓存、队列适配、外部认证与状态快照 |
| 核心 | `MCUGLauncher.Core` | 校验下载、依赖解析、事务安装、实例和游戏会话 |

WebView2 的唯一用途是 `MicrosoftLoginWindow` 承载微软 OAuth 页面；主界面不再加载 HTML/CSS/JS。

后端组件的分级、依赖声明与 Core 下沉标准见 [后端开发规范](../BACKEND_DEVELOPMENT.md)。

## 行为约定

- 页面通过 `LauncherFacade` 发送强类型命令并订阅 `StateChanged`、`TasksChanged` 等事件；
  JSON 协议只作为门面内部实现，页面不直接拼消息。
- 后台任务独立于页面生命周期，由 `LauncherTaskQueue` / `OperationCoordinator` 持久化。
- 旧配置字段与数据文件保留；页面暂未提供入口不等于可以丢弃设置。
- 微软登录窗口继续使用 WebView2，其余界面不再依赖浏览器。

## 历史记录

- 0.22.3 的 WebView2/HTML UI 架构与拆分记录保留在
  [0.22.3-fix 架构快照](archive/0.22.3-fix-architecture.md)；
  当前代码已删除 `Ui/` 与 `MainWindow`。
