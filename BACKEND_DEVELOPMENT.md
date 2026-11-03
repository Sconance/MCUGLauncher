# 后端开发规范（组件分级与依赖声明）

## 1. 目的

本规范约束 `src/MCUGLauncher.App` 后端（Contracts、UseCases、Services、Authentication、Models）的新增与修改，目标是：

1. 让新组件尽可能只依赖 `MCUGLauncher.Core`，为后续插件化铺路；
2. 让不得不依赖现有组件的组件，把依赖面**显式、完整、可审查**地写清楚；
3. 修改现有组件时优先归纳逻辑，而不是不断堆叠新组件；
4. 当共用逻辑的成本足够大时，把逻辑沉淀到 Core，而不是复制到更多组件；
5. 保持 `Contracts.ILauncherService` 是 UI 唯一入口，业务状态、事件和请求使用共享强类型契约。

原生页面 `Views/*View`、`ShellWindow` 属于 UI 层，不在本规范的分级范围内，但仍受
[UI 开发指南](docs/UI_DEVELOPMENT.md) 与《AGENTS.md》约束。

后端任务不得顺手修改 Avalonia 页面或样式。若契约/接口变更会导致 UI 必须调整，按第 9 节说明并请求确认，不能静默改 UI。

## 2. 术语

| 术语 | 定义 |
| --- | --- |
| **Core** | `MCUGLauncher.Core` 项目提供的公共类型与方法；BCL 与 NuGet 依赖不视为“现有 CS 组件” |
| **组件** | 一个承担单一职责的类或一组强内聚类，例如 `SaveCommands`、`LauncherTaskQueue` |
| **完全独立组件（A）** | 除 Core/BCL/NuGet 外，不依赖仓库内任何已有 CS 类 |
| **不完全独立组件（B）** | 除 Core 外，还依赖仓库内已有组件（如 `LauncherContext`、`LauncherTaskQueue`） |
| **契约层** | `src/MCUGLauncher.App/Contracts`：`ILauncherService`、`LauncherState`、事件 DTO、请求补丁、`LaunchLimits`、`WindowPlacement`；UI 与业务共享，不能放业务实现 |
| **集成层（C）** | `LauncherService`、`LauncherApplication`、`IDesktopHost` 等只做装配、协议转发、线程调度的入口，不承载业务算法 |
| **依赖面** | 组件实际使用的类型成员集合（方法、属性、事件），不是“引用到的整个类型” |

## 3. 组件分级与硬性规则

### 3.1 A 类：完全独立组件

定义：只依赖 Core、BCL、NuGet；不引用 `LauncherContext`、`Services/*`、`Authentication/*`、`UseCases/*`、`Models/*`、Avalonia/WebView2。

规则：

- **无额外约束**，可直接单元测试，可被任意 UseCase 复用；
- 不强制依赖声明，但类 `<summary>` 必须说明职责与输入/输出；
- 不允许持有全局可变状态；需要状态时由调用方传入；
- 不允许内部 `new HttpClient`、`new ConfigStore`、`new LauncherTaskQueue`；需要这些能力时说明为什么不能留在调用方。

示例：`ProtocolValues`、`PreviewCache<T>`、Core 中的纯算法/校验类。

### 3.2 B 类：不完全独立组件

定义：依赖 Core 之外、仓库内已存在的组件。

规则：

- **必须在组件文档中声明依赖的全部类与成员**（方法签名、属性、事件），见第 4 节；
- 依赖面只写实际使用的成员，不允许用“依赖 LauncherContext”这种笼统表述；
- 不得依赖 `ShellWindow`、`Views/*`、WebView2、Avalonia 控件；
- 不得反向依赖集成层（`LauncherService`、`LauncherApplication`）或其他 B 组件的**实现细节**；只允许依赖其公开契约/方法；
- 可以使用 `Contracts` 中的状态/事件 DTO，但不得依赖 `ILauncherService` 的 UI 调用路径；
- 新增依赖必须在同一提交更新依赖声明与登记表；
- B 组件如果依赖超过 5 个成员，应评估拆分或上移 Core。

### 3.3 C 类：集成层

当前 C 类组件：

