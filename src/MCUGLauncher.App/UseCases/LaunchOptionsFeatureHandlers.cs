using System.Text.Json;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.UseCases;

[FeatureHandler(nameof(ILauncherService.ReadLaunchOptionsAsync))]
internal sealed class ReadLaunchOptionsFeatureHandler(LauncherContext context)
    : IFeatureHandler<LaunchOptionsInfo>
{
    public Task<LaunchOptionsInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult(new LaunchOptionsCommands(context).Read(arguments));
}

[FeatureHandler(nameof(ILauncherService.SaveLaunchOptionsAsync))]
internal sealed class SaveLaunchOptionsFeatureHandler(LauncherContext context)
    : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult(new LaunchOptionsCommands(context).Save(arguments));
}
