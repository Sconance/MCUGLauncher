# 主窗口完整拆分回归（本地重构分支）

- 原先 42 个 MainWindow 功能源文件已迁入 41 个独立模块，九个页面/框架区域 XAML 已独立。
- 21 个检查程序全部通过（除需要额外真实游戏环境的 IntegrationChecks）：包括原有账号、MUA、下载、取消恢复、内容安全、实例卸载、版本设置、主题、诊断、整合包升级和页内工作流断言。
- CompactUiChecks 继续执行真实 Modrinth 搜索、资源下载、收藏筛选及官方加载器版本获取/入队。
- 源码格式、项目引用、界面职责边界检查和 Release 构建通过。
- 真实微软/MUA 人工授权、完整真实游戏启动矩阵及原生鼠标/多 DPI 验收未重跑。本次没有覆盖正式 EXE 或发布 Release。

# 0.20.2 验证

- 下载界面检查覆盖心形收藏写入/取消、整个收藏集合筛选、视图切换、菜单注册/重复 ID 拒绝、页内导航及详情标签延迟创建。
- 真实 Modrinth 搜索、独立下载、收藏筛选及官方加载器版本读取/入队通过。
- 相关界面与工作流回归、收藏存储和独立下载检查通过。构建与源码格式、项目引用检查通过。
- 检查使用隔离配置与合成实例；未改写用户配置和存档。本轮不包含真人授权及完整游戏启动矩阵，组件渲染不能替代原生鼠标、多 DPI 验收。

# 0.20.1 下载界面验证

- Release 构建 0 警告、0 错误；完整版和 Lite 单文件发布成功。
- DownloadUiChecks：页内详情、依赖数量、宽窄布局、返回列表、图片图标接口、分类及加载器入口检查通过。
- CompactUiChecks：真实 Modrinth 搜索、多列卡片、版本选择、独立资源下载及游戏目录未改变检查通过；官方加载器版本读取和所选版本入队检查通过。
- InlineUiChecks、WorkflowUiChecks、MiniDownloadChecks 通过。
- 29 个项目的引用和资源布局检查通过；源码格式检查通过。
- 渲染图覆盖 1220×820 与 900×640。组件渲染不等同于原生鼠标输入、多 DPI 和真人授权验收；本轮未重跑完整游戏启动矩阵。

以下保留历史验证记录。

# 验证记录

## 0.20.0 — 2026-10-27

- Release 构建：0 警告、0 错误；源码格式检查通过；29 个项目的引用与资源路径检查通过。
- 21 个检查程序通过：Auth、Mua、Collection、ContentSafety、Dependency、Diagnostic、Download、
  Library、ResourceDownload、Stability、Uninstall、Upgrade、Experience、DownloadUi、VersionUi、
  CompactUi、InlineUi、MiniDownload、Queue、Compatibility、WorkflowUi（均为对应的 `*Checks` 工程）。
- 覆盖模拟认证与凭据保护、下载续传/取消、事务回滚与恢复、实例删除边界、资源依赖、
  整合包导入升级、账号显示和内嵌任务交互；CompactUi 另完成真实版本清单、Modrinth 搜索和独立资源下载。
- 更新 CompactUi 的异步等待与当前主页断言；更新版本断言并消除 DiagnosticChecks 的可空警告。
- 格式化前后以 C# token 比对保护表达式和字符串，任务类型分文件后保持同一命名空间和公开接口。
- 未执行需要真人凭据的真实登录，也未重跑完整真实游戏/整合包启动矩阵；组件渲染不等于原生鼠标与多显示器 DPI 验收。

以下保留历史记录，不能作为当前版本的全部验证结论。

## 历史记录 — 2026-10-20

## 0.2 增量验证

- Release 编译及 Windows x64 自包含发布：0 警告、0 错误。
- 真实 Modrinth 搜索与 Minecraft 1.21.1 / Fabric 版本过滤：通过。
- Sodium Extra 0.9.4 与必需依赖 Sodium 0.8.13：解析、下载、文件大小与 SHA-512 校验通过。
- 模组禁用、重新启用、重复安装不留重复文件、移至备份：通过。
- 错误哈希不改变已安装实例、提交中途失败回滚、路径穿越文件名拒绝：通过。
- 资源包搜索及 Fresh Animations 下载、大小 / SHA-512 校验、安装清单登记：通过。
- Quilt 0.30.1 / Minecraft 1.21.1：稳定版选择、真实安装与启动命令构建通过，未实际进入 Quilt 游戏。
- 已检查新版首页、导航、内容搜索与自定义下拉菜单；补齐了控件可访问性，修正了初始页面选择。
- 新安装模组及资源包的实际游戏效果、Forge / NeoForge 官方安装器交互、微软登录尚未在此轮实测。