- `LauncherService`：实现 `Contracts.ILauncherService`，负责 UI 线程调度、事件解码和状态缓存；
- `LauncherApplication`：命令协议入口与命令分发；
- `IDesktopHost`：原生窗口/文件对话框契约，由 Avalonia 的 `ShellDesktopHost` 实现。

规则：

- 只做装配、协议分发、线程调度、窗口/桌面适配；
- 不写账号、下载、安装、启动、实例维护等业务算法；
- 不把 C 层作为 B 组件的依赖；B 组件不得 `using`/调用 `LauncherService` 或 `LauncherApplication`；
- `LauncherService` 的 JSON/命令协议只能作为内部实现，不得暴露给 View；
- `LauncherApplication` 内部字符串命令是过渡实现；新增一次性请求/响应优先使用第 3.4 节的声明式强类型 handler，不再增加字符串分发分支；
- `IDesktopHost` 的文件选择方法返回路径，由 App 侧产生类型化 `PathPicked`，不允许 UI 直接发送原始消息。

`LauncherContext` 是共享运行时上下文（B 类），不是新的业务入口；Commands 依赖它时应按第 4 节声明成员。

### 3.4 契约层：`App/Contracts`

`Contracts` 是 UI 与业务共享的强类型边界，不是 UI 项目，也不是业务实现目录。

规则：

- `ILauncherService` 是 View 唯一业务入口；不得恢复 `LauncherFacade`、`MessageReceived`、`AppConfig` 暴露或命令字符串入口；
- `LauncherState`、事件 DTO、`AppSettingsPatch`、`LaunchOptionsSettings`、`LaunchLimits`、`WindowPlacement` 属于共享契约；
- 契约只表达**语义数据**，不生成 UI 文案、颜色、图标或控件状态；账号标签、占位状态等由 UI 映射；
- `LauncherStateBuilder` 直接返回 `LauncherState`，不得重新引入匿名对象 + JSON 的第二份 schema；
- 数值范围统一使用 `LaunchLimits`，后端仍必须校验/钳制，不能依赖 UI 预校验；
- 占位实例使用 `InstanceInfo.IsPlaceholder`，不得用中文展示字符串做业务过滤；
- 契约中不得出现 Avalonia、WebView2、`Window`、`Control`、`AppConfig` 等 UI/可变配置类型；
- 修改 `ILauncherService`、状态 DTO 或事件 DTO 属于 API 变更，必须同时评估所有 View 消费者、`LauncherChecks` 和边界检查。

### 强类型功能返回

新增一次性查询/操作结果时，优先使用 `Task<TResponse>`，不要再增加请求 ID、结果事件、`LauncherService` 消息解析分支和 `LauncherApplication` 命令分支。

1. 在 `ILauncherService` 声明带 `[FeatureOperation]` 的默认接口方法。方法体将现有参数组成匿名对象，并调用 `FeatureRequest.InvokeAsync<TResponse>(this, nameof(MethodName), arguments)`。
2. 在 `Contracts` 增加共享的结果 DTO，并让接口方法直接返回 `Task<该 DTO>`。
3. 实现一个 UseCase handler，标记 `[FeatureHandler(nameof(ILauncherService.MethodName))]`，实现 `IFeatureHandler<TResponse>`，并通过 `LauncherContext` 构造函数接收依赖。
4. handler 会由 `FeatureDispatcher` 自动发现。启动时校验每个接口声明恰有一个 handler，且 handler 的 `TResponse` 与接口 `Task<TResponse>` 一致；缺失或重复注册会明确失败，不会静默丢弃结果。
5. View 直接 `await` 接口方法取得结果。`LauncherChecks` 应检查接口声明与 handler 的一一对应及响应类型，并至少验证一个真实调用返回声明的 DTO。

因此，增加一项功能时，集成层通常不需要逐处登记；除实际 handler 外，只需修改接口契约文件和结果 DTO 文件。需要长期订阅的状态流仍使用事件与状态快照；长时间后台任务继续使用现有任务队列/协调器，不应伪装成一次性结果。

## 4. 依赖声明规范（B 类）

### 4.1 声明位置

1. **类 XML 注释 `<remarks>`**（强制）：写清组件级别、依赖清单、明确不依赖的范围；
2. **本文第 6 节登记表**（强制）：新增/修改 B 组件时同步；
3. 依赖超过 3 个类型或包含跨模块契约时，增加 `docs/components/<组件名>.md`，内容与 `<remarks>` 一致。

