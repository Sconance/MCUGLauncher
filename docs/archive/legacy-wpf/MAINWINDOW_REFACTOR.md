# MainWindow 职责拆分

## 当前结构

原先 42 个 MainWindow 源文件中的职责已迁移到 41 个独立模块。MainWindow 只保留原生窗口、初始化组合与页内对话框适配；不再存放账号、下载队列、实例、资源查询等业务字段。

- `Composition/LauncherWorkspace.cs`：模块的构造与生命周期组合，不保存业务状态。
- `Composition/LauncherControls.cs`：九个页面视图的命名控件绑定，不通过字符串查找控件。
- `Composition/LauncherPageRouter.cs`：页面动作到具体模块的编译期可检查路由，不使用反射。
- `Views/Pages/`：启动、下载、资源、内容管理、设置、日志、任务、导航和标题区九个 XAML 视图。
- `Components/`：各功能控制器拥有各自字段、事件和操作。启动协调位于 `LaunchCoordinator`，版本设置位于 `VersionSettingsController`，实例管理位于 `InstanceWorkspaceController` 和 `InstanceContentController`，资源和任务也各自独立。
- `Views/`、`Presentation/`：第一批独立控件和加载器 Presenter 继续使用，不退回主窗口大类。

每个顶层类型的职责、入口与源码位置见 [类文档目录](classes/README.md)；页面与模块调整后同步更新对应页面。

## 页面组合与兼容

已有动态布局会移动或重组控件。为了保持视图树和行为，本轮由 `LauncherPage.Materialize` 将独立 XAML 的内容放入原来的宿主，保留页面工厂持有的命名控件与事件，随后使用强类型绑定连接控制器。页面不依赖 MainWindow 的私有字段。

窗口创建页面、组合所有模块，再执行启动初始化。控件初始化早期的动作不会访问尚未创建的工作区；后续页面动作通过专用路由调用模块。原生窗口接口和页内对话框接口保持兼容。

原有认证、启动、下载与实例 Core 实现未重写。账号加密熵、配置目录、JSON 字段和实例存储格式保持原样。独立的公开控件与展示模型仍保留原有公开契约。

## 开发约束

新增页面和栏目沿用 [菜单扩展接口](MENU_EXTENSIONS.md)。页面布局放在 Views，功能状态放在所属 Components 或 Presenter；不要把模块字段重新汇总到窗口或组合根。模块间协作应明确指出所属模块；后续可以继续将跨模块依赖收窄为专用接口，但不要再以 partial MainWindow 共享状态。

此结构不意味着所有模块都已零耦合，也不是另起一套 MVVM 框架。它完成的是窗口职责与页面/功能模块的分离；模块内部的大方法还可以按实际修改需求继续整理。

`scripts/check-ui-boundaries.py` 检查窗口大小边界、独立 XAML 页及生产代码中是否重新引入 MainWindow 功能文件；已接入 CI。

## 参考 refactor 的第二轮细化

以下记录合并前的迁移方式；`refactor` 随后已合入主干，结论见下文「refactor 分支合并与版本控制」。

本轮只修改 `test`。参考远程 `refactor` 的 `db0ab6a` 快照，保留现有独立 XAML 页面与窗口组合结构，逐项迁移小组件，不直接合并另一条分支。

- `AvatarController`、`HomeAccountController`、`KeyboardNavigationController`、`LoaderOverviewController`、`NetworkController`、`WindowChromeController` 不再接收整个 `LauncherWorkspace`。它们通过构造参数接收实际需要的控件、状态读取函数与操作回调。
- 账号状态快照 `PlayerIdentityState` 与视图分离；刷新时读取当前状态，取消令牌和刷新序号仍由头像组件管理。
- 复用 `refactor` 的 `GameWindowBehaviorController`、`InstanceSearchController`、`OfflineSkinPackBuilder`、`LaunchScriptBuilder`，分别负责游戏窗口行为、实例列表筛选、离线皮肤包和安全引用启动脚本参数。
- `Models/` 单独存放 `BookmarkView`、`InstanceTarget`、`InstanceView`、`DownloadCategoryIcon` 和账号展示状态。保留原命名空间与类型可见性。
- `Views/Forms/` 放置启动参数与网络设置表单；`Views/Shared/FeatureUiElements` 提供通用标题、说明和动作按钮。表单不直接访问配置存储或其他模块。
- `HomeWelcomePresenter` 只处理默认欢迎区域的外观。

