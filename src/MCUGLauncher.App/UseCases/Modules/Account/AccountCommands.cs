using BlockLaunch;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.UseCases;

// Handles account use cases; no WPF controls or browser access.
internal sealed class AccountCommands(AccountServices services, IAccountStateWriter writer)
{
    readonly MuaProfileSelection profiles = new(services);

    public void SelectMuaProfile(System.Text.Json.JsonElement message) => profiles.Select(message);

    public async Task<AuthResultInfo> LoginMicrosoftAsync()
    {
        try
        {
            var revision = services.Revision.Next();
            var session = await services.Auth.AuthenticateMicrosoftAsync(true, services.Token);
            if (revision != services.Revision.Current)
                return new(false, false, "登录已被更新的账号操作取代。");
            AccountModeCommands.Apply(writer, "microsoft", session);
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
        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrEmpty(password))
            return new(false, false, "请填写 MUA 账号和密码。");
        try
        {
            var revision = services.Revision.Next();
            var session = await services.Auth.LoginMuaAsync(
                account.Trim(),
                password,
                profiles.ChooseAsync,
                services.Token);
            if (revision != services.Revision.Current)
                return new(false, false, "登录已被更新的账号操作取代。");
            AccountModeCommands.Apply(writer, "mua", session);
            return new(true, false, $"已登录 MUA 账号 {session.Username}");
        }
        catch (Exception)
        {
            return new(false, false, "MUA 登录失败。请检查账号、密码和网络后重试。");
        }
    }
}
