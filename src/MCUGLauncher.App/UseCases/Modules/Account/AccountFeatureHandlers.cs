using System.Text.Json;
using MCUGLauncher.Contracts;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

[FeatureHandler(nameof(ILauncherService.LoginMicrosoftAsync))]
internal sealed class MicrosoftLoginFeatureHandler(AccountServices services, IAccountStateWriter writer)
    : IFeatureHandler<AuthResultInfo>
{
    public Task<AuthResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new AccountCommands(services, writer).LoginMicrosoftAsync();
}

[FeatureHandler(nameof(ILauncherService.SelectMuaProfileAsync))]
internal sealed class SelectMuaProfileFeatureHandler(AccountServices services, IAccountStateWriter writer)
    : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token)
    {
        new MuaProfileSelection(services).Select(arguments);
        return Task.FromResult(new SavedInfo("已选择 MUA 角色。"));
    }
}

[FeatureHandler(nameof(ILauncherService.LoginMuaAsync))]
internal sealed class MuaLoginFeatureHandler(AccountServices services, IAccountStateWriter writer)
    : IFeatureHandler<AuthResultInfo>
{
    public Task<AuthResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new AccountCommands(services, writer).LoginMuaAsync(
            ReadString(arguments, "account"),
            ReadString(arguments, "password"));
}

[FeatureHandler(nameof(ILauncherService.SwitchAccountModeAsync))]
internal sealed class AccountModeFeatureHandler(AccountServices services, IAccountStateWriter writer)
    : IFeatureHandler<AccountSwitchResultInfo>
{
    public Task<AccountSwitchResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new AccountModeCommands(services, writer).SwitchAsync(ReadString(arguments, "mode"));
}

[FeatureHandler(nameof(ILauncherService.ForgetAccountAsync))]
internal sealed class ForgetAccountFeatureHandler(AccountServices services, IAccountStateWriter writer)
    : IFeatureHandler<AuthResultInfo>
{
    public Task<AuthResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new AccountModeCommands(services, writer).ForgetAsync(ReadString(arguments, "mode"));
}

[FeatureHandler(nameof(ILauncherService.LogoutAccountAsync))]
internal sealed class LogoutAccountFeatureHandler(AccountServices services, IAccountStateWriter writer)
    : IFeatureHandler<AuthResultInfo>
{
    public async Task<AuthResultInfo> HandleAsync(JsonElement arguments, CancellationToken token)
    {
        var result = await new AccountModeCommands(services, writer).SwitchAsync("offline");
        return new(true, false, result.Message);
    }
}
