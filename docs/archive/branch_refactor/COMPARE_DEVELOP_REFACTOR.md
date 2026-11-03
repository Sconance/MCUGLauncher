# develop 与 refactor 功能对照

> 历史归档：本文记录 refactor 时期的一次性过程，`refactor` 分支已废弃。现行文档见 [开发导览](../legacy-wpf/DEVELOPMENT.md) 与 合并到主分支前的说明（历史文件已退役）。


> 本文记录 0.20.1 / 0.20.2 功能从 `develop` 迁入 `refactor` 的过程，表中的路径是 `refactor` 当时的 `MainWindow/**` 与 `UI/` 结构。`refactor` 已合入 `test`（`145bf37`），现行路径与迁移去向见 [refactor 分支整合记录](REFACTOR_INTEGRATION.md)，功能定位见 [开发导览](../legacy-wpf/DEVELOPMENT.md)。文末验证记录属于 `refactor`，不作为 `test` 的验收依据。

## 比较范围

- 比较基线：`origin/refactor...origin/develop`。
- 共同基点：`69a41a60d5e5beaa923e2854b7f65e266efac78b`。
- 比较时 refactor 独有 10 个提交、develop 独有 3 个提交；develop 侧改动约 33 个文件，新增约 1,361 行、删除约 68 行。
- 迁移遵循 refactor 的目录和 partial 结构，没有把 develop 的旧根目录 `MainWindow` partial 覆盖到当前实现。

## 功能差异与迁移

| develop 新增或变化 | refactor 原状 | refactor 中的迁移位置与结果 |
|---|---|---|
| 0.20.1 下载分类视觉和加载器配置概览 | 下载分类与加载器卡片已在 `DownloadPageUI` 中组合 | `UI/Downloads/DownloadPageUI.cs` 保留下载页布局，`UI/Downloads/LoaderOverviewUI.cs` 独立构建 Forge、NeoForge、Fabric、Quilt 版本选择和安装行；分类图标放在 `Models/DownloadNavigation.cs`。本地官方安装器仍保留在高级折叠区。 |
| 响应式资源卡片与资源详情工作区 | 已有搜索、版本选择和独立下载流程，但详情仍在搜索结果旁 | `UI/Content/ResourceWorkspaceUI.cs` 将详情改为资源页内工作区；`ResourceSearchLayoutUI.cs` 构造搜索与筛选布局；`ResponsiveResourcePanel.cs` 按可用宽度排列卡片，窄视图可切换列表。资源详情在窄窗口改为上下排列。 |
| 项目介绍、图片、标签、版本事实与依赖摘要 | 资源模型已含标题、描述、图标、标签；收藏系统有独立持久化 | `UI/Content/ResourcePresentationUI.cs` 从 Modrinth 加载有限长度的纯文本介绍和最多三张受限域名图片；详情展示版本信息及依赖摘要。版本选择和下载仍复用原流程。 |
| 心形收藏、仅收藏筛选、排序和列表/卡片切换 | 已有收藏夹、标签、批量下载、项目收藏标记和排序 | `UI/Content/ResourceFavoritesUI.cs` 扩展在线资源卡片与详情收藏，复用 `ResourceBookmarkStore`；收藏筛选在本地收藏全量数据上检索，并按当前游戏/加载器/发布类型验证兼容性。普通搜索支持 relevance、downloads、updated 排序；旧版 `ContentService.Search` 签名继续保留。 |
| 八个区域的代码级菜单注册 | 尚无统一的可扩展菜单注册入口 | `Models/LauncherMenuRegistry.cs` 定义区域、上下文、页面工厂和注册表；`UI/Shell/MenuExtensionsUI.cs` 接入顶部、下载、设置、更多、主页和资源详情；版本设置视图及实例内容对话框接入对应临时菜单。仅加载源码中注册的页面，不加载下载或远程插件代码。 |
| 品牌资源、版本信息与回归断言 | 版本为 0.20.0，图标为旧资源 | refactor 曾以 **0.21.0** 发布；图标与 Logo 已同步。合并后版本号改为单一来源 `Directory.Build.props`，见 [版本号控制](../../VERSIONING.md)；程序集版本断言改为与程序集元数据比较。 |

## 兼容和架构决定

- 收藏数据文件、收藏夹、标签及已有批量操作没有另起格式或迁移；在线资源的心形收藏直接调用现有存储服务。
- 新增的搜索排序是可选重载，原签名仍按 relevance 搜索，其他已有调用不要求同步改造。
- refactor 的 UI 逻辑按 `MainWindow/UI/Content`、`UI/Downloads`、`UI/Shell`、`Models` 与 `Presentation` 职责组织，保留主窗口现有 XAML 类型化控件入口；`test` 合并后改为 `Components/` 与 `Views/` 分工。
- `VersionSettings` 与 `InstanceContent` 扩展接收当前实例 ID；资源详情扩展按需创建页面，并在切换项目时清空旧内容。
- 可扩展菜单是源码级 API，不代表支持第三方插件或远程代码执行。页面应通过工厂为每个宿主创建独立的 WPF 控件。

## 相关说明

- [增加菜单栏目](../legacy-wpf/MENU_EXTENSIONS.md)
- [MainWindow 职责拆分](../legacy-wpf/MAINWINDOW_REFACTOR.md)
- [更新记录](../../../CHANGELOG.md)

## 验证记录（refactor 当时）

- `dotnet build src/MCUGLauncher/MCUGLauncher.csproj -c Release --no-restore`：通过，0 警告、0 错误。
- 常规检查通过：`Auth`、`Mua`、`Collection`、`ContentSafety`、`Dependency`、`Diagnostic`、`Download`、
  `MiniDownload`、`Queue`、`ResourceDownload`、`Library`、`Stability`、`Uninstall`、`Upgrade`、
  `Experience`、`VersionUi`、`WorkflowUi`、`Compatibility`、`InlineUi`、`DownloadUi`、`CompactUi`。
- `DownloadUiChecks` 覆盖资源详情工作区、窄/宽布局切换、心形收藏与收藏筛选、菜单注册与重复 ID 拒绝、
  下载扩展保留分类栏，以及分类图标支持图片槽。
- `CompactUiChecks` 使用真实版本清单与 Modrinth 数据验证响应式卡片多列布局、收藏筛选、加载器版本读取
  以及注入下载队列。
- 未执行：`IntegrationChecks`（需要已安装游戏、Java 与真实客户端矩阵），以及真实微软/MUA 授权。
