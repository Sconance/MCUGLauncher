using MCUGLauncher;
using CmlLib.Core.Auth;

namespace MCUGLauncher.UseCases;

// Mode selection preserves credentials. Only explicit removal deletes an account.
internal sealed class AccountModeCommands(LauncherContext context, Func<string, Task<MSession>>? restore = null)
{
    public Task RestoreSelectedAsync() => SwitchAsync(context.Config.AccountType, false);

    public async Task SwitchAsync(string mode, bool prompt = true)
    {
        if (mode is not ("offline" or "microsoft" or "mua"))
            throw new ArgumentException("未知的账号模式。");
        var revision = Interlocked.Increment(ref context.AccountRevision);
        if (mode == "offline")
        {
            Apply(context, mode, null);
            return;
        }
        var cached = context.AccountSessions.Find(mode);
        if (cached != null)
        {
            Apply(context, mode, cached, false);
            return;
        }

        try
        {
            var session = restore != null ? await restore(mode) : await RestoreAsync(mode);
            if (revision != context.AccountRevision)
                return;
            Apply(context, mode, session);
            context.Send(new
            {
                type = "authResult",
                ok = true,
                message = "已切换到 " + session.Username
            });
        }
        catch (Exception error)
        {
            if (revision != context.AccountRevision)
                return;
            var needsLogin = error is AccountLoginException { RequiresInteractive: true };
            context.Send(new
            {
                type = "accountSwitchResult",
                account = mode,
                needsLogin = prompt && needsLogin,
                message = needsLogin ? "账号授权需要重新确认，请登录。" : "暂时无法恢复账号，请检查网络后重试。已保存的账号仍然保留。"
            });
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
                context.AccountSessions.Remember(mode, session);
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

    public static void Apply(LauncherContext context, string mode, MSession? session, bool verified = true)
    {
        var player = mode == "offline" ? context.Config.OfflinePlayer : session?.Username;
        if (string.IsNullOrWhiteSpace(player))
            throw new AccountLoginException("账号没有有效的玩家名，请重新登录。", true);
        if (context.Config.AccountType == "offline")
            context.Config.OfflinePlayer = context.Config.Player;
        if (session != null && verified)
            context.AccountSessions.Remember(mode, session);
        context.OnlineSession = session;
        context.Config.AccountType = mode;
        context.Config.Player = player;
        context.Store.Save(context.Config);
        context.SendAccountState();
    }

    public async Task ForgetAsync(string mode)
    {
        var revision = Interlocked.Increment(ref context.AccountRevision);
        context.AccountSessions.Forget(mode);
        if (mode == "microsoft")
            context.Microsoft?.Forget();
        else if (mode == "mua" && context.Mua != null)
            await context.Mua.Forget(context.Token);
        else
            throw new ArgumentException("未知的在线账号模式。");
        if (revision != context.AccountRevision)
            return;
        if (context.Config.AccountType == mode)
            Apply(context, "offline", null);
        context.Send(new
        {
            type = "authResult",
            ok = true,
            message = "已移除本机保存的账号。"
        });
    }
}