## 0.1 基础链路验证

环境：Windows x64、.NET SDK 8.0.426、CmlLib.Core 4.1.0、Java 21。

| 项目 | 结果 |
| --- | --- |
| Release 自包含发布 | 通过，0 警告 / 0 错误 |
| 发布版窗口、首页、设置页 | 已打开并检查，修正文字对比度与按钮布局 |
| 官方版本列表 | 成功取得 920 条记录 |
| 原版 1.21.1 下载 | 4362 项文件任务完成 |
| 原版 1.21.1 离线账号启动 | 进程启动并持续运行 25 秒，随后测试程序关闭游戏 |
| 独立实例参数 | 启动参数包含指定实例路径 |
| 网络请求被本地拒绝时构建缓存版本启动命令 | 通过；修正为使用 LocalJsonVersionLoader |
| 断网条件下自动选择已缓存 Java | 通过 |
| Fabric 0.19.5 / Minecraft 1.21.1 | 安装成功，启动命令构建成功 |
| 微软正版登录、正版服务器连接 | 未实测，需要用户本人拥有游戏并完成授权 |
| Fabric 游戏实际运行、模组兼容性 | 未实测 |
| 全部历史 / 最新游戏版本 | 未覆盖，不能据此承诺全部版本兼容 |

离线账号运行时，游戏日志出现在线账号属性请求 401，游戏进程仍持续运行；这不表示离线账号可以使用正版在线服务。测试验证了本地启动链路，没有测试创建存档、游玩和所有硬件驱动组合。

现有版本是开发预览版，不等同于 PCL / HMCL 的完整功能集；缺失项目见 README。

## test 分支依赖细化验证

在原有窗口拆分基础上继续提取组件后，Release 构建通过，21 个无需真实游戏环境的 `*Checks` 检查全部通过。头像显式依赖调整完成后，又执行了 Experience、InlineUi、WorkflowUi、CompactUi 四个相关检查，均通过。

新增的构建器断言覆盖 CMD 百分号与空格转义、PowerShell 单引号转义、账号类型路由、在线实例不生成离线皮肤包，以及现代资源包格式和传统皮肤路径兼容。没有使用真实用户实例作为测试数据。

格式、项目布局与 UI 边界检查通过。生产 Core 无改动；未执行真实微软/MUA 人工授权、全游戏版本启动矩阵及本轮原生桌面点击验收。GitHub CI 结果应以远程任务为准。

## test 分支版本设置细分验证

五个版本页面与身份编辑器独立后，重新执行全部 21 个无需真实游戏环境的检查，均通过。InlineUiChecks 新增逐页切换断言，确认概览、启动设置、加载器修改、Mod 管理、整合包导出仍展示原有操作入口。启动参数读写、页内窗口、内容更新与网络设置持久化的现有断言继续执行。

另外逐页比对原页面与提取后 Build 方法的字符串常量，未发现标签、路径或 API 地址变化。此项只作为迁移核对，不代替交互和业务测试。本轮不覆盖正式 EXE、不创建 Release；真实账号授权与完整游戏启动矩阵仍未重跑。

## 本地 Lite 原生交互验收

在独立测试配置下运行打包后的 framework-dependent 单文件 Lite，实际点击核验了版本内容管理、五个版本设置页面、名称修改、启动设置保存、滚动、加载器远程版本查询和 Modrinth 搜索。未使用真实账号或生产实例。测试版入口及样例仅位于忽略的 work 构建目录，正式入口不变。

发现并修复两处问题：

- 展开的下拉框按 Escape 时，窗口的预览键盘事件抢先关闭编辑页。现在先关闭当前页中的展开选择器，再把下一次 Escape 作为返回操作。
- 嵌套版本设置保存名称后，内容管理仍引用旧的不可变列表行。实例列表刷新事件会重新绑定管理页的列表，并保留选中实例；关闭管理页时取消订阅。首页启动标签也重新读取当前名称，避免沿用旧 VersionRow 的显示文本。

