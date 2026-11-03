# 无界面服务器启动接口

实现位于 `src/MCUGLauncher.Core/Application/ServerLaunch/`，命名空间为 `MCUGLauncher`，不依赖 Avalonia 或其他 UI 框架。

## 调用示例

```csharp
using MCUGLauncher;
using CmlLib.Core.Auth;

var request = new ServerLaunchRequest(
    Root: @"D:\Minecraft",
    InstanceId: "1.21.1",
    ServerAddress: "play.example.com:25565",
    Session: MSession.CreateOfflineSession("Player"),
    OutputFile: @"D:\LauncherLogs\server-game.log",
    Settings: new LaunchSettings { AutoMemory = false, RamMb = 4096 });

var game = await ServerLaunch.LaunchAsync(request, cancellationToken);
var exit = await game.Completion;
game.Process.Dispose();
```

示例使用离线账号，只适用于允许该身份登录的服务器。正版服务器须由调用方先完成认证，传入有效的微软 Minecraft 会话；MUA 会话还需传入已经准备好的 `ExternalAuthentication`。

## 参数与行为

- `Root` 是现有游戏根目录；`InstanceId` 是实例键，不是显示名，也不是一定等于实际启动的版本 ID。接口通过 `InstanceCatalog` 解析 `LaunchProfileId` 和实例数据目录；原版实例可以直接填写已安装版本 ID。
- `ServerAddress` 接收域名、IP 或 `host:port`，省略端口时使用 25565；IPv6 带端口时使用 `[::1]:25565`。地址通过现有 `LaunchService` 的 CmlLib 服务器选项传入，不执行服务器在线探测。
- `Settings` 非空时作为整套启动设置覆盖实例设置；否则使用实例保存的设置，没有时使用默认设置。默认开启自动内存，沿用现有 Windows 内存策略；如需全局配置回退，由调用方显式传入。服务器地址只覆盖本次设置副本，不写回实例配置。
- `OutputFile` 指定脱敏游戏日志；账号 AccessToken 自动加入脱敏列表，额外敏感值可通过 `Secrets` 提供。可选 `output` 回调会接收已脱敏输出，应保持快速且不抛异常。可选 `progress` 报告进程构建和启动阶段，`http` 复用调用方的网络策略。
- 返回 `GameSession` 表示进程已启动，不表示已成功连接服务器。用 `Completion` 等待进程退出，用 `RequestStop()` 请求正常关闭；退出后调用方负责释放 `Process`。取消令牌在准备、构建及启动前有效，进程启动后取消不会终止游戏。

`ServerLaunch.Prepare()` 可单独解析并检查请求，返回现有 `LaunchRequest` 而不启动进程。准备过程可能为已有的旧版游戏创建实例元数据，与 `InstanceCatalog.GetOrCreate()` 一致。

基础接口不安装或修复游戏、不执行登录刷新、不扫描 Mod 兼容性，也不接入 UI 的忙碌状态或任务队列；需要这些能力的调用方应在外层协调，UI 通过 `ILauncherService` 调用业务并沿用现有任务管理，不直接调用 Core。接口复用已有直连参数机制，不保证所有 Minecraft 版本兼容；服务器版本、认证、白名单及网络仍可能导致连接失败。现有 UI 启动路径尚未迁移至此接口。

合并新版后沿用统一命名规范，实例配置读取 `.mcuglauncher-instance.json`，不探测或自动导入历史名称的元数据，也不改写或删除旧数据。配置字段格式保持不变，详见 [命名与数据位置](NAMING.md)。

## 收藏组件的后端入口

### App 层类型化接口

UI 接入时通过 `MCUGLauncher.Contracts.ILauncherService` 调用，不直接操作 Core 或传入账号令牌：

```csharp
await Session.LaunchServerAsync(new ServerLaunchInfo(
    InstanceId: selectedInstanceId,
    ServerAddress: favorite.Address));
```

实例标识和地址都必须显式提供，空实例不会回退到首页选择。App 负责读取配置、刷新账号、检查安装/运行占用并登记游戏进程。内部复用 `ServerLaunch.Prepare()`，再沿用既有进程构建与会话管理，不重复调用会启动进程的 Core `LaunchAsync()`。本次地址不保存至配置，普通启动行为保持不变。失败沿用 `FeedbackReceived`；返回的 `Task` 不保证启动成功，更不代表已连接服务器。尚未创建或绑定 UI 按钮。

### Core 收藏适配器

`serverLaunchFromFavourateList.cs` 定义 `ServerLaunchFromFavourateList` 类。每个收藏组件可持有一个实例，构造时传入该组件绑定的 `ServerFavorite`、当前 `Preferences`、已认证会话和日志路径，按钮响应调用其 `LaunchAsync()`：

```csharp
var launcher = new ServerLaunchFromFavourateList(
    favorite, preferences, authenticatedSession, outputFile);
var game = await launcher.LaunchAsync(cancellationToken);
```

它从自身持有的收藏对象读取 `Profile` 和 `Address`，从配置读取游戏根目录，再将请求交给 `ServerLaunch.LaunchAsync()`。每次调用重新读取实例配置，启动设置优先级为实例设置、全局设置；服务器地址始终使用该收藏的地址，不修改原设置。`CreateRequest()` 可用于单独检查映射，不启动游戏。

可通过构造参数 `externalAuthentication` 传入 MUA 认证组件，通过 `Secrets` 传入额外脱敏值；`LaunchAsync()` 还支持进度、日志回调及 HTTP 客户端。该类不读取控件、不自动选择列表项、不自动登录，也不接管 UI 的任务和进程管理。调用方应传入被点击组件的收藏对象，而不是另外读取首页当前实例；收藏对象为不可变记录，编辑收藏产生新对象时应重新创建对应入口实例。当前尚未绑定任何 UI 按钮。

## 检查

```powershell
dotnet run --project test/ServerLaunchChecks/ServerLaunchChecks.csproj -c Release
```

检查使用临时目录、合成实例及离线会话，覆盖请求解析、实例映射、设置隔离、地址校验、安装状态和启动前取消，不下载或启动真实游戏。真实服务器直连需要在 Windows 上使用完整实例另行验收。

App 接口由 `test/LauncherChecks` 检查类型化调用和失败反馈，由 `test/WebBridgeChecks/ServerLaunchScenarios.cs` 检查参数传递、实例选择、配置不变、失败清理、IPv6、共享目录、在线授权保护及普通启动回归。进程构建注入测试在启动前终止，不替代真实 App 登录/启动验收；Core 真实直连已由用户独立验证。

Windows 上单独运行 App 接口检查：

```powershell
dotnet run --project test/LauncherChecks/LauncherChecks.csproj -c Release
dotnet run --project test/WebBridgeChecks/WebBridgeChecks.csproj -c Release -- --server-launch-only
```

本次在 Linux 交叉编译 App 与上述测试项目通过；使用仅 .NET Core 的临时运行配置执行隔离测试。全量 WebBridgeChecks 运行至既有 CustomizationScenarios 自动内存用例失败，该流程依赖 Windows kernel32 内存 API，不能视为全量回归通过。未修改该平台逻辑，也未在本次验证中进行真实 App 登录或游戏启动。
