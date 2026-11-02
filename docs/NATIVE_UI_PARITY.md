# 原生 Avalonia UI 迁移：功能对齐与 WebView 退出条件

目标：用 Avalonia 原生页面替换 `src/MCUGLauncher.Avalonia/Ui/` 的 HTML/CSS/JS，业务仍只经过
`UseCases`、`Services` 与 `Core`。WebView 只保留给微软 OAuth 登录窗口。

**WebView 主界面已删除**：`Ui/`、`MainWindow`、`bridge` 和 `WebUiChecks` 已移除；
`WebView2` 仅保留在微软 OAuth 登录窗口。以下矩阵保留为迁移记录与发布前的多 DPI/真实环境验收事项。

约束：

- 新页面只通过 `LauncherFacade` 调用业务命令，不在页面拼 JSON，也不复制下载器、依赖解析器或 Java 扫描。
- 每个页面独立为 `Views/*View`，`ShellWindow` 只负责导航、窗口生命周期和页面宿主。
- 页面接入后同步更新本文件的矩阵。

## 功能矩阵（以 `Ui/*.js` 与 `LauncherApplication` 命令为准）

| 功能 | Web 入口 | 原生状态 |
| --- | --- | --- |
| 实例列表与启动 | `home-page.js` | 已完成（启动、刷新、按实例启动） |
| 实例选择 / 版本选择 | `home-page.js` | 部分完成（主页实例列表/设为当前；版本分组检索待接） |
| 账号卡与登录入口 | `home-page.js`、`accounts.js` | 已完成（账号卡 + 账号页切换） |
| 皮肤、披风、MUA 档案 | `avatars.js`、`mua-profiles.js` | 已完成可用部分（头像、离线 PNG 皮肤、MUA 多角色选择；Web 的皮肤站/披风为占位，未接后端） |
| 主页服务器状态 | `home-page.js`、`host.js` | 已完成（自动查询、添加/编辑/删除） |
| 公告 / 推荐 / 语录 | `home-page.js`、`community-pages.js` | 已完成文本展示、本地 data URL 与远程 http(s) 配图（有界下载） |
| 下载分类与搜索 | `resource-pages.js` | 已完成（五类 Modrinth 搜索） |
| 资源详情 / 版本选择 / 安装 | `resource-pages.js`、`resource-picker.js` | 已完成基础流程（版本选择、实例安装/目录下载） |
| 原版版本列表 / 安装 | `resource-pages.js` | 已完成 |
| 加载器安装 | `install.js` | 已完成 |
| 整合包预览 / 导入 | `pack-import.js` | 已完成界面接入，待真实整合包验收 |
| 实例内容管理（模组启停/删除/更新） | `instance-pages.js` | 已完成可用部分（启停、删除、实例级内容更新；Web 的逐项更新列同为占位） |
| 实例设置（改名、分类、文件夹） | `instance-pages.js`、`launch-options.js` | 已完成可用部分（显示名、启动参数、文件夹；分类/收藏为 Web 占位） |
| 克隆 / 导出 / 卸载预览 | `instance-tools.js` | 已完成 |
| 内容更新检查 / 预览 / 执行 | `instance-tools.js` | 已完成界面接入，待真实内容验收 |
| 设置：启动参数 | `settings-page.js` | 已完成（目录、Java、内存、窗口、离线名） |
| 设置：Java 扫描 | `settings-actions.js` | 已完成 |
| 设置：网络 / 数据源 | `settings-page.js`、`data-sources.js` | 已完成（编辑、测试、默认/自定义切换、写入接口） |
| 设置：界面 / 恢复窗口 | `settings-actions.js` | 已完成 |
| 设置：服务器收藏 | `settings-page.js` | 已完成（增删改、查询） |
| 设置：存档备份 / 恢复 | `saves.js` | 已完成 |
| 设置：运行日志 / 诊断 | `settings-actions.js` | 已完成 |
| 设置：关于与许可 | `settings-page.js` | 已完成 |
| 社区：匿名 / 身份密钥 | `community-pages.js` | 已完成 |
| 社区：管理员发布 | `community-pages.js` | 已完成（管理员密钥、发布公告、公告/推荐删除管理） |
| 任务：排队 / 暂停 / 继续 / 取消 / 删除 | `tasks.js` | 基础完成 |
| 运行中的游戏 | `games.js` | 已完成（列表与关闭；真实游戏验收待做） |
| 资源收藏 | `bookmarks.js` | 已完成（列表、取消收藏、按兼容版本安装到实例或目录） |
| 目录切换 / 添加根目录 | `directory.js` | 已完成（设置页已保存目录切换、添加并切换） |
| 视觉逐页对齐与 960×640 可滚动 | `styles.css` | 已完成基础（蓝白主题、卡片、导航高亮；960×640 与 1280×800 截图验收，多 DPI 待发布前复核） |

## 实施顺序

1. **壳与中间层（已完成第一版）**：`LauncherState` 类型化，`LauncherFacade` 暴露状态事件与强类型命令；
   `ShellWindow` 改为 `PageHost` 页面宿主，设置页落到 `Views/SettingsView`。
2. **设置页（已完成第一版）**：启动、Java、数据源、界面、服务器收藏、存档、恢复、日志、诊断、关于、社区密钥。
3. **实例页（已完成第一版）**：实例切换、启动参数、模组启停/删除、克隆/导出/卸载/更新；
   分类与收藏等描述字段仍待接。
4. **下载页（已完成第一版）**：原版、五类资源搜索、详情、版本选择、加载器、整合包导入；
   社区推荐内容仍由主页展示，下载页内导航后续按视觉稿补。
5. **主页与账号（已完成第一版）**：账号卡、实例列表/启动、服务器状态、公告/推荐/语录；
   账号头像、离线皮肤、MUA 多角色选择。披风与在线皮肤上传待接。
6. **外围工具（已完成第一版）**：任务、运行中游戏、收藏（含按版本安装）、目录、
   管理员公告发布与公告/推荐删除管理已接入。
7. **删除 WebView 主界面（已完成）**：删除 `Ui/`、`MainWindow`、`bridge.js`、Web 边界检查与
   `WebUiChecks`；更新 CI 与文档；保留 `MicrosoftLoginWindow` 的 WebView2。

## 删除阶段门槛

- 上表所有功能为“已完成”，且 `dotnet build`、C# 格式、布局/版本检查通过。
- 真实账号登录、真实下载安装、启动/退出、删除与恢复在 Windows 上人工验收。
- 发布包不再包含 `Ui/` 资源；微软登录窗口仍可独立打开并完成 OAuth。
- 删除 `Ui/` 后，边界检查已改为 `scripts/check-native-boundaries.py`，检查原生页面不反向依赖
  Web 协议或 WebView，并限制 WebView2 只出现在微软登录窗口。
