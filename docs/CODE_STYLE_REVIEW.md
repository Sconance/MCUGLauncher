# 当前代码结构与风格复查

## 当前结构

ShellWindow 负责窗口和导航，Views 负责展示和交互，LauncherFacade 适配类型化状态与协议，UseCases 负责流程，Services/Core 执行。原网页页面、app.js/host.js 和相关预览工具不再存在。

- C# 使用 SourceFormatter；布局、版本及原生边界检查已接入 CI。
- 当前没有 JS/CSS 页面，不保留空用的 Prettier 配置，也不宣称运行已退役的网页检查。
- 修改 B 类组件同步 XML remarks、依赖登记和必要的组件文档。
- Core 已复用服务器协议、原子存储、启动快照、内容事务和文件识别；不在视图内重新实现。
- 异步操作依据成功回执关闭弹窗，过期加载结果有请求身份或版本隔离。

## 仍有改进空间

LauncherContext 依赖面较宽，协议分发集中，部分历史组件职责和注释仍可改善。格式通过不是全项目架构完全合规或零缺陷的证明。

## 最近审查记录

- [Core 复用](CORE_REUSE_AUDIT.md)
- [原生交互修复](INTERACTION_FIXES.md)
- [弹窗布局与滚动](DIALOG_DESIGN.md)
- [设置与提示](SETTINGS_DESIGN.md)
- [历史记录](archive/webview/CODE_STYLE_REVIEW.md)

测试范围须如实记录：NativeUiChecks 使用 Headless/Skia 和模拟响应，不能替代真实登录、系统文件拖放及游戏验收。
