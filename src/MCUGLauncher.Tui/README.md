# MCUG Launcher TUI

独立的终端入口，用于不启动 Avalonia 主界面时调用现有应用接口。交互终端使用方向键菜单，重定向输入输出时使用编号菜单，无新增 NuGet 依赖。

## 启动

在仓库根目录使用 .NET SDK（版本见根目录 global.json）：

```sh
dotnet run --project src/MCUGLauncher.Tui
dotnet run --project src/MCUGLauncher.Tui -- --data-directory "D:/MCUGL-tui-data"
dotnet run --project src/MCUGLauncher.Tui -- --help
```

本项目独立构建，不需要修改现有解决方案。使用 `net8.0-windows` 以匹配 App/Core，正式运行目标仍为 Windows。

App 的现有认证依赖会引入 Windows Desktop 运行时。源码运行需要 .NET 8 Desktop Runtime；在 Linux 上交叉构建时需附加 `-p:EnableWindowsTargeting=true`，不能据此声明 Linux 业务兼容。

## 选择 UI 或 TUI 独立发布

每次必须选择一个前端；两种模式引用不同入口项目，不发布整个解决方案。

```bat
scripts\build.bat ui
scripts\build.bat tui
```

```sh
bash scripts/build.sh ui
bash scripts/build.sh tui
```

| 模式 | 构建入口 | 默认产物 |
|---|---|---|
| `ui` | MCUGLauncher.Avalonia.csproj | `artifacts/ui/win-x64/MCUGLauncher.exe` |
| `tui` | MCUGLauncher.Tui.csproj | `artifacts/tui/win-x64/MCUGLauncher.Tui.exe` |

UI 包不包含 TUI 主程序，TUI 包不包含 Avalonia 主界面；两者都复用 App/Core。支持追加一个输出目录参数，如 `bash scripts/build.sh ui "artifacts/ui test"`。若输出目录存在另一种前端的可执行文件、入口程序集或 Avalonia 程序集，脚本拒绝发布，不删除已有文件；请改用独立目录。

旧的 TUI 命令仍可使用，内部转发到统一脚本的 `tui` 模式：

```bat
scripts\build-tui.bat
```

```sh
bash scripts/build-tui.sh
# 可指定 SDK；相对输出路径按仓库根目录解析
DOTNET_BIN=/path/to/dotnet bash scripts/build-tui.sh artifacts/tui-test
```

新脚本的两种模式均生成 Release、Windows x64、自包含单文件程序，无需目标机器另行安装 .NET。`ui` 是图形程序，`tui` 是控制台程序。脚本不修改版本、标签或正式发布文件。现有 PowerShell `build.ps1` 保持只构建 Avalonia 的行为及原有完整版/Lite 发布流程，不会同时构建 TUI。

“纯 TUI”指无 Avalonia 主界面；App 现有认证包仍会携带部分 Windows Desktop/WebView2 依赖，不在本任务中删除或改写它们。

### 在 Windows 启动发布产物

现有 `build.bat` / `build.sh` 负责构建，发布完成后直接运行 EXE，无需额外启动脚本：

```powershell
.\artifacts\ui\win-x64\MCUGLauncher.exe
.\artifacts\tui\win-x64\MCUGLauncher.Tui.exe
```

测试主页收藏进服请使用 UI 版：编辑服务器收藏、选择已安装实例并保存，再点击该收藏卡片的“打开”。
如果未绑定实例，“打开”会引导绑定；如果绑定的实例已删除，后端返回失败，不会自动启动其他实例。
测试前核对当前游戏根目录和账号。程序成功启动进程不代表已经登录服务器，实际连接结果以游戏画面为准。
`artifacts/`、所有项目的 `bin/` 和 `obj/` 均被 Git 忽略，仓库仅提交源码和脚本。

交互终端：`↑/↓` 移动高亮光标，`→/Enter` 进入或确认选择，`←/Esc/0` 返回上一级，主菜单返回即退出；`Q` 随时退出菜单。支持 `PgUp/PgDn` 翻页、`Home/End` 跳到首项/返回项。长列表按窗口高度分页，长名称按显示列截断；窗口过小或光标操作不受支持时退回编号输入。

