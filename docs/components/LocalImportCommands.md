# LocalImportCommands（B）

职责：将拖入文件识别为本地导入预览，冻结目标与来源指纹，确认后提交现有队列。不是下载器，也不负责 UI。

- LauncherContext.Config.Root/LastInstance：读取用户当时选中目标。
- LauncherContext.Token：取消识别和 SHA-256 计算。
- LauncherContext.Games.UsesDirectory(string)：拒绝修改运行中实例。
- LauncherContext.Tasks.RequireLaunchAvailable(string,string,string)：拒绝冲突维护。
- LauncherContext.Tasks.Enqueue(string,string,string,DownloadRequest)：提交 local 请求；可注入同签名回调作隔离检查。
- LauncherContext.Send(object)：回推 localImportPreview/localImportQueued。
- InstanceSnapshot.Capture(AppConfig,string).Root/Key/Name/Directory：固定实例身份和实际路径。
- PreviewCache<Choice>.Add/Get：存储、确认并消费预览。
- ProtocolValues.ReadString(JsonElement,string)：协议字段读取。

Core LocalImportClassifier.Identify 识别文件，ContentService.ImportFiles 做事务复制，WorldArchives.Import 负责存档。整合包仍走原 packPreview/packQueued，不新增整合包安装算法。数据包必须选择具体存档，本轮不把它错误写入实例根目录。

DownloadExecutor 在执行前重新校验来源哈希，防止预览后文件变化；任务记录保存冻结目录，不再读取当前选择。运行/准备启动时拒绝写入；任务生命周期与页面无关。
