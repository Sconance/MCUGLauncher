using System.Text.Json;
using MCUGLauncher.Contracts;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

[FeatureHandler(nameof(ILauncherService.PreviewUninstallAsync))]
internal sealed class PreviewUninstallFeatureHandler(LauncherContext context)
    : IFeatureHandler<UninstallPreviewInfo>
{
    public Task<UninstallPreviewInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new UninstallCommands(context).PreviewAsync(ReadString(arguments, "version"));
}

[FeatureHandler(nameof(ILauncherService.PreviewDeleteVersionAsync))]
internal sealed class PreviewDeleteVersionFeatureHandler(LauncherContext context)
    : IFeatureHandler<UninstallPreviewInfo>
{
    public Task<UninstallPreviewInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new UninstallCommands(context).PreviewAsync(ReadString(arguments, "version"));
}

[FeatureHandler(nameof(ILauncherService.CheckUpdatesAsync))]
internal sealed class CheckUpdatesFeatureHandler(LauncherContext context)
    : IFeatureHandler<ContentUpdatesInfo>
{
    public Task<ContentUpdatesInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new ContentUpdateCommands(context).CheckAsync(ReadString(arguments, "version"));
}

[FeatureHandler(nameof(ILauncherService.PreviewUpdatesAsync))]
internal sealed class PreviewUpdatesFeatureHandler(LauncherContext context)
    : IFeatureHandler<ContentUpdatePreviewInfo>
{
    public Task<ContentUpdatePreviewInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new ContentUpdateCommands(context).PreviewAsync(arguments);
}
