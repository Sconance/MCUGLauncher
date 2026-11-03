# CommunityCommands（B）

职责保留既有社区读取、服务器收藏、密钥和本地发布流程；本轮仅收敛文件写入与服务器协议依赖。

| 成员依赖 | 用途 |
| --- | --- |
| LauncherContext.Community.ReadAsync(string,CancellationToken)/TestAsync(string,CancellationToken) | 读取和测试数据源 |
| LauncherContext.Config.DataSources.Announcements/Servers/Recommendations/Quotes | 当前读取地址 |
| LauncherContext.Config.Servers.RemoveAll/Add | 本机服务器收藏 |
| LauncherContext.Config.Player/CommunityKey/AdminKey/WriteEndpoint | 发布作者、本机验证和同步地址 |
| LauncherContext.Store.DataDirectory/Save(AppConfig) | 本机文件位置与配置持久化 |
| LauncherContext.Http.PostAsync/Token | 既有发布同步与取消 |
| LauncherContext.Send(object)/RefreshState() | 协议结果和状态 |
| ServerPingService.PingAsync(string,CancellationToken)、PingResult.Online/OnlinePlayers/MaxPlayers/Ping/Motd/Version/Error | 展示层状态结果适配，协议由 Core 执行 |
| SavedServer.Name/Address | 收藏值对象 |
| ProtocolValues.ReadString(JsonElement,string) | 协议字段读取 |

Core AtomicFile.ReadJson<List<JsonElement>>(string)/WriteJson<T>(string,T) 负责本地数组原子存储；没有新增服务、HTTP 客户端或窗口依赖。依赖较多仍是历史组件的改进点，本轮不把密钥验证或发布语义混入 Core，也不靠拆转发器掩盖问题。
