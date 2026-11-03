# AccountCommands（B）

职责：交互登录与短期 MUA 角色选择，成功后提交验证会话。无窗口或 UI 依赖。

依赖成员：
- LauncherContext.Microsoft、Mua、Token、AccountRevision、Send(object)：认证实例、取消、迟到结果隔离和登录结果。
- MicrosoftAccountService.Authenticate(bool interactive, CancellationToken token)：返回 MSession；Translate(Exception error, bool refreshing, string? stage = null)：转换微软错误。
- MuaAccountService.Login(string account, string password, Func<IReadOnlyList<MuaProfile>,Task<MuaProfile?>> select, CancellationToken token)：返回 MuaCredential；Session(MuaCredential)：转换已验证会话。
- MuaProfileSelection.ChooseAsync(IReadOnlyList<MuaProfile>)、Select(JsonElement)：等待/接收角色选择。
- AccountModeCommands.Apply(LauncherContext,string,MSession?,bool verified = true,bool persist = true,AccountIdentity? identity = null)：保存当前身份及会话。

不得把账号选择当作授权成功，不在日志记录账号密码、授权令牌或完整服务器响应。真实微软/MUA 授权仍需人工验收。
