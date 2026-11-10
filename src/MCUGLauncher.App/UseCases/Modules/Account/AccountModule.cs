using System.Text.Json;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.UseCases;

/// <summary>账号领域模块：登录、账号切换、MUA 档案、头像皮肤与账号事件。</summary>
internal sealed class AccountModule(IAccountFeatureInvoker invoker)
{
    public event Action<AuthResultInfo>? AuthResultReceived;
    public event Action<AccountStateInfo>? AccountStateChanged;
    public event Action<AccountSwitchResultInfo>? AccountSwitchResultReceived;
    public event Action<MuaProfilesInfo>? MuaProfilesReceived;

    public Task<AuthResultInfo> LoginMicrosoftAsync() =>
        invoker.InvokeAsync<AuthResultInfo>(nameof(LoginMicrosoftAsync), new
        {
        });

    public Task<AuthResultInfo> LoginMuaAsync(string account, string password) =>
        invoker.InvokeAsync<AuthResultInfo>(nameof(LoginMuaAsync), new
        {
            account,
            password
        });

    public Task<AccountSwitchResultInfo> SwitchAccountModeAsync(string mode) =>
        invoker.InvokeAsync<AccountSwitchResultInfo>(nameof(SwitchAccountModeAsync), new
        {
            mode
        });

    public Task<AuthResultInfo> ForgetAccountAsync(string mode) =>
        invoker.InvokeAsync<AuthResultInfo>(nameof(ForgetAccountAsync), new
        {
            mode
        });

    public Task<AuthResultInfo> LogoutAccountAsync() =>
        invoker.InvokeAsync<AuthResultInfo>(nameof(LogoutAccountAsync), new
        {
        });

    public Task<SavedInfo> SelectMuaProfileAsync(string token, string profileId) =>
        invoker.InvokeAsync<SavedInfo>(nameof(SelectMuaProfileAsync), new
        {
            token,
            profileId
        });

    public Task<AvatarInfo?> ReadAvatarAsync() =>
        invoker.InvokeAsync<AvatarInfo?>(nameof(ReadAvatarAsync), new
        {
        });

    public Task<AvatarInfo?> SetOfflineSkinAsync(string path) =>
        invoker.InvokeAsync<AvatarInfo?>(nameof(SetOfflineSkinAsync), new
        {
            path
        });

    /// <summary>处理账号相关消息；非账号消息返回 false，由 LauncherService 继续分发。</summary>
    public bool Handle(string type, JsonElement root)
    {
        switch (type)
        {
            case "authResult":
                AuthResultReceived?.Invoke(AuthResultInfo.From(root));
                return true;
            case "accountState":
                AccountStateChanged?.Invoke(AccountStateInfo.From(root));
                return true;
            case "accountSwitchResult":
                AccountSwitchResultReceived?.Invoke(AccountSwitchResultInfo.From(root));
                return true;
            case "muaProfiles":
                MuaProfilesReceived?.Invoke(MuaProfilesInfo.From(root));
                return true;
            default:
                return false;
        }
    }
}
