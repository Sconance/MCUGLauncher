using BlockLaunch;
using CmlLib.Core.Auth;
using MCUGLauncher.Contracts;
using MCUGLauncher.Services;

namespace MCUGLauncher.UseCases;

// Mode selection preserves credentials. Only explicit removal deletes an account.
internal sealed class AccountModeCommands(
    AccountServices services,
    IAccountStateWriter writer,
    Func<string, Task<MSession>>? restore = null,
    Func<string, Task<AccountIdentity?>>? readIdentity = null)
{
    readonly IAccountStateWriter accountWriter = writer;
    Task? identityPreload;

    public async Task<AccountSwitchResultInfo> RestoreSelectedAsync()
    {
        var revision = services.Revision.Current;
        var mode = services.Record.Mode;
        await PreloadIdentitiesAsync();
        var result = revision == services.Revision.Current
            ? await SwitchAsync(mode, false)
            : new AccountSwitchResultInfo(mode, false, "账号恢复已被更新的账号操作取代。");
        if (mode != "offline")
        {
            if (services.Record.Mode == mode && services.SessionStore.Current != null)
                services.Notifier.Send(new
                {
                    type = "authResult",
                    ok = true,
                    message = result.Message
                });
            else
                services.Notifier.Send(new
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
        var revision = services.Revision.Current;
        foreach (var accountMode in new[] { "microsoft", "mua" })
        {
            try
            {
                var identity = await ReadIdentityAsync(accountMode);
                if (revision != services.Revision.Current)
                    return;
                if (identity is not null)
                    services.Sessions.RememberIdentity(accountMode, identity);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or AccountStorageException)
            {
                services.Logger.Log("账号身份预载失败 [" + error.GetType().Name + "]");
            }
        }
    }

    Task<AccountIdentity?> ReadIdentityAsync(string mode) => readIdentity is not null
        ? readIdentity(mode)
        : Task.FromResult(mode == "microsoft"
            ? services.Auth.ReadMicrosoftIdentity()
            : services.Auth.ReadMuaIdentity());

    public async Task<AccountSwitchResultInfo> SwitchAsync(string mode, bool prompt = true)
    {
        if (mode is not ("offline" or "microsoft" or "mua"))
            throw new ArgumentException("未知的账号模式。");
        var revision = services.Revision.Next();
        if (mode == "offline")
        {
            await ApplySelectionAsync(mode, null);
            return new(mode, false, "已切换到离线账号。");
        }
        var cached = services.Sessions.Find(mode);
        if (cached != null)
        {
            await ApplySelectionAsync(mode, cached, false);
            return new(mode, false, "已切换到 " + cached.Username);
        }

        try
        {
            var identity = services.Sessions.Identity(mode) ?? await ReadIdentityAsync(mode);
            if (revision != services.Revision.Current)
                return new(mode, false, "账号切换已被更新的账号操作取代。");
            if (identity is null)
                throw new AccountLoginException("本机尚未保存此账号，请先登录。", true);
            services.Sessions.RememberIdentity(mode, identity);
            await ApplySelectionAsync(mode, null, false, identity);
            if (revision != services.Revision.Current)
                return new(mode, false, "账号切换已被更新的账号操作取代。");
            return new(mode, false, "已切换到 " + identity.Player);
        }
        catch (Exception error)
        {
            if (revision != services.Revision.Current)
                return new(mode, false, "账号切换已被更新的账号操作取代。");
            var needsLogin = error is AccountLoginException { RequiresInteractive: true };
            return new(mode, prompt && needsLogin,
                needsLogin ? "账号授权需要重新确认，请登录。" : "暂时无法恢复账号，请检查网络后重试。已保存的账号仍然保留。");
        }
    }

    public async Task<MSession> EnsureFreshAsync(string mode)
    {
        var revision = services.Revision.Current;
        var fresh = services.Sessions.Fresh(mode);
        if (fresh != null)
            return fresh;
        try
        {
            var session = restore != null ? await restore(mode) : await RestoreAsync(mode);
            if (revision == services.Revision.Current)
            {
                services.Sessions.Remember(mode, session);
                if (services.Record.Mode == mode)
                    services.SessionStore.Set(session);
            }
            return session;
        }
        catch (AccountLoginException error) when (error.RequiresInteractive)
        {
            if (revision != services.Revision.Current)
                throw;
            services.Sessions.Forget(mode);
            if (services.Record.Mode == mode)
                services.SessionStore.Set(null);
            throw;
        }
    }

    async Task<MSession> RestoreAsync(string mode)
    {
        if (mode == "microsoft")
        {
            if (!services.Auth.HasSavedMicrosoftAccount)
                throw new AccountLoginException("请先登录正版账号。", true);
            return await services.Auth.AuthenticateMicrosoftAsync(false, services.Token);
        }
        return await services.Auth.EnsureMuaAsync(false, services.Token);
    }

    async Task ApplySelectionAsync(
        string mode,
        MSession? session,
        bool verified = true,
        AccountIdentity? identity = null)
    {
        Apply(accountWriter, mode, session, verified, persist: false, identity: identity);
        try
        {
            await services.Store.PersistAsync();
        }
        catch (IOException)
        {
            services.Notifier.Send(new
            {
                type = "toast",
                message = "账号已切换，但设置未能保存，重启后可能恢复上次选择"
            });
        }
        catch (UnauthorizedAccessException)
        {
            services.Notifier.Send(new
            {
                type = "toast",
                message = "账号已切换，但配置目录不可写，请检查目录权限"
            });
        }
    }

    public static void Apply(
        IAccountStateWriter writer,
        string mode,
        MSession? session,
        bool verified = true,
        bool persist = true,
        AccountIdentity? identity = null) =>
        writer.Apply(mode, session, verified, persist, identity);

    public async Task<AuthResultInfo> ForgetAsync(string mode)
    {
        var revision = services.Revision.Next();
        services.Sessions.Forget(mode);
        if (mode == "microsoft")
            services.Auth.ForgetMicrosoft();
        else if (mode == "mua")
            await services.Auth.ForgetMuaAsync(services.Token);
        else
            throw new ArgumentException("未知的在线账号模式。");
        if (revision != services.Revision.Current)
            return new(false, false, "账号移除已被更新的账号操作取代。");
        if (services.Record.Mode == mode)
            Apply(accountWriter, "offline", null);
        return new(true, false, "已移除本机保存的账号。");
    }
}
