# 开发导览

MCUG Launcher 是 .NET 8 / WPF 应用。先从本文件定位功能，再阅读对应入口。
构建、打包和运行检查见 [BUILD.md](BUILD.md)。

## 先读哪些文件

1. `src/MCUGLauncher/MainWindow.xaml.cs`：原生窗口、页面组合和页内对话框衔接。
2. `Composition/LauncherWorkspace.cs`：模块组合；`LauncherControls.cs`：页面控件的强类型绑定。
3. `Views/Pages/`：九个独立页面/导航视图的 XAML；`App.xaml`：公共样式。
4. `Components/`：按功能分开的控制器，拥有各自状态；`Presentation/` 与 `Views/`：已独立提取的视图和 Presenter。
5. `src/MCUGLauncher.Core/`：启动、认证、下载和实例等业务服务。
6. `test/`：原有断言继续执行，测试检查入口改为定位所属模块。

页面动作经 `LauncherPage` 事件边界进入 `Composition/LauncherPageRouter.cs` 的强类型路由。
控制器通过 `workspace.Controls` 使用页面输入，通过 `workspace.Window` 使用原生窗口能力，跨模块协作通过显式的模块属性。
`LauncherWorkspace` 只负责组合，不拥有下载、账号或实例业务状态。不要重新添加 `partial MainWindow` 功能文件。
页面迁移时保留原有动态布局的视图树形状；详细说明见 [MainWindow 重构记录](MAINWINDOW_REFACTOR.md)。

每个顶层类型的职责、主要入口与源码位置见 [类文档目录](classes/README.md)；新增或拆分类型时同步补页，`python scripts/check-class-docs.py` 校验覆盖与索引。

## 功能域与命名

`Components/` 按功能域分组，类名使用“功能域 + 角色”的形式（`Controller`、`View`、`Presenter`、`Builder`、`Registry` 等），保证同类职责在同一目录、名字能直接说明用途：

| 目录 | 职责 |
| --- | --- |
| `Components/Accounts/` | 账号面板、头像与登录入口 |
| `Components/Shell/` | 窗口外壳、导航、键盘、菜单扩展与网络设置 |
| `Components/Downloads/` | 下载页、队列、任务、迷你下载条与加载器版本 |
| `Components/Resources/` | 资源浏览、详情工作区、收藏与内容更新 |
| `Components/Instances/` | 实例列表、内容管理、版本设置与卸载 |
| `Components/Launch/` | 启动协调、游戏窗口行为、任务恢复与整合包 |
| `Components/Experience/` | 个性化与外观 |
| `Components/Bookmarks/` | 收藏夹与书签库 |
| `Components/Support/` | 诊断与反馈 |

本次整理的代表性改名：`SketchController` → `InstanceContentController`、`InstancePagesController` → `InstanceWorkspaceController`、`ContentController` → `ResourceBrowserController`、`CollectionsController` → `BookmarkLibraryController`、`UpdatesController` → `ContentUpdatesController`、`LoaderController` → `LoaderSelectionController`、`PackController` → `ModpackController`、`StabilityController` → `TaskRecoveryController`、`MiniDownloadController` → `DownloadMiniBarController`、`ModernController` → `ModernShellController`。`scripts/refactor-reuse.json` 固定的 11 个逐字复用文件保留 refactor 中的类名，只调整目录；其余类名可按功能调整，调整后必须同步 [类文档目录](classes/README.md)。

## 功能定位

| 想修改的内容 | 界面入口 | 服务或数据 |
| --- | --- | --- |
| 账号面板、登录方式 | `Components/Accounts/HomeAccountController.cs` | `Authentication/` |
| 主导航、窗口拖动、大小适配 | `Components/Shell/ModernShellController.cs`、`Components/Shell/CompactShellController.cs`、`Components/Shell/WindowChromeController.cs` | 窗口自身 |
| 主页版本选择、版本内容管理 | `Components/Instances/InstanceContentController.cs`、`Components/Instances/VersionSettingsController.cs` | Core `Domain/Instances.cs`、`Application/InstanceManagement.cs` |
| 版本和资源下载 | `Components/Downloads/DownloadPageController.cs`、`Components/Resources/ResourceBrowserController.cs` | Core `GameInstaller.cs`、`ContentService.cs` |
| 下载队列与任务操作 | `Components/Downloads/DownloadQueueController.cs`、`Components/Downloads/DownloadTaskController.cs`、`Components/Downloads/DownloadMiniBarController.cs` | Core `Application/OperationCoordinator.cs` |
| 文件拖入、资源导入 | `Components/Instances/DropFeedbackController.cs`、`Components/Instances/InstanceLibraryController.cs` | Core `Application/LocalImportClassifier.cs`、`WorldArchives.cs` |
| 整合包导入、导出、升级 | `Components/Launch/ModpackController.cs`、`Components/Launch/PackUpgradeController.cs` | Core `PackService.cs`、`Application/PackUpgrade.cs` |
| Java 与游戏启动 | `Components/Instances/JavaManagerController.cs`、`MainWindow.xaml.cs` | Core `Application/Java*.cs`、`LaunchService.cs` |
| 主题、背景 | `Components/Experience/AppearanceController.cs`、`Components/Experience/ExperienceThemeController.cs`、`App.xaml` | Core `Domain/Preferences.cs` |
| 崩溃分析与反馈 | `Components/Support/DiagnosticsController.cs` | Core `Application/Diagnostics.cs` |

