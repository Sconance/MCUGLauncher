# 开发环境要求

本文适用于 `tauri` 分支。只构建 React 前端时需要 Node.js 和 npm。编译 Tauri 桌面程序或运行 Rust 测试时，还需要 Rust 和当前平台的系统依赖。

## 工具链

| 工具 | 要求 |
| --- | --- |
| Node.js 与 npm | 当前锁定的 Vite 8 要求 Node.js `^20.19.0` 或 `>=22.12.0`。使用 `npm ci` 安装锁定的依赖。 |
| Rust 与 Cargo | 当前 `Cargo.lock` 中的 `codespace` 和 `typespace` 要求 `rustc >= 1.97`。用 `rustc --version` 确认当前终端实际使用的版本。 |

## Ubuntu 26.04

在仓库根目录运行。系统包安装时需要 `sudo` 权限，Rust 工具链安装到当前用户目录。

```bash
bash scripts/setup-env.sh
```

首次安装 Rust 后，打开新终端再运行构建命令。其他 Ubuntu 版本、Debian 和其他发行版按发行版选择系统包，并用上表确认其 Node.js 版本是否达标。 [Tauri 官方前置要求](https://v2.tauri.app/zh-cn/start/prerequisites/)

已在 Ubuntu 26.04 x86-64 上用本机安装的 Node.js 22.22.1、Rust 1.100.0 和 WebKitGTK 4.1（2.52.6）验证桌面调试构建。

## Windows

在已提供 WinGet 的 Windows 上，从仓库根目录运行。按系统提示确认安装权限。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\setup-env.ps1
```

安装后重新打开 PowerShell，让新工具进入 `PATH`。已有 Build Tools 的机器可通过 Visual Studio Installer 确认已添加「使用 C++ 的桌面开发」。 [微软安装参数说明](https://learn.microsoft.com/en-us/visualstudio/install/use-command-line-parameters-to-install-visual-studio?view=vs-2022) · [Tauri 官方前置要求](https://v2.tauri.app/start/prerequisites/#windows)

## macOS

在仓库根目录运行。若弹出 Xcode Command Line Tools 安装窗口，确认安装后脚本会继续。首次安装 Homebrew 时按终端提示操作。

```bash
bash scripts/setup-env.sh
```

在运行构建命令的终端执行安装脚本输出的 `export PATH=...` 命令。 [Homebrew Node.js 24](https://formulae.brew.sh/formula/node@24) · [Homebrew rustup](https://formulae.brew.sh/formula/rustup) · [Tauri 官方前置要求](https://v2.tauri.app/start/prerequisites/#macos)

## 验证环境

安装脚本会执行 `npm ci --prefer-offline`，优先复用已有下载。只需要构建前端时，可直接从仓库根目录执行 `npm ci` 和 `npm run build`。

如果系统依赖已经安装，只有 npm 镜像下载出现 `ERR_SOCKET_TIMEOUT`，可单独通过官方源重新安装 JavaScript 依赖。此命令只对本次安装切换下载源。

```bash
npm ci --prefer-offline --registry=https://registry.npmjs.org/ --replace-registry-host=always
```

从仓库根目录运行桌面编译脚本。

```bash
node scripts/build.mjs
```

默认生成桌面调试可执行文件。传入参数时，脚本将参数交给 Tauri CLI，例如 `node scripts/build.mjs --no-bundle` 会编译发布模式的可执行文件。

单独检查 Rust 后端可运行 `cargo check --locked --manifest-path src-tauri/Cargo.toml`。

日常桌面开发使用 `npm run tauri dev`。当前分支使用演示后端，桌面构建通过只验证应用可以编译，真实游戏安装和启动仍待实现。详见 [V2 实施状态](BACKEND_V2.md#实施状态)。

## 前端演示模式（`npm run dev:demo`）

只改前端时，不必每次启动 Rust 内核。演示模式用一组内存假数据渲染完整界面，适合调样式、走交互和截取 UI：

```powershell
npm run dev:demo
# 浏览器打开 http://localhost:5174/
```

- 演示模式使用独立端口 `5174`，可以和 `npm run tauri dev`（`1420`）同时开着。
- 标记写进 `index.html`（`window.__MCUGL_DEMO__`），因此 dev 与 build 行为一致；用普通 `npm run dev` 打开时没有这个标记，界面会显示「后端未连接」的错误页，而不是假数据。
- 假数据在 `src/demo/fixtures.ts`（实例、账号、服务器、模组、社区内容等），命令实现子集在 `src/demo/backend.ts`，未实现的命令返回 `UNSUPPORTED`。数据只存在内存里，刷新页面即重置。
- 启动任务会随 `list_tasks` 轮询推进进度，方便观察任务状态机；启动游戏、取消启动、水波动画和 toast 都可以直接点。
- 左上角固定显示「演示模式 · 假数据」标记，避免把演示界面误当真实环境。
- 需要静态产物时用 `npm run build:demo`（同样输出到 `dist/`，`index.html` 带演示标记）；正式发布仍用 `npm run build`。

## 数据目录与启动失败排查

配置和日志默认写到平台标准目录（Windows 为 `%LOCALAPPDATA%\com.mcug.launcher\`）。沙箱、受控文件夹访问或 ACL 异常会让这个目录不可写；启动器会先写探针，再按顺序退到第一个可写目录：`%LOCALAPPDATA%\MCUGLauncher\` → 程序所在目录的 `mcugl-data\`（`tauri dev` 下即 `src-tauri\target\debug\mcugl-data\`） → 临时目录的 `MCUGLauncher-data\`。

指定固定位置时设置 `MCUGL_DATA_DIR`（显式指定，不可写会直接报错，不会静默换目录；留空字符串等同于未设置）：

```powershell
$env:MCUGL_DATA_DIR = "D:\mcugl-data"
npm run tauri dev
```

实际使用的目录会打印在启动日志里，同时通过 `get_system_info` 的 `data` 字段返回，设置页的「配置目录」开的就是它。所有候选都不可写时程序弹窗说明每个路径的失败原因并退出，不再出现 setup hook 的 `panic`。

## 第三方前端（目录 / `.mcuglf`）

v1 支持在运行时把整个前端换成第三方实现，不需要重新编译 Rust。接口与加载规则见 [Kernel API v1](KERNEL_API_V1.md)，包格式见 [前端包（`.mcuglf`）](FRONTEND_PACKAGE.md)。

开发目录（改文件刷新即生效，不用重启）：

```powershell
$env:MCUGL_FRONTEND = "examples\third-party-frontend"
npm run tauri dev
```

打成包、按正式流程验收：

```powershell
npm run frontend:pack   # 产出 dist-frontends\com.example.mcugl-showcase-0.1.0.mcuglf
# 设置 → 开发者选项 → 前端（第三方 UI）→ 导入前端包…（或让 MCUGL_FRONTEND 直接指向 .mcuglf）
```

- 目录根部必须有 `frontend.json`：`name`、`version`、`kernelApi` 必填；`entry` 默认 `index.html`；打包分发时另需 `identifier`（可选 `author` / `description` / `homepage` / `icon`）。
- 校验失败时**不退回**官方前端，而是弹窗说明原因后退出，避免作者误以为自己的前端已加载。
- 文件由内核注册的 `mcugl` 自定义协议直接读取，带 `Cache-Control: no-store`；改完文件刷新页面即可看到，不需要重启进程。
- 页面在 Windows 上的地址是 `http://mcugl.localhost/…`，其他平台是 `mcugl://localhost/…`。它属于本地 origin，页面里可以直接 `invoke`，不需要额外的 Tauri capability 或远程域名白名单。
- 优先级：`MCUGL_FRONTEND`（目录或包）> 设置页选中的已安装前端 > 内置官方前端。清空变量（`Remove-Item Env:MCUGL_FRONTEND`）才会回到「配置里选的前端」。
- `MCUGL_FRONTEND` 指向 `.mcuglf` / `.zip` 时解压到 `<数据目录>/frontends/.dev/`，日志里的模式显示为 `package`；指向目录时模式为 `directory`。
- 第三方前端白屏或卡死时，Windows 上按 **Ctrl+Alt+C** 强制切回官方前端：清空 `MCUGL_FRONTEND` 与配置里的选择，并原地重建主窗口（不退出进程）。快捷键由 Rust 侧注册，不依赖页面 JS。
- 设置 → 开发者选项 → 前端：切换或卸载后点「重启前端」立即生效，不需要重启启动器。
- 示例目录 `examples/third-party-frontend/` 不含 npm、不含构建步骤，复制过去改 `index.html` 就能开始写。
