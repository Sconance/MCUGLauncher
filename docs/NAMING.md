# 命名与数据位置

- 展示名称：MCUG Launcher。
- 仓库、程序、根命名空间：MCUGLauncher。
- 项目：MCUGLauncher.Core / MCUGLauncher.App / MCUGLauncher.Avalonia。
- 配置、任务和账号目录：%LOCALAPPDATA%/MCUGLauncher。
- 默认游戏目录：%LOCALAPPDATA%/MCUGLauncher/minecraft（仍允许用户手动指定游戏目录）。
- 默认资源下载目录：用户 Downloads/MCUGLauncher。
- 实例元数据、事务、缓存及打包标识：.mcuglauncher-* / .mcuglauncher / mcuglauncher-export.json。
- 账号缓存保护标识：MCUGLauncher.Accounts.v1；仍使用 Windows 当前用户 DPAPI。

## 数据影响

按用户要求，当前版本只使用当前名称，不保留其他名称的探测、路径别名或自动配置导入。磁盘上的既有文件不会被删除，也不会擅自改写配置中用户选择的路径。

账号保护标识变化意味着之前加密的缓存可能无法继续解密，需要从账号设置移除对应登录缓存后重新登录。此操作不能由改名脚本擅自执行。实例/整合包文件本身仍在磁盘上，但不同元数据文件名中的安装记录、升级记录和事务历史不会自动导入，不能声称完全兼容。

## 检查范围

本轮编译通过；14 组隔离检查通过，覆盖兼容性、内容安全、依赖、诊断、下载、库、MUA、原生界面、任务、资源下载、稳定性、卸载、升级和协议。IntegrationChecks 编译通过但运行因 work/test-minecraft 缺少 1.21.1 版本数据而中断，未完成真实游戏验收。

此命名统一作用于当前约定分支；不重写 Git 历史，不改动其他分支、标签或 Release。