### 4.2 声明模板

```csharp
/// <summary>实例存档的备份与恢复。</summary>
/// <remarks>
/// 组件级别：B（依赖现有组件）。
/// 依赖：
/// - <see cref="LauncherContext.Config"/>：读取根目录与当前实例；
/// - <see cref="LauncherContext.Store"/>.<see cref="ConfigStore.Save"/>：保存实例选择；
/// - <see cref="LauncherContext.Games"/>.<see cref="RunningGameRegistry.UsesDirectory"/>：禁止恢复运行中的实例；
/// - <see cref="LauncherContext.Send"/>：回推 saveList/saveRestored 消息。
/// 不依赖：Views、ShellWindow、WebView2、LauncherApplication、其他 *Commands。
/// </remarks>
```

要求：

- 每个依赖写成 `类型.成员`，方法带参数类型：`LauncherTaskQueue.Enqueue(string name, string root, string target, DownloadRequest request)`；
- 事件/回调写成 `LauncherContext.Send(Action<object>)`；
- 标明用途（读配置/写文件/提交任务/回推消息）；
- 依赖变更必须与代码同提交更新。

### 4.3 禁止的声明方式

```text
// 反例：没有方法级信息
依赖 LauncherContext、Services。
```

```text
// 反例：声明了但实际没用，或用了却没声明
依赖 LauncherTaskQueue.Enqueue，但代码还调用了 History()/Change()。
```

## 5. 修改现有组件的规则

1. **不新增组件**：修 bug、加小功能时，优先在现有类内完成；不得为单个调用方新建 `XxxManager`/`XxxHelper`/`XxxService`。
2. **不新增依赖**：不得为了省事把新服务塞进构造函数；确实需要时，必须在依赖声明与登记表中补充，并说明为什么不能由调用方传入。
3. **归纳共有逻辑**：
   - 同一组件内重复 ≥2 次：抽为该类的私有/内部方法；
   - 跨 ≥2 个组件重复：先评估是否属于 Core 能力，满足第 6 节标准则下沉 Core；
   - 不允许复制粘贴到第三个组件。
4. **保持契约稳定**：已有公开方法签名变更需检查所有调用方、`ILauncherService` 契约、View 消费方与 `test/*Checks`，并在提交说明列出影响面；UI 修改需按第 9 节另行确认。
5. **不为修改而重构**：修改现有组件时，重构范围应限于本次目标；大范围重命名/移动另开提交。

## 6. 共有逻辑下沉 Core 的标准

满足以下 **全部** 条件时，把逻辑加入 Core（而不是新建 Service 或在组件间共享）：

1. 被 **2 个及以上组件** 使用，或明显是通用领域能力（下载、校验、安装、Java、实例、路径、存档）；
2. 不依赖 `AppConfig`、UI、窗口、WebView2、任务队列的 UI 回调；
3. 行为确定、可单测，异常语义清晰；
4. 有稳定输入/输出契约，不把“当前选中实例”等会话状态作为隐式输入；
5. 有对应测试（至少 Core 级检查或新组件测试）。

下沉后的位置：

- 纯算法/校验：`Core/Application` 或 `Core/Infrastructure`；
- 文件/网络/事务：`Core/Infrastructure`；
- 领域模型：`Core/Domain`。

反例：把“读取 `%LOCALAPPDATA%` 配置”放进 Core（Core 不应知道应用配置路径）；这类应留在 `ConfigStore`（B 类）并声明。

## 7. 新组件开发流程（面向插件化）

1. **先判断能否 A 类**：只依赖 Core/BCL 实现；若需要配置、任务、账号，改由调用方传入参数或接口。
2. **需要跨组件协作时定义接口**，而不是直接引用具体类：
   - 参考 `IResourceProvider`：消费者面向接口，实现由集成层注入；
   - 新接口放在使用方同目录的 `Contracts/` 或 Core 的公共契约区。
