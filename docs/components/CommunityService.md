# CommunityService

B 类组件，负责读取社区数据源并将公告 Markdown 转为公告 DTO。

| 依赖成员 | 用途 |
| --- | --- |
| `AnnouncementSyncService(HttpClient http, string? cacheDirectory)` | 使用调用方的 HTTP 客户端和缓存目录。连通性测试传入空缓存目录。 |
| `AnnouncementSyncService.IsSyncSource(Uri uri)` | 判断地址是否应按索引或清单处理。 |
| `AnnouncementSyncService.SyncAsync<T>(Uri source, Func<string, string, T> parse, CancellationToken token)` | 同步公告并在提交快照前调用解析函数。 |
| `AnnouncementSyncService.FileDate(string path)` | 提取公告文件名中的发布日期。 |
| `AnnouncementSyncService.IsSyncing`、`AnnouncementSyncService.LastStatus` | 提供后端同步状态。 |
| `AnnouncementSyncStatus.Outcome`、`AnnouncementSyncStatus.Error`、`AnnouncementSyncOutcome.Updated` | 区分数据源测试成功和同步错误。 |
| `AnnouncementInfo(string Id, string Title, string Body, string Tag, string Author, DateTimeOffset? PublishedAt, string ImageUrl)`、`AnnouncementInfo.Markdown`、`AnnouncementInfo.PublishedAt` | 创建公告数据并附上正文和发布日期。 |
| `HttpClient.SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken)`、`HttpClient.GetAsync(Uri requestUri, HttpCompletionOption completionOption, CancellationToken cancellationToken)` | 读取旧格式公告和其他社区数据源。 |
