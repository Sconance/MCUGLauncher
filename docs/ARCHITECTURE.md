# 当前架构（0.22.3-codex）

界面使用 Avalonia + WebView2 + HTML/CSS/JS。MainWindow 只组装适配器、管理原生对话框和窗口生命周期；不注册另一套业务命令。

| 层 | 主要模块 | 职责 |
| --- | --- | --- |
| 原生宿主 | MainWindow、Ui/WindowChromeController、Ui/UiBridge、Ui/EmbeddedUiCache | 窗口、消息出口、内嵌资源与 WebView2 |
| 用例 | UseCases/LauncherApplication 及各 Commands | 校验消息、编排账号/游戏/内容/设置/维护 |
| 适配与状态 | Services、Authentication | 配置、会话缓存、队列适配、外部认证与状态快照 |
| 核心 | MCUGLauncher.Core | 校验下载、依赖解析、事务安装、实例和游戏会话 |
| 页面 | Ui/*.js、styles.css | 展示与明确的交互命令；后台任务独立于页面 |

后端组件的分级、依赖声明与 Core 下沉标准见 [后端开发规范](../BACKEND_DEVELOPMENT.md)。

## fix 合并对应关系

- WindowChromeController、EmbeddedUiCache、UiBridge：接入现有宿主，保留原生移动循环、关闭保护与异步发送。
- AccountSession：由 AccountCommands、AccountModeCommands、AccountSessionCache 承接，保留账号快速切换和独立离线名。
- UiCommandRouter：由 LauncherApplication 承接，保留协议类型验证和完整命令集合。
- LauncherStateBuilder：保留 Services 同名实现；账号局部通知不触发全量实例扫描。
- CommunityFeed、LaunchCoordinator：由 CommunityCommands、GameCommands 承接，保留网络策略、多开与启动快照。
- ServerCommands、ContentCommands、SettingsCommands：由现有细分用例承接，下载/维护继续使用持久任务队列。
- AppLog：继续使用 LauncherContext.Log，不增加重复日志实现。

不能把 fix 的旧命令栈与以上模块并排注册，否则会出现两套账号、下载或配置流程。共享 Core、认证、服务及测试的注释已合入。

原 fix 架构记录保留在 [历史快照](archive/0.22.3-fix-architecture.md)。
