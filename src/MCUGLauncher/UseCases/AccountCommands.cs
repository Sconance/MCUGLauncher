using System.Text.Json;
using BlockLaunch;
using CmlLib.Core.Auth;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;

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
            var session = await context.Microsoft.Authenticate(true, context.Token);
            context.OnlineSession = session;
            context.Config.AccountType = "microsoft";
            context.Config.Player = session.Username ?? context.Config.Player;
            context.Store.Save(context.Config);
            context.Send(new
            {
                type = "authResult",
                ok = true,
                message = $"已登录正版账号 {session.Username}"
            });
            context.RefreshState();
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

    public async Task RestoreMicrosoftAsync()
    {
        if (context.Config.AccountType != "microsoft" || context.Microsoft == null)
            return;
        try
        {
            var session = await context.Microsoft.Authenticate(false, context.Token);
            context.OnlineSession = session;
            context.Config.Player = session.Username ?? context.Config.Player;
            context.Store.Save(context.Config);
            context.RefreshState();
        }
        catch
        {
            context.OnlineSession = null;
            context.Config.AccountType = "offline";
            context.Store.Save(context.Config);
            context.RefreshState();
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
            var credential = await context.Mua.Login(account.Trim(), password,
                profiles => Task.FromResult<MuaProfile?>(profiles.FirstOrDefault()), context.Token);
            context.OnlineSession = MuaAccountService.Session(credential);
            context.Config.AccountType = "mua";
            context.Config.Player = credential.Profile.Name;
            context.Store.Save(context.Config);
            context.Send(new
            {
                type = "authResult",
                ok = true,
                message = $"已登录 MUA 账号 {credential.Profile.Name}"
            });
            context.RefreshState();
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
