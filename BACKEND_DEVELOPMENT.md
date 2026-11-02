# 后端开发规范（组件分级与依赖声明）

## 1. 目的

本规范约束 `src/MCUGLauncher` 后端（UseCases、Services、Authentication、Models）的新增与修改，目标是：

1. 让新组件尽可能只依赖 `MCUGLauncher.Core`，为后续插件化铺路；
2. 让不得不依赖现有组件的组件，把依赖面**显式、完整、可审查**地写清楚；
3. 修改现有组件时优先归纳逻辑，而不是不断堆叠新组件；
4. 当共用逻辑的成本足够大时，把逻辑沉淀到 Core，而不是复制到更多组件。

前端 Ui、MainWindow 与协议入口不属于“组件”，不在本规范的分级范围内，但仍受《AGENTS.md》约束。

## 2. 术语

| 术语 | 定义 |
| --- | --- |
| **Core** | `MCUGLauncher.Core` 项目提供的公共类型与方法；BCL 与 NuGet 依赖不视为“现有 CS 组件” |
| **组件** | 一个承担单一职责的类或一组强内聚类，例如 `SaveCommands`、`LauncherTaskQueue` |
| **完全独立组件（A）** | 除 Core/BCL/NuGet 外，不依赖仓库内任何已有 CS 类 |
| **不完全独立组件（B）** | 除 Core 外，还依赖仓库内已有组件（如 `LauncherContext`、`LauncherTaskQueue`） |
| **集成层（C）** | `LauncherApplication`、`MainWindow` 等只做装配/转发的入口，不承载业务 |
| **依赖面** | 组件实际使用的类型成员集合（方法、属性、事件），不是“引用到的整个类型” |

## 3. 组件分级与硬性规则

### 3.1 A 类：完全独立组件

定义：只依赖 Core、BCL、NuGet；不引用 `LauncherContext`、`Services/*`、`Authentication/*`、`UseCases/*`、`Models/*`、WPF/WebView2。

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
- 不得依赖 `MainWindow`、`Ui/*`、WebView2、WPF 控件；
- 不得反向依赖集成层（`LauncherApplication`）或其他 B 组件的**实现细节**；只允许依赖其公开契约/方法；
- 新增依赖必须在同一提交更新依赖声明与登记表；
- B 组件如果依赖超过 5 个成员，应评估拆分或上移 Core。

### 3.3 C 类：集成层

`LauncherApplication`、`MainWindow`、`LauncherContext` 的构造与注册属于集成层：

- 只做装配、协议分发、窗口适配；
- 不写业务算法；
- 不把 C 层作为 B 组件的依赖（B 不得 `using` 集成层）。

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
/// 不依赖：Ui、MainWindow、WebView2、LauncherApplication、其他 *Commands。
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
4. **保持契约稳定**：已有公开方法签名变更需检查所有调用方与 `test/*Checks`，并在提交说明列出影响面。
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
5. **协议与 UI 解耦**：组件只处理 `JsonElement`/领域对象，返回结果通过 `Send` 回调；不直接操作 DOM/窗口。
6. **补测试与边界**：
   - A 类：纯单元测试；
   - B 类：`WebBridgeChecks` 场景 + 受影响检查；
   - 新协议：更新 `ProtocolValues` 字段表与 `check-web-boundaries.py`。
7. **更新文档**：`ARCHITECTURE.md`（模块职责）与本文登记表。

### 7.1 插件化目标形态

```text
A 组件（只依赖 Core）
   ▲
   │ 通过接口/委托组合
B 组件（显式声明依赖）
   ▲
   │ 由集成层装配
LauncherApplication / LauncherContext
```

- 新能力优先做成 A 组件 + 接口，再由 B 组件或集成层组合；
- 不允许“每个功能一个 Service + 一个 Manager + 一个 Helper”；
- 不允许 Core 反向依赖 App。

## 8. 当前组件类别划分

统计范围：`src/MCUGLauncher` 的后端组件（UseCases、Services、Authentication、Models），不含 `MainWindow` 与 Core。共 **45 个**：A 类 13 个、B 类 28 个、C 类 4 个。

### 8.1 C 类（集成层，4 个）

