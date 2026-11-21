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
