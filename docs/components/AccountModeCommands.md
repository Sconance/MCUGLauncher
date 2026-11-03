# AccountModeCommands（B）

职责：模式切换、缓存会话恢复、启动前会话验证与明确移除。切换账号不会删除另一个模式的凭据。

依赖成员：
- LauncherContext.Config.AccountType/Player/OfflinePlayer、OnlineSession、AccountRevision：维护当前身份和选择顺序。
- AccountSessions.Identity(string)/RememberIdentity(string,AccountIdentity)/Find(string)/Fresh(string)/Remember(string,MSession)/Forget(string)：复用会话及启动前验证时效。
- Microsoft.ReadSavedAccount()/Authenticate(bool,CancellationToken)/Forget()：恢复和移除正版账号。
- Mua.Read()/Ensure(bool,CancellationToken)/Forget(CancellationToken)、MuaAccountService.Session(credential)：恢复和移除外置账号。
- LauncherContext.Token：取消恢复。
- Store.Save(AppConfig)/SaveAsync(AppConfig)：同步登录持久化或异步模式选择保存。
- SendAccountState()/Send(object)：更新账号摘要及错误/提示。

有会话缓存时立即应用内存身份并发布账号摘要；随后等待不可变配置快照异步落盘。本地身份加载通过 ReadSavedAccount / Mua.Read 读取并缓存，首次切换不做网络恢复；网络授权仅由 EnsureFreshAsync 在启动前按需执行。保存失败说明可能无法保留重启后的选择，不将磁盘错误误报为授权失效。

启动前 EnsureFreshAsync 的授权验证保持原规则。恢复任务仍检查 AccountRevision；旧恢复完成不能改回先前账号。