| 组件 | 位置 | 说明 |
| --- | --- | --- |
| `LauncherApplication` | `UseCases/LauncherApplication.cs` | 协议入口、命令注册、启动恢复与状态回推；只装配，不写业务 |
| `IDesktopHost` | `UseCases/IDesktopHost.cs` | 原生窗口/对话框契约，由 `MainWindow` 实现 |
| `MicrosoftLoginWebUi` | `Authentication/MicrosoftLoginWebUi.cs` | `IWebUI` 适配器，桥接 WPF 登录窗口 |
| `MicrosoftLoginWindow` | `Authentication/MicrosoftLoginWindow.cs` | WPF + WebView2 授权窗口，属于 UI 边界 |

> `MainWindow` 是宿主壳，不计入后端组件；它只依赖 `LauncherApplication` 与 `IDesktopHost`。

### 8.2 B 类（28 个）

#### 8.2.1 UseCases（19 个）

| 组件 | 说明 | 依赖面 |
| --- | --- | --- |
| `AccountCommands` | 微软/MUA 登录、MUA 角色选择 | `LauncherContext.AccountRevision/Microsoft/Mua/Send/Token`；`AccountModeCommands.Apply` |
| `AccountModeCommands` | 账号模式恢复、切换、忘记 | `LauncherContext.AccountRevision/AccountSessions/Config/Microsoft/Mua/OnlineSession/Send/SendAccountState/Store/Token` |
| `AvatarCommands` | 头像读取与离线皮肤 | `LauncherContext.AccountRevision/Config/Http/Microsoft/Mua/Send/Store/Token` |
| `BookmarkCommands` | 资源收藏切换 | `LauncherContext.RefreshState/Store` |
| `CommunityCommands` | 公告/推荐/服务器收藏/发布 | `LauncherContext.Community/Config/Http/RefreshState/Send/Store/Token` |
| `ContentUpdateCommands` | 内容更新检查、预览、应用 | `LauncherContext.Config/Http/Send/Tasks/Token` |
| `DiagnosticCommands` | 日志读取与系统信息 | `LauncherContext.Config/Send/Store` |
| `GameCommands` | 启动/停止游戏、进程观察 | `LauncherContext.Config/Games/Http/Log/MarkPreparing/Mua/OnlineSession/ReleasePreparing/Send/SendGames/Store/Tasks/Token` |
| `InstallationCommands` | 加载器列表/安装、整合包预览/导入 | `LauncherContext.Config/Http/Send/Tasks/Token` |
| `InstanceCommands` | 目录打开、模组启停/删除 | `LauncherContext.Config/RefreshState` |
| `InstanceMaintenanceCommands` | 实例克隆与导出 | `LauncherContext.Config/Tasks` |
| `LaunchOptionsCommands` | 启动选项读写 | `LauncherContext.Config/RefreshState/Send/Store` |
| `LauncherContext` | 共享运行时（配置、HTTP、队列、会话、状态回推） | `ConfigStore`、`NetworkPolicyHandler`、`CommunityService`、`DownloadExecutor`、`LauncherTaskQueue`、`RunningGameRegistry`、`AccountSessionCache`、`MicrosoftAccountService`、`MuaAccountService` |
| `MuaProfileSelection` | MUA 多角色选择的短期状态 | `LauncherContext.Send/Token` |
| `ResourceChoiceCommands` | 资源版本列表与排队 | `LauncherContext.Config/Http/Send/Tasks/Token` |
| `ResourceCommands` | 原版/Modrinth 搜索与安装 | `LauncherContext.Config/Http/Send/Store/Tasks/Token` |
| `SaveCommands` | 存档备份与恢复 | `LauncherContext.Config/Games/Send/Store/Token` |
| `SettingsCommands` | 设置、Java 扫描、数据源保存/启用 | `LauncherContext.Config/RefreshState/Send/Store/Token` |
| `UninstallCommands` | 卸载预览与应用 | `LauncherContext.Config/Games/Send/Tasks/Token` |

#### 8.2.2 Services（6 个）

| 组件 | 说明 | 依赖面 |
| --- | --- | --- |
| `AvatarImage` | 把皮肤字节编码为 PNG Data URI | `AvatarRenderer`（WPF 渲染）、`PngBitmapEncoder` |
| `ConfigStore` | `config.json` 读取、迁移、保存 | `AppConfig`（Models） |
| `DownloadExecutor` | 按任务类型执行下载/安装/维护 | `InstanceOperationExecutor` 与 Core 的 `OperationRecord`/`OperationContext` |
| `InstanceSnapshot` | 捕获实例根目录、名称、要求与启动设置 | `AppConfig`、Core 实例/包类型 |
| `LauncherStateBuilder` | 组装前端 `state` 快照 | `AppConfig`、`VersionCatalog`、收藏快照参数（由调用方传入） |
| `VersionCatalog` | 版本目录扫描、实例与模组读取 | `AppConfig`、`ModInfo`/`VersionDetails` 等 Models |

