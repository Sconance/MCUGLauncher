namespace MCUGLauncher.Contracts;

/// <summary>微软/MUA/离线账号、登录结果与头像皮肤。</summary>
public interface IAccountService
{
    event Action<AuthResultInfo>? AuthResultReceived;
    event Action<AccountStateInfo>? AccountStateChanged;
    event Action<AccountSwitchResultInfo>? AccountSwitchResultReceived;
    event Action<MuaProfilesInfo>? MuaProfilesReceived;

    [FeatureOperation]
    Task<AuthResultInfo> LoginMicrosoftAsync() =>
        FeatureRequest.InvokeAsync<AuthResultInfo>(this, nameof(LoginMicrosoftAsync), new
        {
        });

    [FeatureOperation]
    Task<AuthResultInfo> LoginMuaAsync(string account, string password) =>
        FeatureRequest.InvokeAsync<AuthResultInfo>(this, nameof(LoginMuaAsync), new
        {
            account,
            password
        });

    [FeatureOperation]
    Task<AccountSwitchResultInfo> SwitchAccountModeAsync(string mode) =>
        FeatureRequest.InvokeAsync<AccountSwitchResultInfo>(this, nameof(SwitchAccountModeAsync), new
        {
            mode
        });

    [FeatureOperation]
    Task<AuthResultInfo> ForgetAccountAsync(string mode) =>
        FeatureRequest.InvokeAsync<AuthResultInfo>(this, nameof(ForgetAccountAsync), new
        {
            mode
        });

    [FeatureOperation]
    Task<AuthResultInfo> LogoutAccountAsync() =>
        FeatureRequest.InvokeAsync<AuthResultInfo>(this, nameof(LogoutAccountAsync), new
        {
        });

    Task SelectMuaProfileAsync(string token, string profileId);

    [FeatureOperation]
    Task<AvatarInfo?> ReadAvatarAsync() =>
        FeatureRequest.InvokeAsync<AvatarInfo?>(this, nameof(ReadAvatarAsync), new
        {
        });

    [FeatureOperation]
    Task<AvatarInfo?> SetOfflineSkinAsync(string path) =>
        FeatureRequest.InvokeAsync<AvatarInfo?>(this, nameof(SetOfflineSkinAsync), new
        {
            path
        });
}
