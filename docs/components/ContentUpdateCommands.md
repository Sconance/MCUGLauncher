# ContentUpdateCommands（B）

- LauncherContext.Config：传给 InstanceSnapshot.Capture；Http/Token 用于创建 Core ContentService 和取消。
- LauncherContext.Tasks.Enqueue(string,string,string,DownloadRequest)/Send(object)：提交并回推 contentUpdateQueued。
- InstanceSnapshot.Capture(AppConfig,string).Root/Key/Name/Directory/Requirements：冻结检查和更新目标。
- PreviewCache<Checked/Previewed>.Add/Get：保存检查及预览令牌。
- ProtocolValues.ReadString(JsonElement,string)：读取令牌。
- Core IResourceProvider、ContentUpdates.Check/Preview：领域检查、依赖解析与文件变更预览。

新增成功入队消息，不改变原确认、指纹和执行流程。没有 UI 依赖。