3. **构造函数注入**：依赖从参数进入，禁止组件内部 `new` 服务、`new HttpClient`、静态单例。
4. **只注册，不互相查找**：由 `LauncherApplication`/`LauncherContext` 装配；组件内部不得反向解析容器。
5. **协议与 UI 解耦**：组件只处理领域对象/内部命令；面向 UI 的结果必须通过 `Contracts` DTO 和 `LauncherService` 事件返回，不直接操作 View/窗口。
6. **补测试与边界**：
   - A 类：纯单元测试；
   - B 类：`LauncherChecks` 场景 + 受影响检查；
   - 新增/修改 UI 契约：更新 `ILauncherService`、`LauncherStateBuilder`、View 消费方、`LauncherChecks` 与 `check-native-boundaries.py`；
   - 内部命令：更新 `ProtocolValues` 字段表。
7. **更新文档**：`ARCHITECTURE.md`（模块职责）、本文登记表与必要的 UI 开发说明。

### 7.1 插件化目标形态

```text
A 组件（只依赖 Core）
   ▲
   │ 通过接口/委托组合
B 组件（显式声明依赖）
   ▲
   │ 由集成层装配
LauncherApplication / LauncherContext
   ▲
   │ 实现 Contracts.ILauncherService
LauncherService（C）
   ▲
   │ 仅类型化调用
Views（UI，不属于后端）
```

- 新能力优先做成 A 组件 + 接口，再由 B 组件或集成层组合；
- 不允许“每个功能一个 Service + 一个 Manager + 一个 Helper”；
- 不允许 Core 反向依赖 App；
- 不允许 View 反向依赖 B/C 组件，只能通过 `ILauncherService`。

## 8. 当前组件类别划分

统计范围：`src/MCUGLauncher.App` 的后端组件（Contracts 除外，UseCases、Services、Authentication、Models），不含 `ShellWindow` 与 Core。共 **46 个**：A 类 14 个、B 类 28 个、C 类 4 个。

### 8.1 C 类（集成层，4 个）

| 组件 | 位置 | 说明 |
| --- | --- | --- |
| `LauncherService` | `UseCases/LauncherService.cs` | 实现 `ILauncherService`，统一 UI 入口、线程调度、状态缓存与事件解码；只装配，不写业务算法 |
| `LauncherApplication` | `UseCases/LauncherApplication.cs` | 内部命令协议入口、命令分发、启动恢复与状态回推；只装配，不写业务 |
| `FeatureDispatcher` | `UseCases/FeatureDispatcher.cs` | 自动发现并校验 `Task<TResponse>` handler，然后分发声明式功能调用 |
| `IDesktopHost` | `UseCases/IDesktopHost.cs` | 原生窗口/文件对话框契约，由 Avalonia 的 `ShellDesktopHost` 实现；选择方法返回路径 |

> `ShellWindow` 是宿主壳，不计入后端组件；它只依赖 `ILauncherService` 与 `IDesktopHost`。
> `MicrosoftLoginWindow` / `MicrosoftLoginWebUi` 是 UI 侧 OAuth 适配，不计入后端组件。

### 8.2 B 类（27 个）

#### 8.2.1 UseCases（20 个）

