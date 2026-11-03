using MCUGLauncher;

namespace MCUGLauncher.UseCases;

/// <summary>执行微软和 MUA 交互登录，提交已验证会话与登录结果。</summary>
/// <remarks>
/// B 类：LauncherContext.Microsoft/Mua/Token/AccountRevision/Send(object) 提供认证、取消与结果发布；
/// MicrosoftAccountService.Authenticate(bool,CancellationToken)、Translate(Exception,bool,string?)；
/// MuaAccountService.Login(string,string,Func&lt;IReadOnlyList&lt;MuaProfile&gt;,Task&lt;MuaProfile?&gt;&gt;,CancellationToken)/Session(MuaCredential) 提供登录及会话转换；
/// MuaProfileSelection.ChooseAsync/Select 提供角色选择，AccountModeCommands.Apply 提交身份。
/// 依赖的准确签名见 docs/components/AccountCommands.md；不依赖 UI 或桌面窗口。
/// </remarks>
internal sealed class AccountCommands(LauncherContext context)
{
    readonly MuaProfileSelection profiles = new(context);
    public void SelectMuaProfile(System.Text.Json.JsonElement message) => profiles.Select(message);

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
                profiles.ChooseAsync, context.Token);
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
