# Fabric、Quilt 与 Forge 安装和启动报告

更新日期为 2026-11-26。改动位于 `tauri-feat/下载和启动` 工作区。

## 已接通的桌面端功能

| 功能 | 当前进度 |
| --- | --- |
| Fabric 与 Quilt | 从官方 Meta API 获取兼容版本，将 Maven 依赖转换为共用启动配置。文件校验通过后发布实例。 |
| Forge | 从官方 Maven 获取版本列表，校验并运行官方无界面客户端安装器。当前支持从 Minecraft 1.20.1 起的正式版。 |
| 共用启动 | 原版和已接入的加载器共用版本继承解析、Java 选择、日志与会话管理。Forge 配置指定补丁客户端时，使用该客户端进入 Java 类路径。 |

## 待接通

- **实机图形验证。** 本次在 Linux 验证了实际安装和客户端进入图形初始化。Fabric、Quilt 与 Forge 的桌面窗口和主菜单仍待实机验证。
- **整合包导入。** 桌面端的 `preview_pack` 和 `confirm_pack` 仍返回 `UNSUPPORTED`。已有各加载器安装、下载校验和任务队列，可作为导入的基础。
- **压缩包格式。** Modrinth `.mrpack` 可从 `modrinth.index.json` 读取版本、下载 URL 和哈希。CurseForge ZIP 可从 `manifest.json` 读取项目 ID 和文件 ID，还需经 CurseForge API 查询允许直接下载的文件。导入需要校验归档路径，在独立实例目录应用覆盖文件。 [Modrinth 格式](https://support.modrinth.com/en/articles/8802351-modrinth-modpack-format-mrpack) · [CurseForge 导出格式](https://support.curseforge.com/support/solutions/articles/9000198500-exporting-a-modpack-for-curseforge-project-submission)

## 已验证

- 49 项 Rust 单元测试通过。Linux 桌面构建和浏览器演示构建通过，Hyprland 菜单入口已更新。
- 在临时目录实际安装 Fabric 0.16.10、Quilt 0.20.0-beta.9 和 Forge 54.1.0，对应 Minecraft 1.21.4。三个客户端均进入图形初始化。Forge 54.1.0 初次获取 Mojang 映射超时，复测安装成功。
- Forge 66.1.9 对应 Minecraft 26.3，已实际安装并运行到 SDL 初始化。测试环境无显示设备，SDL 按预期退出。

## 开发入口

- [安装和任务命令](../../src-tauri/src/commands/vanilla.rs)
- [Fabric 和 Quilt Meta API](../../src-tauri/src/core/meta_loader.rs) · [Forge](../../src-tauri/src/core/forge.rs) · [共用安装流程](../../src-tauri/src/core/loader_install.rs)
- [接口契约](../BACKEND_CONTRACT.md) · [后端 V2 规范](../BACKEND_V2.md) · [后端任务进度](backend-progress.md)
