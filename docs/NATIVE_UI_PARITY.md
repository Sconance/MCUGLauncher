# 原生 Avalonia UI 迁移：功能对齐与 WebView 退出条件

目标：用 Avalonia 原生页面替换 `src/MCUGLauncher.Avalonia/Ui/` 的 HTML/CSS/JS，业务仍只经过
`UseCases`、`Services` 与 `Core`。WebView 只保留给微软 OAuth 登录窗口。

过渡期约束：

- 功能未在原生页面覆盖前，不删除 `Ui/`、`MainWindow` 和“经典界面”入口；`AGENTS.md` 的兼容性要求优先。
- 新页面只通过 `LauncherFacade` 调用业务命令，不在页面拼 JSON，也不复制下载器、依赖解析器或 Java 扫描。
- 每个页面独立为 `Views/*View`，`ShellWindow` 只负责导航、窗口生命周期和页面宿主。
- 页面接入后同步更新本文件的矩阵；只有矩阵全部为“已完成”并通过验收，才能进入删除阶段。

## 功能矩阵（以 `Ui/*.js` 与 `LauncherApplication` 命令为准）

| 功能 | Web 入口 | 原生状态 |
| --- | --- | --- |
| 实例列表与启动 | `home-page.js` | 基础完成（启动、刷新） |
| 实例选择 / 版本选择 | `home-page.js` | 缺失 |
| 账号卡与登录入口 | `home-page.js`、`accounts.js` | 基础完成（微软/MUA/离线切换） |
| 皮肤、披风、MUA 档案 | `avatars.js`、`mua-profiles.js` | 缺失 |
| 主页服务器状态 | `home-page.js`、`host.js` | 缺失 |
| 公告 / 推荐 / 语录 | `home-page.js`、`community-pages.js` | 缺失 |
| 下载分类与搜索 | `resource-pages.js` | 已完成（五类 Modrinth 搜索） |
| 资源详情 / 版本选择 / 安装 | `resource-pages.js`、`resource-picker.js` | 已完成基础流程（版本选择、实例安装/目录下载） |
| 原版版本列表 / 安装 | `resource-pages.js` | 缺失 |
| 加载器安装 | `install.js` | 缺失 |
| 整合包预览 / 导入 | `pack-import.js` | 缺失 |
| 实例内容管理（模组启停/删除/更新） | `instance-pages.js` | 基础完成（启停；删除与更新表待接） |
| 实例设置（改名、分类、文件夹） | `instance-pages.js`、`launch-options.js` | 缺失 |
| 克隆 / 导出 / 卸载预览 | `instance-tools.js` | 已完成基础流程 |
| 内容更新检查 / 预览 / 执行 | `instance-tools.js` | 已接入，待真实内容验收 |
| 设置：启动参数 | `settings-page.js` | 已完成（目录、Java、内存、窗口、离线名） |
| 设置：Java 扫描 | `settings-actions.js` | 已完成 |
| 设置：网络 / 数据源 | `settings-page.js`、`data-sources.js` | 已完成（编辑、测试、默认/自定义切换、写入接口） |
| 设置：界面 / 恢复窗口 | `settings-actions.js` | 已完成 |
| 设置：服务器收藏 | `settings-page.js` | 已完成（增删改、查询） |
| 设置：存档备份 / 恢复 | `saves.js` | 已完成 |
| 设置：运行日志 / 诊断 | `settings-actions.js` | 已完成 |
| 设置：关于与许可 | `settings-page.js` | 已完成 |
| 社区：匿名 / 身份密钥 | `community-pages.js` | 已完成 |
| 社区：管理员发布 | `community-pages.js` | 缺失 |
| 任务：排队 / 暂停 / 继续 / 取消 / 删除 | `tasks.js` | 基础完成 |
| 运行中的游戏 | `games.js` | 缺失 |
| 资源收藏 | `bookmarks.js` | 缺失 |
| 目录切换 / 添加根目录 | `directory.js` | 缺失 |
| 视觉逐页对齐与 960×640 可滚动 | `styles.css` | 部分（主题与壳） |

## 实施顺序

1. **壳与中间层（已完成第一版）**：`LauncherState` 类型化，`LauncherFacade` 暴露状态事件与强类型命令；
   `ShellWindow` 改为 `PageHost` 页面宿主，设置页落到 `Views/SettingsView`。
2. **设置页（已完成第一版）**：启动、Java、数据源、界面、服务器收藏、存档、恢复、日志、诊断、关于、社区密钥。
3. **实例页（下一步）**：实例切换、版本设置、启动参数、模组表格、克隆/导出/卸载/更新。
4. **下载页（进行中）**：社区推荐、原版、模组/整合包/资源包/光影/数据包、详情、版本选择、安装、加载器、整合包导入。
5. **主页与账号**：账号卡、皮肤/披风、MUA 档案、服务器、公告、推荐、语录、版本选择。
6. **外围工具**：任务、运行中游戏、收藏、目录、管理员发布。
7. **删除 WebView 主界面**：删除 `Ui/`、`MainWindow`、`bridge.js`、Web 边界检查与 `WebUiChecks`；
   更新 `scripts/build.ps1`、CI 与文档；保留 `MicrosoftLoginWindow` 的 WebView2。

## 删除阶段门槛

- 上表所有功能为“已完成”，且 `dotnet build`、C# 格式、布局/版本检查通过。
- 真实账号登录、真实下载安装、启动/退出、删除与恢复在 Windows 上人工验收。
- 发布包不再包含 `Ui/` 资源；微软登录窗口仍可独立打开并完成 OAuth。
- 删除 `Ui/` 后，`scripts/check-web-boundaries.py` 改为检查原生页面不反向依赖 Web 协议或 WebView。
