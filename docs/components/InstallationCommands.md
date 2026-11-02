# InstallationCommands（B）

- LauncherContext.Config.Root/Java：加载器和整合包默认根目录、Java。
- LauncherContext.Http.GetStringAsync(string,CancellationToken)/Token：Fabric/Quilt 元数据请求和取消。
- LauncherContext.Tasks.Enqueue(string,string,string,DownloadRequest)：提交加载器或整合包；支持注入同签名回调。
- LauncherContext.Send(object)：loaderList、loaderQueued、packPreview、packQueued。
- PreviewCache<ImportChoice>.Add/Get：绑定根目录、源文件和 SHA-256，确认后消费。
- ProtocolValues.ReadString/InstancePath：协议读取与实例名约束。
- Core LoaderCatalog.Get、PackService.ReadOverview、ContentService.SafeFile：复用兼容列表、包解析和名称校验。

loaderList 新增 requestId 以排除过期结果；loaderQueued 仅在成功提交后回推，失败保留弹窗。没有视图/窗口依赖。
