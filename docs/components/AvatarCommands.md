# AvatarCommands（B）

职责：读取皮肤来源、协调磁盘头像缓存、合并自动读取，返回带身份的头像快照。不校验账号授权，不创建 UI 控件。

依赖成员：
- LauncherContext.Config.AccountType/Player/SkinFile：快照身份与离线文件路径。
- LauncherContext.AccountRevision、AvatarIdentity：区分会话与丢弃迟到结果。
- LauncherContext.Http/Token：传入 SkinAssets.SkinUri/Download，执行有界读取与取消。
- LauncherContext.Microsoft.ReadSavedAccount()：读取正版皮肤地址。
- LauncherContext.Mua.Read()/Metadata(CancellationToken)/Skin(credential,metadata,CancellationToken)：读取 MUA 当前角色和皮肤。
- LauncherContext.Store.DataDirectory/Save(AppConfig)：保存离线皮肤与设置。
- LauncherContext.Send(object)：返回带 account/player 的结果。
- ProtocolValues.ReadString(JsonElement,string)：解析用户选择的本地路径。
- 构造参数 Func<byte[]?,string>：注入平台图像编码；本组件不引用平台图形类型。

账号索引包含模式、服务提供方、UUID（缺失时使用玩家名）及离线路径。先发送磁盘旧图，自动读取在五分钟内复用，过期后后台刷新；手动刷新强制读取来源。相同身份的自动读取合并任务，选择版本和请求序号拒绝迟到结果。刷新失败保留已有缓存。移除后端重复的 24 项编码结果缓存；UI 的短期展示缓存仍保留。

新增依赖：AvatarDiskCache.ReadAsync(string,CancellationToken)、ReadSourceAsync(string,CancellationToken)、SaveAsync(string,string,string,long,CancellationToken,DateTimeOffset?)，分别读取账号、复用来源和保存成功头像。默认 Steve 仅在没有可用缓存时生成一次，失败默认图不写磁盘。

调用由应用所属线程发起，延续该线程的配置/状态所有权；后台下载等待不阻塞线程。首次或手动皮肤解码仍使用注入的图形适配器，不把 Avalonia 渲染擅自搬到线程池。

账号与来源索引均采用五分钟新鲜度。来源复用保留原检查时间，不因多个账号轮流使用同一 URL 无限续期；过期时重新读取。预期 IO/网络/图像格式错误只记录异常类型并回退，意外程序错误向集成层传播并记录类别。绝不记录 URL、凭据、图片正文。AvatarIdentity 同时出现在状态、账号事件、头像事件，UI 使用相同键隔离同名 UUID。
