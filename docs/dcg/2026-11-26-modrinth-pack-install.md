# Modrinth 整合包安装报告

更新日期为 2026-11-26。改动位于 `tauri-feat/下载和启动` 工作区。

## 已接通的桌面端功能

| 功能 | 当前进度 |
| --- | --- |
| 本地整合包预览 | 从本地压缩包读取 `modrinth.index.json`，解析游戏版本和加载器。计划绑定压缩包的 SHA-256 和设置修订，首次确认须在十分钟内完成。 |
| 安装任务 | 下载客户端必需文件并校验大小、SHA-1 和 SHA-512。按顺序应用公共覆盖和客户端覆盖。复用现有 Minecraft 与加载器安装流程。 |
| 目录发布 | 在目标父目录暂存文件。成功后登记 `directory/name/.minecraft`，可用 `list_all_instances` 找到生成的实例。同一计划重复确认返回原安装任务。 |

## 待接通

- **桌面实机启动。** 仍待在游戏窗口验证整合包能进入主菜单。
- **其他格式和可选内容。** CurseForge ZIP、MultiMC 和可选文件选择待接入。当前跳过可选和服务端专用文件。
- **页面入口。** 现有前端命令封装可以调用 Rust 接口，官方页面的本地导入交互尚待接入。

## 已验证

- 53 项 Rust 单元测试通过。整合包相关用例覆盖路径校验、客户端覆盖顺序、重名保护和配置写入失败回滚。
- Linux 临时目录实际安装 Fabulously Optimized 6.5.0。本地压缩包的 50 个客户端依赖通过校验，生成 48 个模组文件，并安装 Minecraft 1.21.1 和 Fabric 0.19.3。实例扫描识别了加载器配置，独立游戏根目录已写入测试配置。冷安装用时约 9 分 31 秒。 [实测整合包](https://modrinth.com/modpack/fabulously-optimized/version/N276l2ON)
- Linux 桌面调试构建和浏览器演示构建通过。Hyprland 启动项的固定二进制已更新，`cmp` 确认与构建产物一致。

## 开发入口

- [Rust 公开命令](../../src-tauri/src/commands/modrinth_pack.rs) · [Modrinth 归档与文件安装](../../src-tauri/src/core/modrinth_pack.rs)
- [接口契约](../BACKEND_CONTRACT.md) · [后端任务进度](backend-progress.md) · [Modrinth 格式规范](https://support.modrinth.com/en/articles/8802351-modrinth-modpack-format-mrpack)
