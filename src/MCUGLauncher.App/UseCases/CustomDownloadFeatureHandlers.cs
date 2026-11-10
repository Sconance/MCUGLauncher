using System.Text.Json;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.UseCases;

[FeatureHandler(nameof(ILauncherService.StartCustomDownloadAsync))]
internal sealed class StartCustomDownloadFeatureHandler(LauncherContext context)
    : IFeatureHandler<CustomDownloadInfo>
{
    public Task<CustomDownloadInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult(new CustomDownloadCommands(context).Start(arguments));
}