表中的 Core 路径相对于 `src/MCUGLauncher.Core/`；界面路径相对于 `src/MCUGLauncher/`。

## 如何扩展

- 先在 Core 定义业务操作，接收明确参数、取消令牌和进度回调；不要让服务访问 WPF 控件。
- UI 负责读取输入、调用操作和展示结果。长时间任务沿用现有 `Run` / 下载队列入口。
- 账号面板的三种内容分别在 `BuildOfflineIdentity`、`BuildMicrosoftIdentity`、`BuildMuaIdentity` 中构建；
  显示切换统一由 `UpdateAccountPanel` 处理。认证请求仍进入 `Authentication/` 服务。
- 下载任务卡片由 `BuildDownloadTaskCard` 创建；状态变更统一进入 `ChangeDownloadById`，
  再交给协调器处理。不要在按钮回调里直接修改任务文件。
  持久化任务模型在 Core `Domain/Operations.cs`，单次执行上下文在 `Application/OperationContext.cs`，
  异步提交边界在 `Application/OperationExecution.cs`。
- 为实际业务补充对应 `test/*Checks` 的边界检查。不要把预览窗口当作回归检查。
- 默认分支包含当前源码，Release 附件必须从同一提交构建。历史标签保持原样。

## 编码约定

- 一条执行语句一行，4 空格缩进。类型、公开成员使用 PascalCase；现有窗口私有成员保留当前 camelCase。
- 方法按职责命名；长方法优先按明确步骤提取。避免为了少写几行把多层条件、状态变更和副作用揉在一起。
- 普通条件用 `if`，状态映射可用 `switch`；三元表达式只用于容易读懂的值选择。
- 新增控件样式优先放在 XAML；C# 负责动态数据、事件和必要的控件组合。
- 注释解释约束、顺序、兼容性或原因。不要逐句翻译代码，也不要写无依据的保证。
- 不记录密码、令牌或个人游戏路径。不要随意改变 JSON 字段名、账号加密熵和存储目录。
- 内部 `BlockLaunch` 命名空间和目录仍用于兼容已有配置；用户可见品牌是 MCUG Launcher。

## 格式检查

从仓库根目录执行，使用 .NET 8 SDK 自带的 Roslyn 组件，不增加启动器运行时依赖：

```powershell
dotnet run --project tools/SourceFormatter -- --write
dotnet run --project tools/SourceFormatter
```

第二条只检查，未通过时返回非零退出码。工具排除 `bin` / `obj`，写入前逐一比较 C# token，
防止格式化改变字符串或表达式。原始字符串的行尾会保留，避免改变其运行时内容。
源码格式化不能证明业务正确；仍需构建与相应行为检查。

## 验证边界

多数检查采用隔离目录、模拟 HTTP 和合成实例，适合验证取消、恢复、文件提交和界面状态。
真实微软/MUA 授权、Java 兼容性和不同游戏整合包启动仍需实际环境验证，不能由格式检查代替。


## 0.20.1 下载界面扩展

- `Components/Downloads/DownloadNavigationController.cs`：分类名称与 `DownloadCategoryIcon` 映射。为条目的 `Image` 传入 `ImageSource` 即显示图片；留空时回退到字形，不影响分类键或下载逻辑。图片建议以 WPF Resource 加入项目，并使用 pack URI 加载。
- `Components/Resources/ResourceWorkspaceController.cs`：列表与页内详情切换、兼容信息与依赖数量展示、窄窗口布局。复用既有命名控件及事件，资源独立下载和整合包导入行为保持原样。
- `Components/Downloads/LoaderOverviewController.cs`：横向加载器配置行，从官方目录读取兼容版本；选择后调用既有 EnqueueDownload，不实现第二套安装器。
- 列表模板 `ResourceSearchItem` 位于 `App.xaml`。不展示服务端未提供的评价、点赞或截图。

