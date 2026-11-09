# CommunityCommands

B 类：社区数据、图片、服务器收藏及本地推荐管理。没有界面或浏览器依赖，不自行构造 HTTP 客户端。

依赖成员：

- `LauncherContext.Config` 的 `DataSources.Announcements/Recommendations/Quotes`、`Servers`、`Root`、`AdminKey`、`CommunityKey`。
- `SavedServer.Name/Address/Instance/GameRoot`；`GameDirectoryCatalog.RequireRoot(AppConfig,string)` 校验明确的收藏实例目录。
- `LauncherContext.Community.ReadAnnouncementsAsync(string,CancellationToken)`、`ReadAsync(string,CancellationToken)` 读取社区内容。
- `LauncherContext.Http.GetAsync(string,HttpCompletionOption,CancellationToken)` 和 `Token` 下载有大小限制的图片。
- `LauncherContext.Store.DataDirectory`、`Save(AppConfig)` 保存收藏、密钥与本地推荐。
- `LauncherContext.RefreshState`、`Send(object)` 发布状态和社区消息。
- `ServerPingService.PingAsync(string,CancellationToken)` 查询服务器。
- `ProtocolValues.ReadString(JsonElement,string)` 读取本地推荐字段，Contracts 结果 DTO 构造函数生成类型化返回。

`SaveServer(name,address,oldAddress,instance,gameRoot)`：地址仍是收藏的去重键；实例与所属目录共同绑定。
省略实例保留旧绑定；绑定名称不变且未指定目录时保留旧目录；明确传入 gameRoot 时必须是已管理目录；清空实例时清空目录。
保存绑定不负责下载或启动游戏，也不强制离线磁盘在线。无编号字段。
