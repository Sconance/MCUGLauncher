using System.Text.Json;
using MCUGLauncher.Contracts;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

[FeatureHandler(nameof(ILauncherService.ToggleModAsync))]
internal sealed class ToggleModFeatureHandler(LauncherContext context)
    : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult(new InstanceCommands(context).ToggleMod(
            ReadString(arguments, "file"),
            arguments.GetProperty("enabled").GetBoolean()));
}

[FeatureHandler(nameof(ILauncherService.DeleteModAsync))]
internal sealed class DeleteModFeatureHandler(LauncherContext context)
    : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult(new InstanceCommands(context).DeleteMod(ReadString(arguments, "file")));
}
