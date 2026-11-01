using BlockLaunch;

namespace MCUGLauncher.UseCases;

// Handles account use cases; no WPF controls or browser access.
internal sealed class AccountCommands(LauncherContext context)
{
    public async Task LoginMicrosoftAsync()
    {
        if (context.Microsoft == null)
        {
            context.Send(new
            {
                type = "authResult",
                ok = false,
                message = "登录组件未就绪。"
            });
            return;
        }
        context.Send(new
        {
            type = "authResult",
            pending = true,
            message = "已打开微软登录页，请完成授权…"
        });
        try
        {
            var revision = Interlocked.Increment(ref context.AccountRevision);
            var session = await context.Microsoft.Authenticate(true, context.Token);
            if (revision != context.AccountRevision)
                return;
            AccountModeCommands.Apply(context, "microsoft", session);
            context.Send(new
            {
                type = "authResult",
                ok = true,
                message = $"已登录正版账号 {session.Username}"
            });
        }
        catch (Exception ex)
        {
            context.Send(new
            {
                type = "authResult",
                ok = false,
                message = ex is AccountLoginException ? ex.Message : MicrosoftAccountService.Translate(ex, false).Message
            });
        }
    }

    public async Task LoginMuaAsync(string account, string password)
    {
        if (context.Mua == null)
        {
            context.Send(new
            {
                type = "authResult",
                ok = false,
                message = "登录组件未就绪。"
            });
            return;
        }
        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrEmpty(password))
        {
            context.Send(new
            {
                type = "authResult",
                ok = false,
                message = "请填写 MUA 账号和密码。"
            });
            return;
        }
        context.Send(new
        {
            type = "authResult",
            pending = true,
            message = "正在登录 MUA…"
        });
        try
        {
            var revision = Interlocked.Increment(ref context.AccountRevision);
            var credential = await context.Mua.Login(account.Trim(), password,
                profiles => Task.FromResult<MuaProfile?>(profiles.FirstOrDefault()), context.Token);
            if (revision != context.AccountRevision)
                return;
            AccountModeCommands.Apply(context, "mua", MuaAccountService.Session(credential));
            context.Send(new
            {
                type = "authResult",
                ok = true,
                message = $"已登录 MUA 账号 {credential.Profile.Name}"
            });
        }
        catch (Exception)
        {
            context.Send(new
            {
                type = "authResult",
                ok = false,
                message = "MUA 登录失败。请检查账号、密码和网络后重试。"
            });
        }
    }
}