| 组件 | 说明 | 依赖面 |
| --- | --- | --- |
| `AccountCommands` | 微软/MUA 登录、MUA 角色选择 | `LauncherContext.AccountRevision/Microsoft/Mua/Send/Token`；`AccountModeCommands.Apply` |
| `AccountModeCommands` | 账号模式恢复、切换、忘记 | `LauncherContext.AccountRevision/AccountSessions/Config/Log/Microsoft/Mua/OnlineSession/Send/SendAccountState/Store/Token` |
| `AvatarCommands` | 头像读取与离线皮肤 | `LauncherContext.AccountRevision/AvatarIdentity/Config/Http/Log/Microsoft/Mua/Store/Token`；`AvatarDiskCache`；`SkinAssets`；`ProtocolValues.ReadString` |
| `BookmarkCommands` | 资源收藏切换 | `LauncherContext.RefreshState/Store` |
| `CommunityCommands` | 公告图片下载、推荐/服务器收藏 | `LauncherContext.Community/Config/Http/RefreshState/Send/Store/Token` |
| `ContentUpdateCommands` | 内容更新检查、预览、应用 | `LauncherContext.Config/Http/Send/Tasks/Token` |
| `DiagnosticCommands` | 日志读取与系统信息 | `LauncherContext.Config/Send/Store` |
| `GameCommands` | 启动/停止游戏、进程观察 | `LauncherContext.Config/Games/Http/Log/MarkPreparing/Mua/OnlineSession/ReleasePreparing/Send/SendGames/Store/Tasks/Token` |
| `InstallationCommands` | 加载器列表/安装、整合包预览/导入 | `LauncherContext.Config/Http/Send/Tasks/Token` |
| `InstanceCommands` | 目录打开、模组启停/删除 | `LauncherContext.Config/RefreshState` |
| `InstanceMaintenanceCommands` | 实例克隆与导出 | `LauncherContext.Config/Tasks` |
| `LaunchOptionsCommands` | 启动选项读写 | `LauncherContext.Config/RefreshState/Send/Store` |
| `LauncherContext` | 共享运行时（配置、HTTP、队列、会话、状态回推） | `ConfigStore`、`NetworkPolicyHandler`、`CommunityService`、`DownloadExecutor`、`LauncherTaskQueue`、`RunningGameRegistry`、`AccountSessionCache`、`MicrosoftAccountService`、`MuaAccountService`、`AvatarCommands` |
| `LocalImportCommands` | 本地文件分类预览、冻结导入目标并排入任务队列 | `LauncherContext.Config/Tasks/Games/IsRootPreparing/IsInstanceInUse/Token`；`InstanceSnapshot`；`LauncherTaskQueue`；`RunningGameRegistry`；`PreviewCache<Choice>`；Core `LocalImportClassifier`、`ContentService`、`WorldArchives` |
| `MuaProfileSelection` | MUA 多角色选择的短期状态 | `LauncherContext.Send/Token` |
| `ResourceChoiceCommands` | 资源版本列表与排队 | `LauncherContext.Config/Http/Send/Tasks/Token` |
| `ResourceCommands` | 原版/Modrinth 搜索与安装 | `LauncherContext.Config/Http/Send/Store/Tasks/Token` |
| `SaveCommands` | 存档备份与恢复 | `LauncherContext.Config/Games/Send/Store/Token` |
| `SettingsCommands` | 设置、Java 扫描、数据源保存/启用 | `LauncherContext.Config/RefreshState/Send/Store/Token` |
| `DownloadImageFeatureHandler` | 公告图片下载的强类型结果 handler | `LauncherContext.Http/Token` → `CommunityCommands.DownloadImageAsync` |
| `TestSourceFeatureHandler` | 数据源连通性与 Markdown 公告校验的强类型结果 handler | `LauncherContext.Community` → `CommunityService.TestAsync` / `TestAnnouncementAsync` |
| `UninstallCommands` | 卸载预览与应用 | `LauncherContext.Config/Games/Send/Tasks/Token` |

#### 8.2.2 Services（5 个）

| 组件 | 说明 | 依赖面 |
| --- | --- | --- |
| `ConfigStore` | `config.json` 读取、迁移、保存 | `AppConfig`（Models） |
| `DownloadExecutor` | 按任务类型执行下载/安装/维护 | `InstanceOperationExecutor` 与 Core 的 `OperationRecord`/`OperationContext` |
| `InstanceSnapshot` | 捕获实例根目录、名称、要求与启动设置 | `AppConfig`、Core 实例/包类型 |
| `LauncherStateBuilder` | 直接组装强类型 `LauncherState`，不生成匿名 JSON schema | `AppConfig`、`VersionCatalog`、`FavoriteInfo`（由调用方传入）、`Contracts` DTO |
| `VersionCatalog` | 版本目录扫描、实例与模组读取 | `AppConfig`、`ModInfo`/`VersionDetails` 等 Models；占位实例带 `IsPlaceholder` |

#### 8.2.3 Authentication（2 个）

| 组件 | 说明 | 依赖面 |
| --- | --- | --- |
| `AvatarRenderer` | 皮肤/默认头像渲染 | WPF `BitmapSource`/`PngBitmapEncoder` |
| `MicrosoftAccountService` | 微软登录、会话刷新、令牌存储 | `ProtectedAccountStorage`、`MinecraftOwnershipGuard`、`AuthenticationCheckpoint`、`IWebUI`、`HttpClient` |
| `MuaAccountService` | MUA 登录、角色、元数据 | `ProtectedAccountStorage`、`HttpClient` |

### 8.3 A 类（15 个）

