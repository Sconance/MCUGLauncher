using BlockLaunch;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.UseCases;

// Handles account use cases; no WPF controls or browser access.
internal sealed class AccountCommands(IAccountContext context)
{
    readonly MuaProfileSelection profiles = new(context);
    public void SelectMuaProfile(System.Text.Json.JsonElement message) => profiles.Select(message);

    public async Task<AuthResultInfo> LoginMicrosoftAsync()
    {
        if (context.Microsoft == null)
            return new(false, false, "登录组件未就绪。");
        try
        {
            var revision = context.NextRevision();
            var session = await context.Microsoft.Authenticate(true, context.Token);
            if (revision != context.AccountRevision)
                return new(false, false, "登录已被更新的账号操作取代。");
            AccountModeCommands.Apply(context, "microsoft", session);
            return new(true, false, $"已登录正版账号 {session.Username}");
        }
        catch (Exception ex)
        {
            return new(false, false,
                ex is AccountLoginException ? ex.Message : MicrosoftAccountService.Translate(ex, false).Message);
        }
    }

    public async Task<AuthResultInfo> LoginMuaAsync(string account, string password)
    {
        if (context.Mua == null)
            return new(false, false, "登录组件未就绪。");
        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrEmpty(password))
            return new(false, false, "请填写 MUA 账号和密码。");
        try
        {
            var revision = context.NextRevision();
            var credential = await context.Mua.Login(account.Trim(), password,
                profiles.ChooseAsync, context.Token);
            if (revision != context.AccountRevision)
                return new(false, false, "登录已被更新的账号操作取代。");
            AccountModeCommands.Apply(context, "mua", MuaAccountService.Session(credential));
            return new(true, false, $"已登录 MUA 账号 {credential.Profile.Name}");
        }
        catch (Exception)
        {
            return new(false, false, "MUA 登录失败。请检查账号、密码和网络后重试。");
        }
    }
}
