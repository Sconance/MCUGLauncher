# 构建与目录

## 目录职责

| 目录 | 内容 |
| --- | --- |
| `src/MCUGLauncher.Avalonia/` | Avalonia 原生界面、窗口、登录窗口与页面 |
| `src/MCUGLauncher.App/` | UseCases、Services、认证与配置 |
| `src/MCUGLauncher.Core/` | 下载、Java、实例、整合包等核心能力 |
| `test/` | 行为、兼容与启动器协议检查（含 `LauncherChecks`） |
| `tools/` | 源码格式与隔离预览工具 |
| `scripts/` | 构建、发布、指定检查、布局与原生 UI 边界校验 |
| `docs/` | 使用指南、构建说明、迁移记录与截图 |

命名空间与兼容存储路径保留内部旧名 BlockLaunch，不影响产品名称。
根目录保留 README、CHANGELOG 和 CONTRIBUTING，便于仓库平台展示。

## 构建与发布

要求 Windows x64、.NET 8 SDK（global.json 固定 8.0.426，可使用同 feature band 的更新补丁），首次还原 NuGet 包需要网络。
以下命令从仓库根目录运行：

```powershell
.\scripts\build.ps1
.\scripts\build.ps1 -Publish
```

也可以直接使用 SDK：

```powershell
dotnet restore src/MCUGLauncher.Avalonia/MCUGLauncher.Avalonia.csproj
dotnet build src/MCUGLauncher.Avalonia/MCUGLauncher.Avalonia.csproj -c Release
dotnet publish src/MCUGLauncher.Avalonia/MCUGLauncher.Avalonia.csproj -c Release -o artifacts/publish
```

单文件：`scripts/build.ps1 -Publish -SingleFile` 输出两个 EXE（需要可写的 `%TEMP%\.net`）。文件夹发布：`artifacts/full`、`artifacts/lite`，由 `scripts/build.ps1 -Publish` 打包到 `artifacts/release`。单文件 apphost 依赖 `%TEMP%\.net`，缓存不可写时会以 error 160 退出，因此不再作为默认发布格式。
游戏仍需 Java，微软登录窗口需要 WebView2；主界面是原生 Avalonia，不再加载 Web UI。

## 运行检查

检查项目使用自定义控制台断言，以 `dotnet run` 执行，不是 `dotnet test`。
PowerShell 脚本支持从任意工作目录调用，执行时会切换到仓库根目录：

```powershell
.\scripts\run-checks.ps1 -Name CompatibilityChecks
.\scripts\run-checks.ps1 -Name DownloadChecks,DependencyChecks
.\scripts\run-checks.ps1 -Name AuthChecks -CheckArguments '--avatar'
.\scripts\run-checks.ps1 -Name LauncherChecks
python scripts/check-native-boundaries.py
```

直接运行的等价示例：

```powershell
dotnet run --project test/LauncherChecks/LauncherChecks.csproj -c Release
```

脚本不会默认批量运行全部检查。部分检查会启动窗口、访问网络、读取账号缓存或扫描
本机环境，运行前阅读对应 `Program.cs`。集成检查可能下载和启动真实游戏，需单独准备
Java 与游戏环境。自动化检查不代替真实授权、游戏联机或多显示器 DPI 验收。

## 原生界面

启动 `src/MCUGLauncher.Avalonia` 即可打开原生主界面：启动、实例、下载、账号、任务、游戏、
收藏、社区与设置。修改页面后应实际启动检查布局、滚动和状态刷新；MUA 检查中的 metadata JSON
是合成 fixture，真实账号与游戏验收仍需独立执行。

## 静态布局校验

```powershell
python scripts/check-layout.py
```

只需 Python 3 标准库，在 Windows 或 Linux 上检查项目 XML、项目引用、链接源码、
图标与复制资源是否存在，并检查旧路径残留；不会编译或运行游戏。CI 在构建前执行此校验。

`docs/archive/legacy-wpf/TESTING.md` 记录了旧 WPF 界面时期的验证范围，供历史追溯。

## 双版本发布

scripts/build.ps1 -Publish 同时生成完整版和 Lite 版。artifacts/release 中包含 MCUGLauncher-版本号-Windows-x64.exe 与 MCUGLauncher-版本号-Windows-x64-Lite.exe。Lite 不内置 .NET，要求 .NET 8 Desktop Runtime x64；两版均需要 WebView2 才能使用内嵌微软授权。后续 Release 同时附带两个程序，完整版名称保持不变。
