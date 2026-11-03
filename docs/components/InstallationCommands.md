# InstallationCommands（B）

- LauncherContext.Config.Root/Java：加载器和整合包默认根目录、Java。
- LauncherContext.Http/Token：传入 Core LoaderCatalog.Get(HttpClient,string,string,CancellationToken)，统一加载器目录请求和取消。
- LauncherContext.Tasks.Enqueue(string,string,string,DownloadRequest)：提交加载器或整合包；支持注入同签名回调。
- LauncherContext.Send(object)：loaderList、loaderQueued、packPreview、packQueued。
- PreviewCache<ImportChoice>.Add/Get：绑定根目录、源文件和 SHA-256，确认后消费。
- ProtocolValues.ReadString/InstancePath：协议读取与实例名约束。
- Core LoaderCatalog.Get、PackService.ReadOverview、ContentService.SafeFile：复用兼容列表、包解析和名称校验。

loaderList 新增 requestId 以排除过期结果；loaderQueued 仅在成功提交后回推，失败保留弹窗。没有视图/窗口依赖。

Fabric/Quilt 先读取官方 versions/game，只有提供方声明支持才查询 versions/loader/{game}。不在 App 重复解析元数据，不硬编码最低游戏版本。未知游戏正常返回空列表；HTTP 400/503、网络超时为中文错误，用户取消继续传播。

验证：CompatibilityChecks 使用假 HTTP 检查 1.12.1 不发送 Fabric 加载器请求、正常版本/快照、去重、400/503 和取消；WebBridgeChecks 通过。另实时查询 Fabric 官方目录，确认 1.12.1 不在支持列表、1.20.1 可读取加载器列表；未执行实际加载器安装。
