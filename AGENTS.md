# MCUG Launcher 开发约定

本分支为 **0.24.0-codex · AI 迁移框架尝试**。用户已授权迁移到 Avalonia，旧 WebView 主界面规则在此分支由以下规则替代。

- ShellWindow 仅组装窗口、导航和原生适配；不在窗口中添加账号、下载、启动或社区业务。
- Views 只构建原生控件与展示交互，Presentation 定义界面状态/菜单，LauncherFacade 是现有用例的适配边界。
- Core 与 App 保持独立；App 不引用 Avalonia、WPF 或 WebView2 控件。后端开发规范见 BACKEND_DEVELOPMENT.md。
- 复用原有认证、网络策略、安装与任务队列，不为换界面新写下载器或 Java 扫描器。
- 任务和游戏会话不依赖页面生命周期，离开页面只释放界面订阅。关闭启动器不关闭游戏，单独关闭游戏需确认。
- 下载目标在确认时固定；卸载必须先显示路径预览，再由原生对话框确认。
- 保留旧配置，编辑/保存/启用分别表达。数据源默认/自定义可分别测试，保存不自动生效。
- 保留蓝白配色和透明 MCUG 标志；分类通过 NavigationCatalog 注册，IconPath 可替换成图片资源。
- 不回引旧 HTML/CSS/JS 主界面。微软官方授权网页是唯一保留的独立 WebView2 用途，不扩展成业务页面回退。
- 提交前运行 SourceFormatter、布局、版本、边界检查，以及受影响的 App/Core 检查和 NativeUiChecks；检查三档 Skia 截图。
- 原生 Headless、模拟响应不代表真实认证、真实游戏与原生文件对话框已验收，必须如实写明验证范围。
- 只推送 0.24.0-codex；不改 0.23.x 或其他远端分支，不发布 Release，不移动标签，不覆盖正式 EXE。
