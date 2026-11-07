# GameCommands

B 类游戏启动用例。普通启动和收藏启动共用一条准备、授权、进程注册及观察流程；不新增下载器或启动器。

## 接口和依赖成员

- `LaunchAsync(string version)`：普通启动；空版本沿用 LastVersion，保留已有启动设置。
- `LaunchToServerAsync(string instance, string serverAddress)`：要求明确实例和地址；Core ServerEndpoint.Parse 校验域名/IP、端口和 IPv6。
- `LauncherContext.Config.Root/LastVersion/LastInstance/Player/AccountType`：读取当前根目录、账号，成功后记录最近实例；根目录变化时不写入错误目录的最近实例。
- `InstanceSnapshot.Capture(AppConfig, string)`：冻结根目录、实例键、数据目录、需求以及深复制的启动设置。
- `LauncherContext.Tasks.RequireLaunchAvailable(string, string, string)`：检查下载/维护与启动冲突。
- `LauncherContext.Games.RequireAvailable(string, string, bool)`、`UsesDirectory(string)`：检查已运行会话与实例占用。
- `LauncherContext.MarkPreparing(string, string)` / `ReleasePreparing(string)`：原子预留目录，在 finally 释放；供其他用例检查占用。
- `AccountModeCommands.EnsureFreshAsync(string)`：在线会话校验；`LauncherContext.Mua.Metadata(CancellationToken)` 和 Core AuthlibLaunch.Prepare 注入 MUA 认证。
- `LauncherContext.Http/Token`：复用网络策略和生命周期，不在视图中新建网络客户端。
- `LauncherContext.Store.DataDirectory/Save(AppConfig)`：日志、认证依赖及最近实例持久化。
- `LauncherContext.Games.Start(...)` / `Active` / `Release(Guid)`：登记、查询和释放游戏会话；`GameSession.RequestStop()` 沿用原生确认后的关闭请求。
- `LauncherContext.Log(string)` / `SendGames()`：记录和发布会话状态。

Core 调用：LaunchService.Build(LaunchRequest, CancellationToken, HttpClient)、GameDefaults.EnsureChinese、
MemoryPolicy.Recommend、JavaDiscovery.Architecture、ServerEndpoint.Parse。离线会话沿用 ProtocolValues.OfflineUuid。
既有 buildProcess 委托仅作为进程构建适配点，验证中用短命令进程，不把它当成真实游戏验收。

## 参数隔离和失败

服务器地址只写入 InstanceSnapshot 的设置副本。不会写回实例描述文件、全局 LaunchSettings 或收藏列表。
任何准备、账号或进程错误都返回 LaunchResultInfo(false, message)，不把创建了 Process 对象当作启动成功。
只有 Games.Start 成功才返回 Ok=true；这也不表示远端服务器已完成登录。

入口位于 Contracts.ILauncherService 和 UseCases.GameFeatureHandlers，不新增 JSON 字符串命令。
测试在 test/LauncherChecks/ServerLaunchScenarios.cs，执行命令见 test/README.md。