#### 8.2.3 Authentication（3 个）

| 组件 | 说明 | 依赖面 |
| --- | --- | --- |
| `AvatarRenderer` | 皮肤/默认头像渲染 | WPF `BitmapSource`/`PngBitmapEncoder` |
| `MicrosoftAccountService` | 微软登录、会话刷新、令牌存储 | `ProtectedAccountStorage`、`MinecraftOwnershipGuard`、`AuthenticationCheckpoint`、`IWebUI`、`HttpClient` |
| `MuaAccountService` | MUA 登录、角色、元数据 | `ProtectedAccountStorage`、`HttpClient` |

### 8.3 A 类（13 个）

| 组件 | 位置 | 说明 |
| --- | --- | --- |
| `ProtocolValues` | `UseCases/ProtocolValues.cs` | 协议字段类型、实例路径、离线 UUID 校验；只依赖 BCL |
| `AccountSessionCache` | `Services/AccountSessionCache.cs` | 进程内已验证会话缓存；依赖 CmlLib 会话类型与可注入时钟 |
| `CommunityService` | `Services/CommunityService.cs` | 社区 URL 连通性测试与读取；只依赖 `HttpClient` |
| `InstallationProgress` | `Services/InstallationProgress.cs` | 安装阶段的进度计算；只依赖 Core `OperationContext` |
| `InstanceOperationExecutor` | `Services/InstanceOperationExecutor.cs` | 实例级维护执行器；只依赖 Core 队列记录与回调 |
| `InstanceRequirements` | `Services/InstanceRequirements.cs` | 读取实例安装元数据；只依赖 Core 的类型与 JSON |
| `LauncherTaskQueue` | `Services/LauncherTaskQueue.cs` | 持久任务队列；只依赖 Core `OperationCoordinator` 与注入的执行委托 |
| `PreviewCache<T>` | `Services/PreviewCache.cs` | 通用、带过期的预览缓存 |
| `ServerPingService` | `Services/ServerPingService.cs` | Minecraft SLP 查询；只依赖 BCL 网络/JSON |
| `AuthenticationCheckpoint` | `Authentication/AuthenticationCheckpoint.cs` | 认证阶段日志钩子；只依赖 XboxAuthNet 接口 |
| `MinecraftOwnershipGuard` | `Authentication/MinecraftOwnershipGuard.cs` | 正版资格校验；依赖 CmlLib 认证上下文 |
| `ProtectedAccountStorage` | `Authentication/ProtectedAccountStorage.cs` | DPAPI 账号存储；依赖 `IJsonStorage` 与 BCL 加密 |
| `AppConfig` | `Models/AppConfig.cs` | 配置数据模型（叶子类型），只依赖 BCL/集合 |

### 8.4 `LauncherContext` 依赖成员签名

B 组件声明依赖时，按以下签名书写：

```text
AppConfig Config { get; }
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
AccountSessionCache.Find(string mode) : MSession?
AccountSessionCache.Fresh(string mode) : MSession?
AccountSessionCache.Forget(string mode) : void

CommunityService.TestAsync(string url, CancellationToken token) : Task<(bool Ok, string Text, int Ms)>
CommunityService.ReadAsync(string url, CancellationToken token) : Task<string?>

MicrosoftAccountService.Authenticate(bool interactive, CancellationToken token) : Task<MSession>
MicrosoftAccountService.ReadSavedAccount() : SavedMicrosoftAccount?
MicrosoftAccountService.Forget() : void

MuaAccountService.Read() : MuaCredential?
MuaAccountService.Login(string account, string password,
    Func<IReadOnlyList<MuaProfile>, Task<MuaProfile?>> select, CancellationToken token) : Task<MuaCredential>
MuaAccountService.Metadata(CancellationToken token) : Task<MuaMetadata>
MuaAccountService.Session(MuaCredential credential) : MSession

AccountModeCommands.Apply(LauncherContext context, string mode, MSession? session, bool verified = true) : void
```

## 9. 审查清单（提交前）

