using System.Text.Json;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.UseCases;

/// <summary>账号领域模块：登录、账号切换、MUA 档案、头像皮肤与账号事件。</summary>
internal sealed class AccountModule
{
    readonly IFeatureRequestInvoker invoker;
    readonly Func<object, Task> command;

    public AccountModule(IFeatureRequestInvoker invoker, Func<object, Task> command)
    {
        this.invoker = invoker;
        this.command = command;
    }

    public event Action<AuthResultInfo>? AuthResultReceived;
    public event Action<AccountStateInfo>? AccountStateChanged;
    public event Action<AccountSwitchResultInfo>? AccountSwitchResultReceived;
    public event Action<MuaProfilesInfo>? MuaProfilesReceived;

    public Task<AuthResultInfo> LoginMicrosoftAsync() =>
        invoker.InvokeFeatureAsync<AuthResultInfo>(nameof(LoginMicrosoftAsync), new
        {
        });

    public Task<AuthResultInfo> LoginMuaAsync(string account, string password) =>
        invoker.InvokeFeatureAsync<AuthResultInfo>(nameof(LoginMuaAsync), new
        {
            account,
            password
        });

    public Task<AccountSwitchResultInfo> SwitchAccountModeAsync(string mode) =>
        invoker.InvokeFeatureAsync<AccountSwitchResultInfo>(nameof(SwitchAccountModeAsync), new
        {
            mode
        });

    public Task<AuthResultInfo> ForgetAccountAsync(string mode) =>
        invoker.InvokeFeatureAsync<AuthResultInfo>(nameof(ForgetAccountAsync), new
        {
            mode
        });

    public Task<AuthResultInfo> LogoutAccountAsync() =>
        invoker.InvokeFeatureAsync<AuthResultInfo>(nameof(LogoutAccountAsync), new
        {
        });

    public Task SelectMuaProfileAsync(string token, string profileId) =>
        command(new
        {
            type = "selectMuaProfile",
            token,
            profileId
        });

    public Task<AvatarInfo?> ReadAvatarAsync() =>
        invoker.InvokeFeatureAsync<AvatarInfo?>(nameof(ReadAvatarAsync), new
        {
        });

    public Task<AvatarInfo?> SetOfflineSkinAsync(string path) =>
        invoker.InvokeFeatureAsync<AvatarInfo?>(nameof(SetOfflineSkinAsync), new
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
