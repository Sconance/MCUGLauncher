namespace MCUGLauncher.Contracts;

/// <summary>微软/MUA/离线账号、登录结果与头像皮肤。</summary>
public interface IAccountService
{
    AccountStateInfo? AccountState
    {
        get;
    }

    event Action<AuthResultInfo>? AuthResultReceived;
    event Action<AccountStateInfo>? AccountStateChanged;
    event Action<AccountSwitchResultInfo>? AccountSwitchResultReceived;
    event Action<MuaProfilesInfo>? MuaProfilesReceived;

    [FeatureOperation]
    Task<AuthResultInfo> LoginMicrosoftAsync();

    [FeatureOperation]
    Task<AuthResultInfo> LoginMuaAsync(string account, string password);

    [FeatureOperation]
    Task<AccountSwitchResultInfo> SwitchAccountModeAsync(string mode);

    [FeatureOperation]
    Task<AuthResultInfo> ForgetAccountAsync(string mode);

    [FeatureOperation]
    Task<AuthResultInfo> LogoutAccountAsync();

    [FeatureOperation]
    Task<SavedInfo> SelectMuaProfileAsync(string token, string profileId);

    [FeatureOperation]
    Task<AvatarInfo?> ReadAvatarAsync();

    [FeatureOperation]
    Task<AvatarInfo?> SetOfflineSkinAsync(string path);
}
