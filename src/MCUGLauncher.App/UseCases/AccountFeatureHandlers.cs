using System.Text.Json;
using MCUGLauncher.Contracts;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

[FeatureHandler(nameof(ILauncherService.LoginMicrosoftAsync))]
internal sealed class MicrosoftLoginFeatureHandler(LauncherContext context)
    : IFeatureHandler<AuthResultInfo>
{
    public Task<AuthResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new AccountCommands(context).LoginMicrosoftAsync();
}

[FeatureHandler(nameof(ILauncherService.LoginMuaAsync))]
internal sealed class MuaLoginFeatureHandler(LauncherContext context)
    : IFeatureHandler<AuthResultInfo>
{
    public Task<AuthResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new AccountCommands(context).LoginMuaAsync(
            ReadString(arguments, "account"),
            ReadString(arguments, "password"));
}

[FeatureHandler(nameof(ILauncherService.SwitchAccountModeAsync))]
internal sealed class AccountModeFeatureHandler(LauncherContext context)
    : IFeatureHandler<AccountSwitchResultInfo>
{
    public Task<AccountSwitchResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new AccountModeCommands(context).SwitchAsync(ReadString(arguments, "mode"));
}

[FeatureHandler(nameof(ILauncherService.ForgetAccountAsync))]
internal sealed class ForgetAccountFeatureHandler(LauncherContext context)
    : IFeatureHandler<AuthResultInfo>
{
    public Task<AuthResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new AccountModeCommands(context).ForgetAsync(ReadString(arguments, "mode"));
}

[FeatureHandler(nameof(ILauncherService.LogoutAccountAsync))]
internal sealed class LogoutAccountFeatureHandler(LauncherContext context)
    : IFeatureHandler<AuthResultInfo>
{
    public async Task<AuthResultInfo> HandleAsync(JsonElement arguments, CancellationToken token)
    {
        var result = await new AccountModeCommands(context).SwitchAsync("offline");
        return new(true, false, result.Message);
    }
}
