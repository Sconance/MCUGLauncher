# GameCommands（B）

准备启动、记录会话、观察退出及关闭指定游戏，不构建页面。

| 实际依赖成员 | 用途 |
| --- | --- |
| LauncherContext.Config.Root/LastVersion/LastInstance/Player/AccountType | 当前选择与成功后的选择保存 |
| LauncherContext.Mua.Metadata(CancellationToken)/Http/Token | 授权与外置认证准备 |
| LauncherContext.Store.DataDirectory/Save(AppConfig) | 日志目录和配置保存 |
| LauncherContext.Tasks.RequireLaunchAvailable(string,string,string) | 排除安装中的目标 |
| LauncherContext.Games.RequireAvailable(string,string,bool)/UsesDirectory(string)/Start/Release(Guid)/Active | 管理独立进程，同目录运行时不重写语言 |
| LauncherContext.MarkPreparing(string,string)/ReleasePreparing(string) | 保留准备状态 |
| LauncherContext.Send(object)/SendGames()/Log(string) | 结果、会话及日志回推 |
| InstanceSnapshot.Capture(AppConfig,string).Root/Directory/Requirements/Settings | 冻结启动目标 |
| AccountModeCommands.EnsureFreshAsync(string) | 启动前刷新授权 |
| RunningGame.Id/Session.Completion/Session.RequestStop() | 观察与关闭会话 |

Core 复用 GameDefaults.EnsureChinese、MemoryPolicy、JavaDiscovery、LaunchService、AuthlibLaunch。依赖面较大源于既有启动流程；此次没有新增服务。可注入进程构建函数进行隔离测试。

`LaunchServerAsync(ServerLaunchInfo)` 读取契约的 InstanceId/ServerAddress，拒绝空实例或空地址，不回退当前选择。与普通启动共用准备互斥、InstanceSnapshot、Tasks/Games 检查、账号刷新、语言设置、内存策略、进程注册、退出观察及结果回推。通过 Core `ServerEndpoint.Parse(string)` 校验地址，`ServerLaunch.Prepare(ServerLaunchRequest,CancellationToken)` 生成直连参数，不调用会自行启动进程的 `ServerLaunch.LaunchAsync`，避免绕过 RunningGameRegistry。共享根目录时在检查占用前固定实际目录，准备后拒绝目录变化；保留 App 已计算的内存上限。地址仅覆盖本次启动副本，不保存到实例或全局设置。没有新增 B 类组件；契约不包含账号凭据，也不依赖 UI。

AutoChinese 默认开启，可全局或按实例关闭；仅在同目录没有运行中的游戏时写 options.txt。旧版使用 zh_CN，新版使用 zh_cn，保留其他设置行。

在线启动先调用 AccountModeCommands.EnsureFreshAsync(string)，再执行 MUA 注入准备和进程创建。本地已选身份可以没有 OnlineSession；它不能绕过验证，也不会被标记为已验证。
