# 原生 Avalonia 架构（0.24.0-codex）

本分支是 AI 迁移框架尝试。原网页主界面已移除，以下是当前实际结构。

```text
ShellWindow（窗口和导航）
  → Views（原生控件和对话框）
  → ILauncherService / UiContext（类型化状态、消息兼容适配）
  → MCUGLauncher.App / UseCases（流程）
  → Services / Authentication / MCUGLauncher.Core（实际业务）
```

窗口不扫描 Java，不安装资源，不读写配置文件。页面不持有 LauncherContext，不创建网络客户端，不管理游戏进程。界面状态在 ILauncherService / UiContext，菜单在 Presentation/NavigationCatalog。临时表单由页面持有，后端状态刷新不覆盖正在编辑的表单。

队列和运行游戏由 App/Core 管理，页面导航不取消后台业务。ShellDesktopHost 是异步确认和原生文件选择的适配器。微软授权是唯一保留的独立网页用途，业务页面不使用浏览器。

详细迁移对照见 [迁移记录](MIGRATION_0.24.0-codex.md)，后端组件依赖见 [后端规范](../BACKEND_DEVELOPMENT.md)。

Core 的无界面服务器直连接口与收藏适配入口见 [服务器启动接口](SERVER_LAUNCH.md)，尚未绑定 UI 按钮。
