# MCUG Launcher TUI

独立的终端入口，用于不启动 Avalonia 主界面时调用现有应用接口。采用编号菜单，无新增 NuGet 依赖。

## 启动

在仓库根目录使用 .NET SDK（版本见根目录 global.json）：

```sh
dotnet run --project src/MCUGLauncher.Tui
dotnet run --project src/MCUGLauncher.Tui -- --data-directory "D:/MCUGL-tui-data"
dotnet run --project src/MCUGLauncher.Tui -- --help
```

本项目独立构建，不需要修改现有解决方案。使用 `net8.0-windows` 以匹配 App/Core，正式运行目标仍为 Windows。

App 的现有认证依赖会引入 Windows Desktop 运行时。源码运行需要 .NET 8 Desktop Runtime；在 Linux 上交叉构建时需附加 `-p:EnableWindowsTargeting=true`，不能据此声明 Linux 业务兼容。

## 仅发布 TUI

```bat
scripts\build-tui.bat
```

```sh
bash scripts/build-tui.sh
# 可指定 SDK；相对输出路径按仓库根目录解析
DOTNET_BIN=/path/to/dotnet bash scripts/build-tui.sh artifacts/tui-test
```

两份脚本只发布 `MCUGLauncher.Tui.csproj` 及 App/Core 依赖，不构建 Avalonia 主界面。默认生成 `artifacts/tui/win-x64/MCUGLauncher.Tui.exe`，为 Release、Windows x64、自包含单文件控制台程序；无需目标机器另行安装 .NET。可用唯一的位置参数指定输出目录，路径有空格时加引号。脚本不修改版本、标签或正式发布文件。

“纯 TUI”指无 Avalonia 主界面；App 现有认证包仍会携带部分 Windows Desktop/WebView2 依赖，不在本任务中删除或改写它们。

输入编号选择，`0` 返回上一级，在主菜单输入 `0` 退出；`q` 或输入结束退出程序。没有全屏清屏或光标定位依赖，可以保留终端输出。密码输入时 q 是密码字符，Esc 取消。交互终端中密码隐藏；重定向输入时不回显密码，不应把含密码的输入脚本提交仓库。

默认配置位于 `%LOCALAPPDATA%/MCUGLauncher.Tui`，与 Avalonia 的配置分开。`--data-directory` 明确指定已有目录时会使用并修改该目录的配置、账号缓存和任务记录。配置隔离不等于游戏文件隔离：默认游戏目录仍由现有 ConfigStore 决定；安装或启动前请在设置页核对游戏目录。隔离配置默认没有 Avalonia 的微软账号缓存。

## 文件职责

```text
MCUGLauncher.Tui/
├── tui.cs                  # Main、参数、真实服务装配及释放
├── Hosting/
│   ├── TerminalDesktopHost.cs # 确认和手动路径输入
│   └── TerminalBrowser.cs     # 明确拒绝尚未支持的微软交互授权
└── tui/
    ├── ui.cs               # 主菜单、页面导航、应用通知
    ├── Terminal.cs         # 输入、编号校验、确认及文本输出
    └── Pages/              # 每个功能页面一个类
```

页面只依赖 `Contracts.ILauncherService` 和共享 DTO；仅组合入口引用 LauncherService、ConfigStore，Hosting 实现既有 IDesktopHost。没有新增业务契约、JSON 命令桥、下载器或账号认证流程。

## 已接入的入口

| 页面 | 操作 |
|---|---|
| 实例与启动 | 查看和选择实例、启动、保存实例自动进服地址、卸载预览及后端确认 |
| 账号 | 离线/已有账号切换、离线名、MUA 登录和角色选择、移除缓存 |
| 服务器收藏 | 查看、添加、编辑、删除、查询在线状态 |
| 下载与安装 | 搜索和分页查看原版、安装原版/加载器、整合包预览及确认导入 |
| 任务与游戏 | 查看进度、暂停/继续/取消、删除历史、确认关闭游戏 |
| 设置与 Java | 游戏目录、Java 扫描、内存、窗口尺寸、自动中文 |
| 诊断与数据源 | 日志末尾、系统信息、数据源连通性、状态刷新 |

任务进度在重新进入列表时刷新；退出页面不会停止任务。退出 TUI 会释放应用服务并停止启动器任务处理，不主动关闭已启动的游戏。已提交后台请求不等于安装或卸载已完成，结果应以任务页状态为准。

## 当前限制

- 没有微软交互授权窗口，首次登录和缓存过期后的重新授权仍需 Avalonia；已有账号使用仍受后端缓存与运行环境限制。
- 未提供资源搜索/收藏安装、社区内容编辑、存档备份恢复等全部接口的终端页面。
- 没有服务器收藏的一键进服接口；可在实例页配置已有的自动进服设置。
- Windows 账号保护、Java、文件选择、真实 MUA 登录、游戏启动和下载安装需要在实际环境中验收。终端输入输出或模拟服务检查不能代替这些验收。

## 验证

```sh
dotnet build src/MCUGLauncher.Tui -c Release
dotnet run --project src/MCUGLauncher.Tui -- --help
```

使用隔离目录进行手工检查：进入各页并返回、输入非法菜单编号、取消删除/关闭确认、输入结束退出。真实业务检查先选择专用测试游戏目录；下载任务还应检查暂停、继续、取消和重新启动后的恢复。

### 本次开发验证（2026-11-07）

- Linux 交叉构建通过，0 警告、0 错误；`build-tui.sh` 实际生成 Windows x64 自包含控制台 EXE。
- 临时验证程序位于 `/tmp`，未加入仓库；18 项检查覆盖菜单、EOF、失败回执、异常、取消删除/导入、提交阶段任务保护、启动参数保留、MUA 角色选择、订阅释放和真实 App 的隔离配置持久化。模拟登录未使用真实账号。
- 通过临时 .NET Core runtimeconfig 在 Linux 运行不涉及 Windows API 的终端冒烟，实际检查主菜单、添加服务器、查看设置和退出；此方式仅用于开发验证，不是发布启动方式。
- SourceFormatter、布局和原生 UI 边界检查通过。根目录版本检查在未修改的 `src/MCUGLauncher.App/Models/AppConfig.cs` 报硬编码版本；按本任务范围保留原文件。
- `.bat` 未在 Windows 实际执行，Windows EXE 及真实登录、下载安装、游戏进服、任务恢复仍待实际环境验收。当前任务没有修改解决方案、CI、后端或 Avalonia。
