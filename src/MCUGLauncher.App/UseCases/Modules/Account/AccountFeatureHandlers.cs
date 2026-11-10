using System.Text.Json;
using MCUGLauncher.Contracts;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

[FeatureHandler(nameof(ILauncherService.LoginMicrosoftAsync))]
internal sealed class MicrosoftLoginFeatureHandler(IAccountContext context, IAccountStateWriter writer)
    : IFeatureHandler<AuthResultInfo>
{
    public Task<AuthResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new AccountCommands(context, writer).LoginMicrosoftAsync();
}

[FeatureHandler(nameof(ILauncherService.SelectMuaProfileAsync))]
internal sealed class SelectMuaProfileFeatureHandler(IAccountContext context, IAccountStateWriter writer)
    : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token)
    {
        new MuaProfileSelection(context).Select(arguments);
        return Task.FromResult(new SavedInfo("已选择 MUA 角色。"));
    }
}

[FeatureHandler(nameof(ILauncherService.LoginMuaAsync))]
internal sealed class MuaLoginFeatureHandler(IAccountContext context, IAccountStateWriter writer)
    : IFeatureHandler<AuthResultInfo>
{
    public Task<AuthResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new AccountCommands(context, writer).LoginMuaAsync(
            ReadString(arguments, "account"),
            ReadString(arguments, "password"));
}

[FeatureHandler(nameof(ILauncherService.SwitchAccountModeAsync))]
internal sealed class AccountModeFeatureHandler(IAccountContext context, IAccountStateWriter writer)
    : IFeatureHandler<AccountSwitchResultInfo>
{
    public Task<AccountSwitchResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new AccountModeCommands(context, writer: writer).SwitchAsync(ReadString(arguments, "mode"));
}

[FeatureHandler(nameof(ILauncherService.ForgetAccountAsync))]
internal sealed class ForgetAccountFeatureHandler(IAccountContext context, IAccountStateWriter writer)
    : IFeatureHandler<AuthResultInfo>
{
    public Task<AuthResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new AccountModeCommands(context, writer: writer).ForgetAsync(ReadString(arguments, "mode"));
}

[FeatureHandler(nameof(ILauncherService.LogoutAccountAsync))]
internal sealed class LogoutAccountFeatureHandler(IAccountContext context, IAccountStateWriter writer)
    : IFeatureHandler<AuthResultInfo>
{
    public async Task<AuthResultInfo> HandleAsync(JsonElement arguments, CancellationToken token)
    {
        var result = await new AccountModeCommands(context, writer: writer).SwitchAsync("offline");
        return new(true, false, result.Message);
    }
}