- `ResponsiveResourcePanel.cs`：分页结果的自适应卡片排列；`Components/Resources/ResourceSearchLayoutController.cs`：搜索及带标签筛选布局。
- `Components/Resources/ResourcePresentationController.cs`：可选的项目介绍与截图展示。请求失败不影响版本选择和下载；截图限 Modrinth CDN，正文仅显示有长度上限的纯文本预览。


## 0.20.2 菜单与资源交互

- 新增栏目从 `MenuExtensions.cs` 注册，入口定义在 `Navigation/LauncherMenuRegistry.cs`，宿主适配在 `Components/Shell/MenuExtensionsController.cs`。具体区域、上下文和图片图标示例见 [菜单扩展说明](MENU_EXTENSIONS.md)。
- `Components/Resources/ResourceFavoritesController.cs` 负责收藏筛选、心形状态及视图切换，持久化复用 `ResourceBookmarkStore`，不创建第二份收藏文档。
- 收藏兼容性检查最多并发四个请求，并跟随浏览取消；资源服务保留原始 Search 签名，通过新增重载接收受限的排序值。

## test 分支的组件依赖细化

查看 [窗口拆分与依赖约束](MAINWINDOW_REFACTOR.md) 和 [分支结构比较](archive/branch_refactor/TEST_REFACTOR_COMPARISON.md)。账号展示、加载器概览、网络编辑及原生窗口控件通过显式构造参数连接；新的小组件优先接收实际控件、只读状态或操作回调。公共 UI 元素位于 `Views/Shared`，表单位于 `Views/Forms`，数据记录位于 `Models`，纯文件构建位于 `Builders`。

版本设置的页面主体位于 `Views/VersionSettings/`，动作连接在 `Composition/VersionSettingsComposition.cs`。新增或修改某一页时优先定位对应的 `Version*Page`，不要把页面代码重新加进版本设置协调器。

内容管理、全局启动选项和实例导出分别位于 `Presentation/SketchContentManagerDialog.cs`、`LaunchOptionsDialog.cs`、`InstancePackExportDialog.cs`，组合在 `Composition/InstanceDialogComposition.cs`。内容更新的两个视图位于 `Views/Updates`；通用初始化与导航样式位于 `Initialization`、`Styling`。与参考分支的继承关系和准确复用范围见 [延续记录](archive/branch_refactor/REFACTOR_CONTINUATION.md)。

## 版本号与分支

- 版本号唯一来源是根目录 `Directory.Build.props` 的 `McugVersion`；运行期读取 `ProductInfo.Version`，不要在界面、User-Agent 或诊断信息里写版本字面量。发版与标签规则见 [版本号控制](VERSIONING.md)。
- 0.22.0 在 `test` 分支准备合并到 `main`，步骤、验证与检查清单见 [合并到主分支前的说明](MERGE_TO_MAIN.md)。
- `refactor` 分支已废弃并归档，历史材料见 [历史归档](archive/branch_refactor/README.md)；不要重新引入 `partial MainWindow` 或把窗口字段汇总回组合根。
- 合并其他分支时遵循 [重构路线图](../TODO.md) 中的整合检查要点：按组件迁移、保留数据兼容、先查对应服务与 `test/*Checks`，并同步文档与版本号。

## 0.22.1：运行会话与本地优先启动

- `Core/Application/RunningGameRegistry` 管理真实子进程会话，按目录阻止重复启动，日志按会话 GUID 分开；不会依赖窗口、账号选择或全局单进程字段。
- `Components/Launch/RunningGamesController` 通过显式 UI 回调展示会话与退出诊断，关闭操作仅针对选中的会话；不调用 Process.Kill。
- `Components/Downloads/VersionCatalogController` 独立负责在线列表的按需请求、同目录请求合并、12 秒超时和旧目录响应抑制。组合层连接现有 CmlLib 版本服务。
- `StartupRecovery` 只完成本地事务恢复和实例列表刷新。首页刷新也只读取本地，进入原版/加载器下载页才请求在线元数据。在线等待不占用 OperationCoordinator 或禁用首页控件。
- 为保护共享 libraries/assets 和实例内容，安装队列及破坏性内容变更仍在有游戏运行时暂停；不同独立实例可继续启动，同一目录仅允许不同离线 ID 多开；相同 ID 或在线会话仍阻止重复启动。切换到已有运行会话的根目录时不对其执行恢复/迁移。

