using System.Text.Json;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.UseCases;

[FeatureHandler(nameof(ILauncherService.SearchContentAsync))]
internal sealed class ContentSearchFeatureHandler(LauncherContext context)
    : IFeatureHandler<ContentSearchResultInfo>
{
    public Task<ContentSearchResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new ResourceCommands(context).SearchContentAsync(arguments);
}