- [ ] 组件级别（A/B/C）已在 `<remarks>` 或提交说明中写明；
- [ ] B 组件依赖清单完整到“类.成员（含参数）”，并与代码一致；
- [ ] 没有依赖 `Ui/*`、`MainWindow`、WebView2、`LauncherApplication`；
- [ ] 修改现有组件时没有新增组件/新增依赖（有则说明理由）；
- [ ] 共用逻辑先就地归纳；下沉 Core 满足第 6 节全部条件；
- [ ] 没有新增 `HttpClient`、下载器、任务队列、Java 扫描器等重复能力；
- [ ] 新协议字段已更新 `ProtocolValues`、边界检查与 `WebBridgeChecks`；
- [ ] 文档（ARCHITECTURE、登记表）与代码同提交更新。

## 10. 迁移与例外

- 现有 B 组件在**下一次被修改时**补齐依赖声明与登记表，不要求一次性重写；
- 例外（必须新增 B 组件或新增依赖）需在提交说明中写明：为什么 A 类不可行、为什么不能复用现有组件、退出条件；
- 例外不得成为惯例；连续两个功能以“例外”方式新增依赖，应在评审中要求先重构。

## 11. 示例

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
/// 不依赖：Ui、MainWindow、WebView2、其他 *Commands。
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


## 0.24.0-codex 原生迁移依赖变动

后端项目现位于 `src/MCUGLauncher.App`，旧路径只保留为历史说明。

- ResourceCommands（B）：继续依赖 LauncherContext.Config.Root/LastInstance/Java、Http、Token、Store.DataDirectory、Tasks.Enqueue(string,string,string,DownloadRequest)、Send(object)，VersionCatalog.Load/ReadDetails/InstanceDirectory；新增图片读取复用 SkinAssets.Download(HttpClient,Uri,CancellationToken)。readImage 为原生图片控件提供受限 HTTPS、有界字节结果，不引用图片解码器或控件。它属于已有资源用例，不新增下载器。
- DiagnosticCommands（B）：LauncherContext.Store.DataDirectory、Config.Root、Send(object)。移除 WebView2 版本探测，系统信息不依赖界面框架。
- CommunityCommands（B）：既有 LauncherContext.Community.ReadAsync/TestAsync、Config.DataSources/Servers/Player/CommunityKey/AdminKey/WriteEndpoint、Store.DataDirectory/Save(AppConfig)、Http、Token、Send(object)、RefreshState()，以及 ServerPingService.PingAsync。仅将既有 Servers 数据源纳入同一读取流程，未增加组件依赖或新网络客户端。
- ShellWindow、ShellDesktopHost、LauncherFacade（C）：装配、原生窗口操作和协议适配；Views 为前端视图，不列入后端 B 组件。

IDesktopHost 确认契约变更：ConfirmStopGameAsync() / ConfirmUninstallAsync(string) 返回 Task<bool>；LauncherApplication 在关闭、卸载及恢复排队卸载前 await 结果。测试替身与 ShellDesktopHost 同步更新，取消不执行操作。

空目录/源边界修正：VersionCatalog（B）仍只构造 Models.VersionGroup/InstanceInfo/ModInfo、读取 InstanceInfo.Name，复用 Core InstanceCatalog.MetadataDirectory/DirectoryFor、AtomicFile.ReadJson、ContentService.SafeFile；未增加依赖，空目录返回空列表。CommunityService 只依赖注入的 HttpClient 与 BCL，先验证 URL 再请求，空/无效源返回 null。


### 0.24.0-codex 原版清单修订

ResourceCommands（B）保持既有依赖：LauncherContext.Http / Token 读取 Mojang manifest，LauncherContext.Send(object) 回推结果。ManifestEntries(JsonElement) 只是该用例内的纯协议映射，无新增组件或外部依赖；移除 release-only 过滤及 24 项上限，保留 type。SendVanillaVersionsAsync(string requestId) 在成功与失败回推 requestId，LauncherApplication（C）只透传现有字段。分类、搜索、分页由 VanillaPage 展示层完成；安装仍使用原队列与 Core。WebBridgeChecks 用含 30 个正式版、快照和历史版本的隔离清单验证完整性。


### 本轮设置与语言修订

GameCommands、SettingsCommands、LaunchOptionsCommands 仍为 B 类，完整成员依赖见 docs/components 下同名文档和类 remarks。GameCommands 复用 Core GameDefaults，不引入视图依赖；SettingsCommands 扩展既有偏好保存，不新建服务；LaunchOptionsCommands 统一全局/实例的 AutoChinese。LauncherStateBuilder 只将 AppConfig.LegacyPreferences 加入状态快照，UI 不获得存储器或认证对象。