| 组件 | 位置 | 说明 |
| --- | --- | --- |
| `ProtocolValues` | `UseCases/ProtocolValues.cs` | 协议字段类型、实例路径、离线 UUID 校验；只依赖 BCL |
| `AccountSessionCache` | `Services/AccountSessionCache.cs` | 进程内已验证会话缓存；依赖 CmlLib 会话类型与可注入时钟 |
| `CommunityService` | `Services/CommunityService.cs` | 社区 URL 读取与单篇 Markdown 公告解析；依赖 `HttpClient` 和 `Contracts.AnnouncementInfo` |
| `InstallationProgress` | `Services/InstallationProgress.cs` | 安装阶段的进度计算；只依赖 Core `OperationContext` |
| `InstanceOperationExecutor` | `Services/InstanceOperationExecutor.cs` | 实例级维护执行器；只依赖 Core 队列记录与回调 |
| `InstanceRequirements` | `Services/InstanceRequirements.cs` | 读取实例安装元数据；只依赖 Core 的类型与 JSON |
| `LauncherTaskQueue` | `Services/LauncherTaskQueue.cs` | 持久任务队列；只依赖 Core `OperationCoordinator` 与注入的执行委托 |
| `PreviewCache<T>` | `Services/PreviewCache.cs` | 通用、带过期的预览缓存 |
| `ServerPingService` | `Services/ServerPingService.cs` | Minecraft SLP 查询；只依赖 BCL 网络/JSON |
| `SkinAssets` | `Services/SkinAssets.cs` | 皮肤地址校验与有界下载；只依赖 BCL 网络/加密 |
| `AuthenticationCheckpoint` | `Authentication/AuthenticationCheckpoint.cs` | 认证阶段日志钩子；只依赖 XboxAuthNet 接口 |
| `MinecraftOwnershipGuard` | `Authentication/MinecraftOwnershipGuard.cs` | 正版资格校验；依赖 CmlLib 认证上下文 |
| `ProtectedAccountStorage` | `Authentication/ProtectedAccountStorage.cs` | DPAPI 账号存储；依赖 `IJsonStorage` 与 BCL 加密 |
| `AppConfig` | `Models/AppConfig.cs` | 配置数据模型（叶子类型），只依赖 BCL/集合 |
| `AvatarDiskCache` | `Services/AvatarDiskCache.cs` | 头像磁盘缓存；只依赖 Core `AtomicFile` 与 BCL |

### 8.4 `LauncherContext` 依赖成员签名

B 组件声明依赖时，按以下签名书写：

```text
AppConfig Config { get; }
string AvatarIdentity { get; }
AvatarCommands Avatars { get; }
ConfigStore Store { get; }
HttpClient Http { get; }
CommunityService Community { get; }
MicrosoftAccountService? Microsoft { get; }
MuaAccountService? Mua { get; }
MSession? OnlineSession { get; set; }
AccountSessionCache AccountSessions { get; }
LauncherTaskQueue Tasks { get; }
RunningGameRegistry Games { get; }
Action<object> Send { get; }
Action RefreshState { get; set; }
CancellationToken Token { get; }
long AccountRevision
void Log(string message)
void MarkPreparing(string root, string directory)
void ReleasePreparing(string directory)
bool IsInstanceInUse(string directory)
bool IsRootPreparing(string root)
void SendGames()
void SendTasks()
void SendAccountState()
```

### 8.5 常用被依赖成员签名

新增依赖时，按以下签名写到 `<remarks>`；未列出的成员以源文件为准，不得只写类型名。

