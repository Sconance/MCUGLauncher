# refactor 组件在 test 中的延续

> 历史归档：本文记录 refactor 时期的一次性过程，`refactor` 分支已废弃。现行文档见 [开发导览](../legacy-wpf/DEVELOPMENT.md) 与 合并到主分支前的说明（历史文件已退役）。


参考基线固定为 `db0ab6af7cde32ca685de7ff3371cfefdf308f18`。合并前的这一轮从本地 `test` 的 `156208e` 继续，只向 `test` 提交，没有移动或改写 `refactor` 的分支指针，也没有修改当时的正式版本号；随后的合并与版本统一见文末。

## 完整复用的组件

以下 11 个完整文件与参考提交逐字一致，比较仅统一 UTF-8 BOM 与 CRLF/LF。六个已在此前迁移，五个在本轮接入。不是只比较类名或文件名。

| refactor 的 MainWindow/ 下路径 | test 的 src/MCUGLauncher/ 下路径 |
| --- | --- |
| Builders/LaunchScriptBuilder.cs | Builders/LaunchScriptBuilder.cs |
| Builders/OfflineSkinPackBuilder.cs | Builders/OfflineSkinPackBuilder.cs |
| Controllers/GameWindowBehaviorController.cs | Components/Launch/GameWindowBehaviorController.cs |
| Controllers/InstanceSearchController.cs | Components/Instances/InstanceSearchController.cs |
| Models/PlayerIdentityState.cs | Models/PlayerIdentityState.cs |
| Builders/LaunchOptionsForm.cs | Views/Forms/LaunchOptionsForm.cs |
| Presentation/ContentUpdateSelectionView.cs | Views/Updates/ContentUpdateSelectionView.cs |
| Presentation/ContentUpdatePreviewView.cs | Views/Updates/ContentUpdatePreviewView.cs |
| Initialization/UtilityWindowInitializer.cs | Initialization/UtilityWindowInitializer.cs |
| Initialization/LauncherTooltipInitializer.cs | Initialization/LauncherTooltipInitializer.cs |
| Styling/NavigationMenuStyle.cs | Styling/NavigationMenuStyle.cs |

`scripts/refactor-reuse.json` 记录参考来源与内容哈希。`scripts/check-refactor-reuse.py` 检查工作区，也可用 `--revision origin/test` 检查已推送的目标文件。CI 无需下载另一个分支即可检查固定基线。后续若有正确性修复，应明确改为“适配组件”并更新来源说明，不应为保持一致而保留缺陷。

## 延续原实现、适配依赖的组件

- `Presentation/LaunchOptionsDialog.cs`：沿用同名参考组件，保留启动选项克隆、校验、全局字段联动、个性化与离线皮肤设置；用 `LaunchOptionsActions` 接收配置与保存操作。
- `Presentation/InstancePackExportDialog.cs`：沿用同名参考组件，保留导出选项、保守的存档默认值、保存选择和任务执行顺序；接收与导出有关的少量回调。
- `Presentation/FeatureDialogBuilder.cs`：沿用参考布局、尺寸、按钮分组和准备逻辑；接收普通 Window 所有者与主题配置回调。
- `Presentation/SketchContentManagerDialog.cs`：按参考的同名类和 Open 入口提取内容管理；数据读取、管理命令、生命周期绑定分别传入，不接收整个 MainWindow 或 LauncherWorkspace。继续使用 test 的菜单扩展宿主和已验收的实例名称同步，而不是覆盖回旧实现。
- `Components/Resources/ContentUpdatesController.cs`：延续参考 `ContentUpdateController` 的检查、选择、预览、确认和入队流程，直接使用上表中的两个原始视图；保留 test 已有的事件入口名称，改用 `ContentUpdateActions`。
- `Builders/ContentSearchFilterBuilder.cs`：沿用参考实现，仅将 MainWindow 的通用按钮工厂改为现有 `FeatureUiElements`。重置回调继续清除 test 的收藏展示条件。

`Composition/InstanceDialogComposition.cs` 负责把既有服务连接到上述组件。适配代码属于组合层，不把新的业务字段重新塞回主窗口。上述文件有必要的依赖差异，不能宣称完整文件逐字相同。

## 保留的 test 差异

- 精简的原生窗口和九个独立 XAML 页面继续保留。
- 版本设置的五个独立页面继续保留。
- Esc 优先关闭展开选择器的修复继续保留。
- 嵌套编辑后实例列表、选中项和首页名称同步继续保留；内容管理关闭时解除订阅。
- 资源收藏、兼容筛选、现有下载/认证协议及实例数据格式保持原有实现；Core 无改动。

## 检查要求

运行全部无需真实游戏环境的检查；额外断言参考启动设置对话框可以保存并同步全局字段，参考整合包导出对话框保留五个选项且默认不包含存档。已有更新确认、目录切换、名称同步、Esc 路由、拖放与菜单扩展断言继续执行。

`test` 的推送现已触发 Windows CI，并加入固定复用基线、UI 边界及适合 CI 的组件检查。真实授权、完整游戏启动矩阵、多显示器 DPI 与本轮原生桌面点击不能由这些检查代替。

## 资源收藏与搜索布局的进一步解耦

在上述基线上继续拆分资源页，不新增业务功能：

- `ResourceFavoritesController` 不再持有 `LauncherWorkspace`，改为收藏相关控件和 `ResourceFavoriteActions`。收藏存储仍由原来的 `ResourceBookmarkStore` 读写，初始化后通过回调获取。
- `ResourceFavoriteSearch` 独立负责收藏候选、兼容版本筛选、四路并发查询、排序和分页；输入是不可变的 `ResourceFavoriteQuery`，异步过程不再读取 WPF 控件。
- `ResourceSearchLayoutController` 接收搜索页所需控件、收藏/筛选组件和刷新回调；布局继续复用现有 `ResourceSearchHeader`，不改变视觉结构。
- `Composition/ResourceComposition.cs` 集中连接依赖。搜索布局需要的标题控件在资源工作区组件创建后传入；选择事件回调直接保留原方法委托，确保临时解除事件订阅仍然有效。

这部分延续参考分支的资源收藏、搜索布局职责，保留 test 的独立视图组织，不属于新增的完整逐字复用文件。前面的 11 文件复用基线继续检查。

`CollectionChecks` 现在直接编译独立查询组件，以本地收藏和 HTTP 替身检查分页无重复、类型和下载量排序、发布通道过滤、最多四路请求及取消传播；无需窗口或实际网络。UI 边界脚本同时禁止两个控制器重新引入整个工作区，也禁止查询组件依赖 WPF。

## 与 refactor 合并后的状态

参考基线 `db0ab6af7cde32ca685de7ff3371cfefdf308f18` 已作为第二父提交并入 `test`（合并提交 `145bf37`），因此 `scripts/refactor-reuse.json` 仍按该基线校验逐字复用的 11 个文件，无需改动基线。合并决定、退役文档与迁移去向见 [refactor 分支整合记录](REFACTOR_INTEGRATION.md)。