修订后的打包 EXE 已再次实际操作验证这两条路径。新增回归断言覆盖选择器的 Escape 路由，以及打开的管理页同步名称、保留选择。界面回归检查中的键盘路由测试使用已加载的独立测试窗口，放在其他布局断言之后，避免可见性变化影响虚拟化测量。

真实微软/MUA 授权、完整游戏启动矩阵、多显示器和不同 DPI 尚未在本轮重新验证。测试 EXE 使用独立配置，不自动加载正式账号或游戏目录；默认运行不带界面样例。

## refactor 延续拆分回归

按参考分支提取内容管理、启动设置、导出、更新选择/预览与公共组件后，全部 21 项无需真实游戏环境的回归检查通过。WorkflowUiChecks 新增全局启动设置保存、主界面关联字段同步，以及整合包导出保留五项选择、默认不包含存档的断言。已有目录切换、名称同步、Esc、菜单扩展、下载队列和更新确认断言继续执行。

格式、Release 构建、目录布局和 UI 边界检查通过。11 个完整组件文件经哈希核对与固定参考提交一致；检查脚本可对推送后的 Git 修订再次核对。CI 已配置在 test 推送时自动运行，远程结果需以对应提交的 Actions 记录为准。本轮不改变 Core、正式版本号或 Release；原生点击、真实授权与完整游戏启动矩阵未在这轮重复运行。

## 资源收藏解耦回归

资源收藏界面与搜索布局移除完整工作区依赖后，重新运行全部 21 项检查通过。新增的收藏查询检查使用临时收藏目录和 HTTP 替身，覆盖类型过滤、下载量排序、15 项分页、兼容发布类型、四路请求上限和取消传播；CompactUiChecks 继续验证真实 Modrinth 搜索、独立资源下载及仅收藏筛选。检查未使用正式账号或游戏存档；本轮未重新执行原生点击和完整游戏启动矩阵，也未打包或替换正式 EXE。

## CI 干净检出检查

WorkflowUiChecks 的截图保存方法自行创建输出目录，不依赖其他检查或本地历史产物预先创建 work/verify。修复后，在全新的本地 Git 克隆中确认该目录不存在，再按 CI 顺序执行 AuthChecks、MuaChecks、DownloadChecks、QueueChecks、CompatibilityChecks、VersionUiChecks、WorkflowUiChecks、CollectionChecks，全部通过，截图正常生成。该修复仅涉及测试，不改变启动器业务实现。

## refactor 合并与版本号整合（0.21.0）

`refactor`（`db0ab6a`）以第二父提交并入 `test`（合并提交 `145bf37`），结构保持 test 的窗口 shell、九个 XAML 页面与组合根；逐项迁移与退役理由见 [整合记录](archive/branch_refactor/REFACTOR_INTEGRATION.md)。

本地实跑结果：

- `dotnet build src/MCUGLauncher/MCUGLauncher.csproj -c Release`：0 警告、0 错误；程序集版本与文件版本为 `0.21.0`，`InformationalVersion` 形如 `0.21.0+<提交>`。
- `tools/SourceFormatter`：0 个文件需要格式化。
- `scripts/check-layout.py`、`check-ui-boundaries.py`、`check-refactor-reuse.py`、`check-version.py`：全部通过；复用基线仍为 `refactor` `db0ab6a` 的 11 个整文件。
- 检查程序通过：`InlineUiChecks`（含程序集名称与版本元数据一致性断言）、`VersionUiChecks`、`WorkflowUiChecks`、`CollectionChecks`、`AuthChecks`、`MuaChecks`、`DownloadChecks`、`QueueChecks`、`CompatibilityChecks`、`MiniDownloadChecks`、`StabilityChecks`、`UninstallChecks`、`UpgradeChecks`、`ExperienceChecks`、`ContentSafetyChecks`、`DependencyChecks`、`LibraryChecks`、`ResourceDownloadChecks`、`CompactUiChecks`、`DownloadUiChecks`（后两项使用实时 Modrinth 搜索与下载队列）。
- `DiagnosticChecks` 在 `Program.cs` 第 111 行创建符号链接时被本机权限拒绝（缺少符号链接特权，开发者模式未开启），该断言之前的输出检查均已通过；这是本地会话限制，与本次版本号改动无关，CI 仍会执行该检查。
- 未执行：`IntegrationChecks`、真实微软/MUA 授权、完整游戏启动矩阵，以及正式 EXE 打包。这些仍以远程 CI 与人工验收为准。

