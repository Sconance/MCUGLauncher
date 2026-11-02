# 开发约定与交付流程

规则来源：[0.22.3-Avalonia 开发约定](https://github.com/Sconance/MCUGLauncher/blob/7f1dc70f4ca062218ec546caa50cec6d3019f712/AGENTS.md) 与 [后端规范](../BACKEND_DEVELOPMENT.md)。项目入口为根目录 [AGENTS.md](../AGENTS.md)，其中保留上游约定并明确 Avalonia 对应关系。

## 当前约定

- 分支：`0.24.0-codex`，不得改动其他远端分支。
- 标记：AI 迁移框架尝试，不代表稳定发行。
- 本地测试文件：`outputs/MCUG Launcher 0.24.0-codex Lite.exe`。
- 每次变更均检查职责、依赖、可读性、格式及受影响行为；同步文档、提交推送、核对 CI，再交付对应提交生成的 Lite。
- 框架例外只限用户授权的 Avalonia 迁移，不能用来绕过组件规范。

## 检查

```powershell
python scripts/check-layout.py
python scripts/check-version.py
python scripts/check-web-boundaries.py
dotnet run --project tools/SourceFormatter
```

按实际代码影响选择回归项目；完整迁移检查组合为：

```powershell
./scripts/run-checks.ps1 -Name MuaChecks,DownloadChecks,QueueChecks,CompatibilityChecks,WebBridgeChecks,NativeUiChecks
```

文档修改不机械重跑真实账号或全部游戏检查；不得把未执行项目写成通过。页面变更必须检查原生控件测试与截图，队列变更必须验证排队、取消和恢复。格式化工具只能验证排版，不能证明逻辑、依赖和架构没有问题。

## Lite 交付

提交完成后使用当前源码进行框架依赖单文件发布，文件名由当前约定分支决定，覆盖同名本地测试版并校验 SHA-256。旧测试文件保存在忽略目录；正式 EXE 不覆盖，不自动发布 Release。Windows 锁定文件时等用户关闭，不结束游戏进程。

本轮仅调整开发约定和工作流文档，程序业务代码不变；已有原生迁移与回归结果详见 [迁移记录](MIGRATION_0.24.0-codex.md)。