```text
ConfigStore.Load() : AppConfig
ConfigStore.Save(AppConfig config) : void
ConfigStore.SaveAsync(AppConfig config) : Task

LauncherTaskQueue.Enqueue(string name, string root, string target, DownloadRequest request) : void
LauncherTaskQueue.History() : List<OperationRecord>
LauncherTaskQueue.Change(string id, string action) : void
LauncherTaskQueue.Start() : void
LauncherTaskQueue.Completion : Task
LauncherTaskQueue.Dispose() : void

RunningGameRegistry.Active : IReadOnlyList<RunningGame>
RunningGameRegistry.UsesDirectory(string directory) : bool
RunningGameRegistry.UsesRoot(string root) : bool

AccountSessionCache.Remember(string mode, MSession session) : void
AccountSessionCache.Identity(string mode) : AccountIdentity?
AccountSessionCache.RememberIdentity(string mode, AccountIdentity identity) : void
AccountSessionCache.Find(string mode) : MSession?
AccountSessionCache.Fresh(string mode) : MSession?
AccountSessionCache.Forget(string mode) : void

CommunityService.TestAsync(string url, CancellationToken token) : Task<(bool Ok, string Text, int Ms)>
CommunityService.ReadAsync(string url, CancellationToken token) : Task<string?>
CommunityService.ReadAnnouncementAsync(string url, CancellationToken token) : Task<(bool Ok, string Markdown, string Error, int Ms)>
CommunityService.TestAnnouncementAsync(string url, CancellationToken token) : Task<(bool Ok, string Text, int Ms)>
CommunityService.ParseMarkdownAnnouncement(string markdown, string source) : AnnouncementInfo

MicrosoftAccountService.Authenticate(bool interactive, CancellationToken token) : Task<MSession>
MicrosoftAccountService.ReadSavedAccount() : SavedMicrosoftAccount?
MicrosoftAccountService.Forget() : void

MuaAccountService.Read() : MuaCredential?
MuaAccountService.Login(string account, string password,
    Func<IReadOnlyList<MuaProfile>, Task<MuaProfile?>> select, CancellationToken token) : Task<MuaCredential>
MuaAccountService.Metadata(CancellationToken token) : Task<MuaMetadata>
MuaAccountService.Session(MuaCredential credential) : MSession

AccountModeCommands.Apply(LauncherContext context, string mode, MSession? session, bool verified = true, bool persist = true, AccountIdentity? identity = null) : void
```

## 9. 后端与 UI 的协同边界

### 9.1 文件权限

后端任务默认只修改：

```text
src/MCUGLauncher.App/**
src/MCUGLauncher.Core/**
test/**
scripts/check-*.py
docs/**（仅后端/架构文档）
```

后端任务不得：

- 修改 `src/MCUGLauncher.Avalonia/` 的 View、样式、资源或窗口视觉；
- 以“顺手修一下”为由调整页面布局；
- 通过修改 UI 来掩盖后端契约变更；
- 把 `AppConfig`、JSON、`LauncherApplication` 直接暴露给 View。

如果后端契约变更导致 UI 编译失败或行为不匹配：

1. 先完成并验证后端/契约变更；
2. 在提交说明列出需要 UI 跟进的文件、API 和预期行为；
3. 由用户确认是否授权同步修改 UI，或另开 UI 任务；
4. 不要在未确认时静默修改 View。

说明模板：

```text
后端契约变更需要 UI 跟进

变更内容：
- ...

影响页面：
- ...

需要 UI 修改：
- ...

是否同步修改 UI：
- [ ] 授权本次一并修改
- [ ] 另开 UI 任务
- [ ] 暂不修改，先保留后端变更
```

### 9.2 契约变更必须成套更新

修改 `ILauncherService`、`LauncherState`、事件 DTO 或请求 DTO 时，至少检查：

- `App/Contracts` 的接口/DTO；
- `LauncherService` 的实现与事件映射；
- 相关 Commands/Services；
- `LauncherStateBuilder`；
- 所有 View 消费方（若获授权）；
- `test/LauncherChecks` 与协议/边界检查；
- `docs/ARCHITECTURE.md`、本指南的组件登记。

只改接口而不改实现/测试，或只改实现而留下过期契约，视为未完成。

### 9.3 展示职责

- 后端返回语义数据：账号类型、占位标志、启用状态、限制范围；
- UI 负责中文标签、颜色、图标、空态、布局和动画；
- 不在 `Contracts` 中新增 `accountLabel`、`color`、`icon` 这类展示字段；
- 用户提示目前仍有部分由后端生成（`Toast`/错误文案）；新增业务提示优先使用领域语义，由 UI 映射；至少不得包含布局/控件指令；
- 后端是校验和钳制的最终权威；UI 预校验只能使用 `LaunchLimits`，不能替代后端校验。

### 9.4 状态构建

- `LauncherStateBuilder` 必须直接构建 `LauncherState`；
- 不得恢复匿名对象 → JSON → UI 解析的第二份 schema；
- 新增状态字段时同步更新契约、builder、View（获授权）和 typed service 测试；
- 占位实例使用 `InstanceInfo.IsPlaceholder`，不得恢复显示字符串判断；
- 状态构建的目录扫描不要新增到 UI 线程路径；当前构建仍可能由命令触发，后续应继续下沉/异步化。