版本号改动后，导航区与关于区不再写死版本，诊断信息与两处 User-Agent 读取 `ProductInfo.Version`；`scripts/check-version.py` 会阻止版本字面量重新进入源码、界面、CHANGELOG 与 README 的不一致状态。

## 0.22.1：多运行会话与本地优先启动

`RuntimeChecks` 使用两个独立 WPF 子进程模拟游戏窗口，直接复用运行注册表和运行会话界面组件：验证同时运行、每会话日志、日志脱敏、目录重复启动保护、通过列表只关闭指定窗口、剩余会话状态更新及启动失败不残留登记。测试进程使用与 LaunchService 相同的无控制台启动选项，避免控制台窗口成为关闭目标。测试只清理自身创建的进程和临时目录。

同一套检查验证在线列表组件构造不请求网络、请求合并、旧目录响应抑制、缓存、失败重试，以及忽略取消的传输仍受超时限制。WorkflowUiChecks 在窗口 Loaded 前注入记录请求的传输，确认本地启动恢复为零网络请求；耗时输出仅代表隔离样例，不作为真实用户目录的性能保证。

本轮不使用真实账号或生产存档。进程与组件检查不等同于真实 Minecraft 多开、显卡兼容和联机验收；这些结果需在实际游戏环境补验。

验证结果：本轮全部 22 项无需真实游戏环境的检查通过（含新增 RuntimeChecks）；Release 构建、格式、版本、类文档、目录与 UI 边界检查通过。完整 Minecraft 多开/真实授权矩阵仍以上述验收边界为准。

### 运行会话的展示端口回归

控制器与 WPF 视图通过 IRunningGamesView/RunningGameRow 分离，日志读取和诊断捕获通过 RunningGameActions 注入。SessionControllerChecks 不启用 UseWPF，其 runtimeconfig 只依赖 Microsoft.NETCore.App，直接验证控制器的快照、日志命令、会话退出和事件退订。RuntimeChecks 保留真实视图与双进程检查；测试窗口必须先报告 Loaded，再发送关闭命令，避免把窗口创建句柄误当作界面准备完成。

### 0.22.1 进度与运行入口修订

WorkflowUiChecks 直接触发首页 StopGameButton 并断言 inlinePages 中出现运行列表，防止只创建页面未显示的回归；覆盖紧凑确认的确认、取消、Escape 与背后页面保留。RuntimeChecks 使用同一目录、不同离线 ID 的两个真实测试窗口，验证身份去重、取消不关闭、确认只关闭指定进程与日志隔离。它不代表真实 Minecraft 的世界锁或联机行为测试。

MiniDownloadChecks 检查右上角锚定及跨页面可见；InlineUiChecks 继续覆盖暂停、继续、取消及进度位置，并断言任务栏不重复显示进度条。启动及普通操作不显示底部进度表面。

本批全部 23 项非真实游戏检查通过；移除进度表面后，“取消当前操作”复用原按钮移至任务页，并额外复核启动/游戏启动不显示进度条、实际入口与确认遮罩。

### 设置导航迁移回归

WorkflowUiChecks 逐项进入 11 个内置分类，重复进入关于页、验证 Java 应用后的返回、下载快捷入口和离开页面清理；动态注册 Settings 与 More 扩展，确认页面可见且注册不会劫持当前主页；旧日志/任务路由转入设置对应栏目。SessionControllerChecks 在无 WPF 项目中验证栏目选择、未知 ID、扩展刷新保留状态与事件退订。ExperienceChecks 和 InlineUiChecks 的工具入口、下载暂停/继续/取消断言改为检查真实设置宿主。

设置页面独立化：WorkflowUiChecks 新增外观应用、服务器收藏、备份策略的持久化、旧 Java 入口复用及主标签重排后的路由；check-ui-boundaries.py 约束 Views/Settings 与弹窗/工作区解耦，以及设置组装不再通过索引和控件树提取页面。

0.22.2：ResourceInstallChecks 校验实例安装快照、版本/加载器拒绝、改名与 OptiFine 元数据、下载历史安全清理；InlineUiChecks 检查旧 Mod 入口跳转到唯一管理页与伸展列表。

版本选择器检查实际 PART_EditableTextBox 和输入同步；资源卡片检查已处理点击事件及鼠标按下/松开路径。截图验证详情目标名称、模组列表高度。原生预览工具未暴露目标窗口，本轮未完成原生鼠标验收。
