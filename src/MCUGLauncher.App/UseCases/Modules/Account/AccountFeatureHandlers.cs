using System.Text.Json;
using MCUGLauncher.Contracts;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

[FeatureHandler(nameof(ILauncherService.LoginMicrosoftAsync))]
internal sealed class MicrosoftLoginFeatureHandler(IAccountContext context)
    : IFeatureHandler<AuthResultInfo>
{
    public Task<AuthResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new AccountCommands(context, context.StateWriter).LoginMicrosoftAsync();
}

[FeatureHandler(nameof(ILauncherService.SelectMuaProfileAsync))]
internal sealed class SelectMuaProfileFeatureHandler(IAccountContext context)
    : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token)
    {
        new MuaProfileSelection(context).Select(arguments);
        return Task.FromResult(new SavedInfo("已选择 MUA 角色。"));
    }
}

[FeatureHandler(nameof(ILauncherService.LoginMuaAsync))]
internal sealed class MuaLoginFeatureHandler(IAccountContext context)
    : IFeatureHandler<AuthResultInfo>
{
    public Task<AuthResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new AccountCommands(context, context.StateWriter).LoginMuaAsync(
            ReadString(arguments, "account"),
            ReadString(arguments, "password"));
}

[FeatureHandler(nameof(ILauncherService.SwitchAccountModeAsync))]
internal sealed class AccountModeFeatureHandler(IAccountContext context)
    : IFeatureHandler<AccountSwitchResultInfo>
{
    public Task<AccountSwitchResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new AccountModeCommands(context, writer: context.StateWriter).SwitchAsync(ReadString(arguments, "mode"));
}

[FeatureHandler(nameof(ILauncherService.ForgetAccountAsync))]
internal sealed class ForgetAccountFeatureHandler(IAccountContext context)
    : IFeatureHandler<AuthResultInfo>
{
    public Task<AuthResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new AccountModeCommands(context, writer: context.StateWriter).ForgetAsync(ReadString(arguments, "mode"));
}

[FeatureHandler(nameof(ILauncherService.LogoutAccountAsync))]
internal sealed class LogoutAccountFeatureHandler(IAccountContext context)
    : IFeatureHandler<AuthResultInfo>
{
    public async Task<AuthResultInfo> HandleAsync(JsonElement arguments, CancellationToken token)
    {
        var result = await new AccountModeCommands(context, writer: context.StateWriter).SwitchAsync("offline");
        return new(true, false, result.Message);
    }
}