交互菜单会清屏重绘，并保留最近几行上下文。路径、名称等文字仍使用行输入；删除、卸载、关闭游戏等确认仍用 `y/N`，保留前面显示的预览，回车默认取消。输入输出重定向或 `TERM=dumb` 时使用编号菜单，`0` 返回，`q` 或输入结束退出。密码输入时 q 是密码字符，Esc 取消。交互终端中密码隐藏；重定向输入时不回显密码，不应把含密码的输入脚本提交仓库。

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
    ├── ArrowMenu.cs        # 高亮、分页、窗口尺寸适配及键盘读取
    ├── MenuSelection.cs    # 方向键选择状态，不访问终端或业务
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
- 后端提供 `LaunchToServerAsync`，Avalonia 主页已接入实例绑定及收藏进服；TUI 收藏页尚未提供该操作，可在实例页配置已有的自动进服设置。
- Windows 账号保护、Java、文件选择、真实 MUA 登录、游戏启动和下载安装需要在实际环境中验收。终端输入输出或模拟服务检查不能代替这些验收。

## 验证

```sh
dotnet build src/MCUGLauncher.Tui -c Release
dotnet run --project src/MCUGLauncher.Tui -- --help
```

使用隔离目录进行手工检查：进入各页并返回、输入非法菜单编号、取消删除/关闭确认、输入结束退出。真实业务检查先选择专用测试游戏目录；下载任务还应检查暂停、继续、取消和重新启动后的恢复。

游戏启动入口已调用真实的 `ILauncherService.LaunchAsync(instance.Name)`，后续由现有 GameCommands、LaunchService 处理账号、Java、启动参数及进程；不是只打印日志。真实游戏能否成功启动仍取决于实例完整性、Java、账号和实际运行环境，本次未完成真实游戏验收。

### 本次开发验证（2026-11-07）

- Linux 交叉构建通过，0 警告、0 错误；`build-tui.sh` 实际生成 Windows x64 自包含控制台 EXE。
- 临时验证程序位于 `/tmp`，未加入仓库；18 项检查覆盖菜单、EOF、失败回执、异常、取消删除/导入、提交阶段任务保护、启动参数保留、MUA 角色选择、订阅释放和真实 App 的隔离配置持久化。模拟登录未使用真实账号。
- 通过临时 .NET Core runtimeconfig 在 Linux 运行不涉及 Windows API 的终端冒烟，实际检查主菜单、添加服务器、查看设置和退出；此方式仅用于开发验证，不是发布启动方式。
- SourceFormatter、布局和原生 UI 边界检查通过。初次开发时版本检查误把 AppConfig.cs 公告 URL 中的分支名当成硬编码版本；后续已修复检查规则，公告地址保持不变。
- `.bat` 未在 Windows 实际执行，Windows EXE 及真实登录、下载安装、游戏进服、任务恢复仍待实际环境验收。当前任务没有修改解决方案、CI、后端或 Avalonia。

### 方向键菜单验证

- 临时检查扩展到 32 项，新增方向键移动、进入/返回、Q 退出、首尾跳转、翻页边界、空列表、中英文截断与控制字符过滤。
- 实际终端检查了主菜单移动光标、右键进入服务器页、左键返回，以及退出；编号模式的原有接口调用检查仍保留。
- 方向键菜单只改变展示和输入，不改变游戏启动接口或增加 Linux 原生运行支持。

### 独立发布验证

- 通过 `build.sh ui` 和 `build.sh tui` 实际发布两种 Windows x64 自包含 EXE，分别为 GUI 和 Console 子系统。
- 检查实际依赖清单：UI 不包含 TUI 项目，TUI 不包含 Avalonia 前端及其包；检查输出目录没有另一种入口程序。
- 13 项临时脚本场景检查通过，覆盖项目选择、旧 TUI 命令、路径空格、参数错误、SDK 失败、缺失产物及拒绝混合目录；使用实际产物目录互换发布目标也会被拒绝，不删除已有文件。
- `.bat` 未在 Windows 执行；布局、原生边界、格式和 Shell 语法检查通过。版本检查的公告 URL 误报已修复，并加入 Python 回归检查和 CI。