### 运行会话的展示端口

`RunningGamesController` 不再构造页面或引用 WPF。它把注册表映射成 `RunningGameRow` 快照，经 `IRunningGamesView` 更新展示，再按 ID 接收关闭/日志命令。`Views/RunningGamesView` 负责 WPF 控件构造，不接收进程会话、文件读取器或注册表。日志读取和诊断捕获由组合层注入 `RunningGameActions`。窗口关闭时控制器解除视图事件订阅。`SessionControllerChecks` 不启用 WPF，直接编译控制器与接口；`RuntimeChecks` 保留真实 WPF 视图和双测试进程的交互检查。

### 0.22.1 交互修订

- 运行会话登记包含运行期角色名和离线标记。同目录离线身份按忽略大小写的角色 ID 去重，不同 ID 可以共存；启动第二个会话时不重写语言和离线皮肤包配置。用户配置及认证协议没有新增字段。
- 控制器通过展示端口请求确认，确认后再次核对会话；GameCloseConfirmationView 只管理主窗口内的紧凑遮罩，不接触进程或注册表。
- 组合层负责真正显示运行列表。全局操作进度表面不再显示，下载进度只在标题栏右上角显示；任务列表保留文本状态与百分比。

### 设置与工具导航

- `SettingsNavigationController` 只处理纯数据栏目和选择命令，通过 `ISettingsNavigationView` 与展示分离；控制器继续在无 WPF 的 SessionControllerChecks 中直接编译。
- `SettingsNavigationView` 复用与下载页一致的导航样式和侧栏宽度，保留图标图片映射。`SettingsPageHost` 与 `SettingsPageContent` 管理惰性内容和清理，切换栏目或离开设置都会取消工具页面的后台操作、解除页面事件。
- `Composition/SettingsComposition.cs` 集中登记内置分类、连接原有工具页面工厂、兼容日志/任务旧路由。不把服务或工作区塞进新控制器。
- 原 `LauncherMenuArea.Settings` 与 `More` 的注册接口不变，两者分别进入设置侧栏的扩展设置/工具扩展组；原 More 头部菜单已删除。扩展刷新只更新栏目，不强制跳转或重建用户当前页面。
- 原有设置控件、保存回调和实例数据格式保持；工具方法拆出 Create...Page 工厂，其他入口仍可使用原来的内嵌对话框包装。没有复制业务流程或引入新的 UI 框架。

### 设置页面独立化补充

- `SettingsPageView.TakeSections()` 使用 XAML 命名引用交付启动设置和关于内容；`LauncherPage.PageHost` 在页面物化时记录真实宿主。设置组合层使用这些宿主及诊断页面引用进行路由，不依赖主标签顺序或按钮文案。
- `Views/Settings` 的外观、Java、服务器、存档页面仿照 `main` 的 `VersionSettings` 页面，接收明确的状态/操作回调；不依赖 `LauncherWorkspace`、`MainWindow` 或 `InlineDialog`。网络复用原独立 `NetworkSettingsForm`。
- 各工厂直接返回 `SettingsPageContent`。设置页面不再通过 `FromDialog`、`PrepareContent` 或 `ForceClose` 拆卸对话框。仅旧工具入口使用 `SettingsDialogPresenter` 包装同一页面，并在关闭时释放。
- 代码边界检查限制上述依赖方向；交互检查覆盖独立表单持久化、工具释放、旧 Java 入口和调整标签顺序后的导航。

### 0.22.2 下载与内容交互

- 游戏版本由可编辑 ComboBox 展示，合并本地真实版本和按需在线目录；加载器筛选按资源类型显示。
- ResourceDestinationView 只维护互斥目标选项。ResourceInstallPlanner 位于 Core，校验兼容性并生成既有 content 下载任务；目标目录、Minecraft 版本、加载器与资源版本在入队时固定。
- InstanceModManagementPage 为唯一 Mod 管理界面；组合层绑定实例目录和动作，旧 VersionModPage 只跳转，禁止重新加入业务或重复列表。
- HomeLaunchButtonView 只呈现启动状态，Launch 的 try/finally 保证恢复；仅已知百分比显示确定进度，其余显示不定动画。
- OperationCoordinator.DeleteHistory 只删除合法 ID 的已结束 JSON 记录，保留活动、排队、暂停任务及所有资源文件；UI 清空只针对已结束下载任务。

ResourceInstallationController 通过值快照、目录选择、队列与错误端口协调资源安装；在 ResourceInstallChecks 中不启用 WPF 直接编译，依赖组装位于 ResourceComposition。
