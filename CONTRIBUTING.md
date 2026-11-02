# 参与改进

欢迎提交问题与改进建议。Issue 请包含软件和系统版本、复现步骤、预期与实际结果，截图和日志先脱敏。

代码修改保持范围清晰，说明目的与验证。不要提交 bin、obj、账号文件、Token、私人路径、游戏文件或存档。涉及启动、认证、下载或实例删除时补充针对性检查。

源码放在 `src/`，检查项目放在 `test/`，构建/格式工具放在 `tools/`。
构建和检查命令见 [构建与目录说明](docs/BUILD.md)。迁移或新增项目后运行
`python scripts/check-layout.py`，核对引用和资源路径。

项目整体许可证尚未指定，复用或再分发前请与维护者确认。第三方组件遵循各自许可证。

开发与构建说明见 [构建与目录说明](docs/BUILD.md)，架构与数据存储见 [README](README.md)。提交前执行源码格式检查，并按改动范围运行行为检查。

## 分支与版本

- `main` 是发布分支；功能开发在版本分支（如 `0.22.3`）进行，修复分支如 `0.22.3-fix`。
- 版本号只改 `Directory.Build.props` 的 `McugVersion`，运行期读取 `ProductInfo.Version`；发版前运行 `python scripts/check-version.py`（需要标签时加 `--tag`），规则见 [版本号控制](docs/VERSIONING.md)。
- 界面是 `src/MCUGLauncher.Avalonia/Views` 下的原生 Avalonia 页面；业务放入 `UseCases`，页面只通过
  `LauncherFacade` 调用。WebView2 只用于微软登录，不要重新引入主界面 Web UI 或旧的 WPF 页面结构。
- 提交前运行 `python scripts/check-layout.py`、`python scripts/check-native-boundaries.py`、
  `dotnet run --project tools/SourceFormatter`，并按改动范围运行 `test/*Checks`。
  旧 WPF 时期的检查项目已退役，相关历史见 [WPF 归档](docs/archive/legacy-wpf/)。

新增命令须补 `LauncherChecks`，并保持门面层类型化；架构见 [当前架构](docs/ARCHITECTURE.md)。

后端新增/修改组件按 [后端开发规范](BACKEND_DEVELOPMENT.md) 执行：优先完全独立组件；依赖现有组件的必须声明到“类.成员（含参数）”；修改现有组件不新增组件，共用逻辑膨胀时下沉 Core。