组合根负责连接回调，所有模块构造完成后才执行初始化。需要后续状态的回调延迟读取，不捕获过时的 HttpClient 或账号状态。

仍有应用级控制器依赖 `LauncherWorkspace`；实例页面和资源工作区还可以继续分解。没有宣称完成整个应用的依赖解耦，也没有改动 Core、认证协议、配置格式或版本号。

## 版本设置页面进一步拆分

`VersionSettingsController` 从约 750 行缩减到约 125 行，负责页内窗口生命周期、初始栏目和导航；页面主体不再放在一个 Render 方法的分支中。

- `Views/VersionSettings/VersionSettingsView.cs`：布局、页头和统一的卡片、字段及按钮。
- `VersionOverviewPage`：版本个性化、快捷方式和维护入口。
- `VersionLaunchPage`：当前实例启动选项与 Java 配置。
- `VersionLoaderPage`：兼容加载器查询、安装与实例维护。
- `VersionModPage`：本实例 Mod 列表、筛选及管理操作。
- `VersionExportPage`：整合包选项和启动脚本入口。
- `Components/VersionIdentityEditor`：名称、描述、分类等身份信息的编辑与保存。
- `Presentation/VersionSettingsPageContext`：当前实例的数据、页面视图与重绘入口，不持有 MainWindow 或工作区。
- `Presentation/VersionTaskActions`：任务执行、取消令牌和运行时网络访问的少量明确能力；每页再单独声明所需的动作记录。
- `Composition/VersionSettingsComposition`：把已有应用操作接到页面，保持下载、账号和实例服务实现不变。

页面构建器不接收整个工作区，也不通过反射查找应用操作。新增的检查限制协调器大小，并防止独立页面重新依赖 MainWindow 或 LauncherWorkspace。

## 按 refactor 延续的第三轮组件整理

原内容管理约 603 行的 `InstanceContentController` 缩减为约 285 行，内容管理对话框迁入 `Presentation/SketchContentManagerDialog`。原约 553 行的 `InstanceWorkspaceController` 缩减为约 322 行，启动设置、整合包导出和公共对话框构造分别迁入同名参考组件。更新选择/预览、工具提示、工具窗口初始化及导航样式直接复用参考实现。

完整相同的文件、适配的文件及保留差异见 [refactor 延续记录](archive/branch_refactor/REFACTOR_CONTINUATION.md)。没有把整个 MainWindow 改名后交给新组件；独立对话框与更新模块不持有 LauncherWorkspace。

## 验证范围与限制

测试仅在 `test/Support/LauncherTestAccess.cs` 中使用模块检查适配器，原有功能断言继续执行，不在生产窗口保留用于兼容测试的私有字段或业务方法。该适配器仅链接到检查/预览项目。

真实微软/MUA 人工授权及完整游戏启动矩阵未重跑。组件检查、真实资源下载和合成实例检查不能替代这些验收。本轮尚未发布，不覆盖现有 Release 与本地正式 EXE。

## refactor 分支合并与版本控制

`refactor`（`db0ab6a`）已在 `145bf37` 合入主干，父提交为 `0eaf5ec`（原文 `test`）与 `db0ab6a`。合并保持本文件的窗口 shell、九个 XAML 页面与组合根结构；`refactor` 的 86 个 `partial MainWindow` 文件与 51 篇逐类文档按结构退役，逐项去向、退役理由与验证记录见 [整合记录](archive/branch_refactor/REFACTOR_INTEGRATION.md)。

- 版本号改为单一来源：`Directory.Build.props` 的 `McugVersion` 派生程序集元数据，运行期读取 `ProductInfo.Version`，界面、User-Agent 与诊断信息不再写死；规则见 [版本号控制](VERSIONING.md)。
- 补充 `scripts/check-version.py` 与 CI 检查，避免 CHANGELOG、README、程序集版本与标签再次不一致。
- 剩余拆分工作（实例概览、资源工作区、任务与启动协调、400 行以上模块、前后端分离）记录在 [重构路线图](../TODO.md)，仍不在窗口内恢复业务字段或 partial 组织。
