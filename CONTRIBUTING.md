# 参与改进

Issue 请说明版本、系统、复现步骤、预期与实际结果，日志和截图先脱敏。不要提交账号、令牌、游戏文件、存档或私人路径。

## 当前分支与结构

当前开发分支为 `0.24.0-codex`，属于 AI 迁移框架尝试。主界面为 Avalonia 原生控件；WebView2 仅用于微软官方授权页面。

- `src/MCUGLauncher.Avalonia/Views`：页面和弹窗展示、事件。
- `Presentation` 与 `ILauncherService / UiContext`：界面状态与消息适配。
- `src/MCUGLauncher.App/UseCases`：校验、预览、确认和入队。
- App 的 Services/Authentication：运行环境适配。
- `src/MCUGLauncher.Core`：可复用的启动、安装、解析、事务和存储能力。

开发严格遵循 [AGENTS.md](AGENTS.md) 与 [后端规范](BACKEND_DEVELOPMENT.md)。不把业务放回窗口，不用 partial 隐藏业务，不为缩短文件而增加空转发层。B 类组件必须更新成员级依赖说明。历史网页前端说明仅保留在 [归档](docs/archive/webview/README.md)，不能按其旧目录新增页面。

## 验证与交付

构建见 [BUILD](docs/BUILD.md)。提交前运行 SourceFormatter、布局、版本与原生边界检查，按影响范围运行 test/*Checks。页面修改运行 NativeUiChecks 并查看截图；模拟响应不替代真实账号、系统拖放及游戏验收。

只推送约定分支；发布 Release 和移动标签按用户指令。版本唯一来源为 Directory.Build.props，运行期读取 ProductInfo.Version。

项目整体许可证尚未指定，复用或再分发前请与维护者确认。第三方声明仍需保留在相应包或分发资源中。
