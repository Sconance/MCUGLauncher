using BlockLaunch;
using CmlLib.Core.Auth;
using MCUGLauncher.Contracts;
using MCUGLauncher.Services;

namespace MCUGLauncher.UseCases;

// Mode selection preserves credentials. Only explicit removal deletes an account.
internal sealed class AccountModeCommands(
    LauncherContext context,
    Func<string, Task<MSession>>? restore = null,
    Func<string, Task<AccountIdentity?>>? readIdentity = null)
{
    Task? identityPreload;

    public async Task<AccountSwitchResultInfo> RestoreSelectedAsync()
    {
        var revision = context.AccountRevision;
        var mode = context.Config.AccountType;
        await PreloadIdentitiesAsync();
        var result = revision == context.AccountRevision
            ? await SwitchAsync(mode, false)
            : new AccountSwitchResultInfo(mode, false, "账号恢复已被更新的账号操作取代。");
        if (mode != "offline")
        {
            if (context.Config.AccountType == mode && context.OnlineSession != null)
                context.Send(new
                {
                    type = "authResult",
                    ok = true,
                    message = result.Message
                });
            else
                context.Send(new
                {
                    type = "accountSwitchResult",
                    account = result.Account,
                    needsLogin = result.NeedsLogin,
                    message = result.Message
                });
        }
        return result;
    }

    public Task PreloadIdentitiesAsync() => identityPreload ??= LoadIdentitiesAsync();

    async Task LoadIdentitiesAsync()
    {
        var revision = context.AccountRevision;
        foreach (var accountMode in new[] { "microsoft", "mua" })
        {
            try
            {
                var identity = await ReadIdentityAsync(accountMode);
                if (revision != context.AccountRevision)
                    return;
                if (identity is not null)
                    context.AccountSessions.RememberIdentity(accountMode, identity);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or AccountStorageException)
            {
                context.Log("账号身份预载失败 [" + error.GetType().Name + "]");
            }
        }
    }

    Task<AccountIdentity?> ReadIdentityAsync(string mode) => readIdentity is not null
        ? readIdentity(mode)
        : Task.Run<AccountIdentity?>(() =>
        {
            if (mode == "microsoft")
            {
                var saved = context.Microsoft?.ReadSavedAccount();
                return saved is null ? null : new AccountIdentity(saved.Username, saved.Uuid);
            }
            var mua = context.Mua?.Read();
            return mua is null ? null : new AccountIdentity(mua.Profile.Name, mua.Profile.Id);
        }, context.Token);

    public async Task<AccountSwitchResultInfo> SwitchAsync(string mode, bool prompt = true)
    {
        if (mode is not ("offline" or "microsoft" or "mua"))
            throw new ArgumentException("未知的账号模式。");
        var revision = context.NextRevision();
        if (mode == "offline")
        {
            await ApplySelectionAsync(mode, null);
            return new(mode, false, "已切换到离线账号。");
        }
        var cached = context.AccountSessions.Find(mode);
        if (cached != null)
        {
            await ApplySelectionAsync(mode, cached, false);
            return new(mode, false, "已切换到 " + cached.Username);
        }

        try
        {
            var identity = context.AccountSessions.Identity(mode) ?? await ReadIdentityAsync(mode);
            if (revision != context.AccountRevision)
                return new(mode, false, "账号切换已被更新的账号操作取代。");
            if (identity is null)
                throw new AccountLoginException("本机尚未保存此账号，请先登录。", true);
            context.AccountSessions.RememberIdentity(mode, identity);
            await ApplySelectionAsync(mode, null, false, identity);
            if (revision != context.AccountRevision)
                return new(mode, false, "账号切换已被更新的账号操作取代。");
            return new(mode, false, "已切换到 " + identity.Player);
        }
        catch (Exception error)
        {
            if (revision != context.AccountRevision)
                return new(mode, false, "账号切换已被更新的账号操作取代。");
            var needsLogin = error is AccountLoginException { RequiresInteractive: true };
            return new(mode, prompt && needsLogin,
                needsLogin ? "账号授权需要重新确认，请登录。" : "暂时无法恢复账号，请检查网络后重试。已保存的账号仍然保留。");
        }
    }

    public async Task<MSession> EnsureFreshAsync(string mode)
    {
        var revision = context.AccountRevision;
        var fresh = context.AccountSessions.Fresh(mode);
        if (fresh != null)
            return fresh;
        try
        {
            var session = restore != null ? await restore(mode) : await RestoreAsync(mode);
            if (revision == context.AccountRevision)
            {
                context.AccountSessions.Remember(mode, session);
                if (context.Config.AccountType == mode)
                    context.OnlineSession = session;
            }
            return session;
        }
        catch (AccountLoginException error) when (error.RequiresInteractive)
        {
            if (revision != context.AccountRevision)
                throw;
            context.AccountSessions.Forget(mode);
            if (context.Config.AccountType == mode)
                context.OnlineSession = null;
            throw;
        }
    }

    async Task<MSession> RestoreAsync(string mode)
    {
        if (mode == "microsoft")
        {
            var service = context.Microsoft ?? throw new AccountLoginException("登录组件未就绪。");
            if (service.ReadSavedAccount() == null)
                throw new AccountLoginException("请先登录正版账号。", true);
            return await service.Authenticate(false, context.Token);
        }
        var mua = context.Mua ?? throw new AccountLoginException("登录组件未就绪。");
        return MuaAccountService.Session(await mua.Ensure(false, context.Token));
    }

    async Task ApplySelectionAsync(
        string mode,
        MSession? session,
        bool verified = true,
        AccountIdentity? identity = null)
    {
        Apply(context, mode, session, verified, persist: false, identity: identity);
        try
        {
            await context.Store.SaveAsync(context.Config);
        }
        catch (IOException)
        {
            context.Send(new
            {
                type = "toast",
                message = "账号已切换，但设置未能保存，重启后可能恢复上次选择"
            });
        }
        catch (UnauthorizedAccessException)
        {
            context.Send(new
            {
                type = "toast",
                message = "账号已切换，但配置目录不可写，请检查目录权限"
            });
        }
    }

    public static void Apply(
        IAccountContext context,
        string mode,
        MSession? session,
        bool verified = true,
        bool persist = true,
        AccountIdentity? identity = null)
    {
        var player = mode == "offline" ? context.Config.OfflinePlayer : identity?.Player ?? session?.Username;
        if (string.IsNullOrWhiteSpace(player))
            throw new AccountLoginException("账号没有有效的玩家名，请重新登录。", true);
        if (context.Config.AccountType == "offline")
            context.Config.OfflinePlayer = context.Config.Player;
        if (session != null && verified)
            context.AccountSessions.Remember(mode, session);
        context.OnlineSession = session;
        context.Config.AccountType = mode;
        context.Config.Player = player;
        if (persist)
            context.Store.Save(context.Config);
        context.SendAccountState();
    }

    public async Task<AuthResultInfo> ForgetAsync(string mode)
    {
        var revision = context.NextRevision();
        context.AccountSessions.Forget(mode);
        if (mode == "microsoft")
            context.Microsoft?.Forget();
        else if (mode == "mua" && context.Mua != null)
            await context.Mua.Forget(context.Token);
        else
            throw new ArgumentException("未知的在线账号模式。");
        if (revision != context.AccountRevision)
            return new(false, false, "账号移除已被更新的账号操作取代。");
        if (context.Config.AccountType == mode)
            Apply(context, "offline", null);
        return new(true, false, "已移除本机保存的账号。");
    }
}