### 9.5 内部协议

`LauncherApplication` 当前仍使用字符串命令和 JSON 事件作为内部实现。规则：

- 不得把内部协议暴露成新的 UI API；
- 新增业务优先使用现有 Commands + `LauncherService` 类型化方法；
- 不要把新逻辑写进 `LauncherService` 的 JSON 解码分支；
- 逐步把内部命令改为类型化记录；在此之前，UI 仍只依赖 `ILauncherService`。

## 10. 审查清单（提交前）

- [ ] 组件级别（A/B/C）已在 `<remarks>` 或提交说明中写明；
- [ ] B 组件依赖清单完整到“类.成员（含参数）”，并与代码一致；
- [ ] B 组件没有依赖 `Views/*`、`ShellWindow`、WebView2、`LauncherService`、`LauncherApplication`；
- [ ] 修改现有组件时没有新增组件/新增依赖（有则说明理由）；
- [ ] 共用逻辑先就地归纳；下沉 Core 满足第 6 节全部条件；
- [ ] 没有新增 `HttpClient`、下载器、任务队列、Java 扫描器等重复能力；
- [ ] 没有在 UI 任务中修改 App/Core/Contracts；也没有在后端任务中静默修改 UI；
- [ ] 新增/修改 UI 契约已同步 `Contracts`、`LauncherService`、View 消费方（获授权）、`LauncherChecks` 与边界检查；
- [ ] `LauncherStateBuilder` 仍直接构建 `LauncherState`，没有恢复匿名 JSON schema；
- [ ] 数值范围使用 `LaunchLimits`，占位实例使用 `IsPlaceholder`；
- [ ] 契约没有新增 `accountLabel`/`color`/`icon` 等展示字段；
- [ ] 新协议字段已更新 `ProtocolValues`、边界检查与 `LauncherChecks`；
- [ ] 文档（ARCHITECTURE、登记表）与代码同提交更新。

## 11. 迁移与例外

- 现有 B 组件在**下一次被修改时**补齐依赖声明与登记表，不要求一次性重写；
- 例外（必须新增 B 组件或新增依赖）需在提交说明中写明：为什么 A 类不可行、为什么不能复用现有组件、退出条件；
- 例外不得成为惯例；连续两个功能以“例外”方式新增依赖，应在评审中要求先重构。

## 12. 示例

### A 类（合格）

`ProtocolValues` 只使用 BCL 与 Core 无关的 JSON/密码学类型，直接可测，无依赖声明负担。

### B 类（合格）

```csharp
/// <summary>存档备份与恢复。</summary>
/// <remarks>
/// 组件级别：B。
/// 依赖：
/// - LauncherContext.Config：读取 Root 与 LastInstance；
/// - LauncherContext.Store.Save(AppConfig)：保存选择；
/// - LauncherContext.Games.UsesDirectory(string)：拒绝运行中的实例；
/// - LauncherContext.Send：回推 saveList/saveRestored。
/// 不依赖：Views、ShellWindow、WebView2、其他 *Commands。
/// </remarks>
internal sealed class SaveCommands(LauncherContext context)
```

### 反例

| 反例 | 问题 |
| --- | --- |
| 为单个调用方新增 `SaveManager` + `SaveHelper` | 违反“修改不新增组件” |
| 新组件内部 `new LauncherTaskQueue` | 绕过统一任务队列与持久化 |
| B 组件 `using MCUGLauncher.Ui` | 依赖 UI，破坏分层 |
| 把 `%LOCALAPPDATA%` 路径逻辑放进 Core | Core 不应知道应用配置 |
| 在三个组件里复制“实例目录解析” | 应归纳后下沉 Core |

## 加载器目录复用

InstallationCommands.LoadersAsync 统一调用 Core LoaderCatalog.Get，避免在 App 重复解析 Fabric/Quilt 数据。先检查提供方的游戏支持列表，再查询对应加载器版本。

版本目录在无实例时返回空列表与空选中项，不构造可误选的占位实例；空实例名不读取 mods。CommunityService 仅接受 HTTP(S) 源，空或无效地址返回 null；切换数据源后重新发送社区状态。
