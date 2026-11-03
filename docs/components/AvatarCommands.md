# AvatarCommands（B）

职责：读取皮肤来源、管理进程内编码缓存、合并自动读取，返回带身份的头像快照。不校验账号授权，不创建 UI 控件。

依赖成员：
- LauncherContext.Config.AccountType/Player/SkinFile：快照身份与离线文件路径。
- LauncherContext.AccountRevision、OnlineSession.UUID、AccountSessions.Identity(string).Uuid：区分会话与丢弃迟到结果。
- LauncherContext.Http/Token：传入 Core SkinAssets.SkinUri/Download，执行有界读取与取消。
- LauncherContext.Microsoft.ReadSavedAccount()：读取正版皮肤地址。
- LauncherContext.Mua.Read()/Metadata(CancellationToken)/Skin(credential,metadata,CancellationToken)：读取 MUA 当前角色和皮肤。
- LauncherContext.Store.DataDirectory/Save(AppConfig)：保存离线皮肤与设置。
- LauncherContext.Send(object)：返回带 account/player 的结果。
- ProtocolValues.ReadString(JsonElement,string)：解析用户选择的本地路径。
- 构造参数 Func<byte[]?,string>：注入平台图像编码；本组件不引用平台图形类型。

自动读取按模式、玩家名、UUID、离线路径缓存，成功结果 5 分钟，默认/失败结果 15 秒，最多 24 项；相同身份的并发自动读取共用任务。手动刷新不使用自动缓存。后台网络返回后仍检查 AccountRevision，换账号或换本地皮肤后的旧结果不能覆盖当前头像。

调用由应用所属线程发起，延续该线程的配置/状态所有权；后台下载等待不阻塞线程。首次或手动皮肤解码仍使用注入的图形适配器，不把 Avalonia 渲染擅自搬到线程池。
