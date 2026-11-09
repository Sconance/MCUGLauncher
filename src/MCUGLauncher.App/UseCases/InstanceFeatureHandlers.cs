using System.Text.Json;
using MCUGLauncher.Contracts;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

[FeatureHandler(nameof(ILauncherService.SelectInstanceAsync))]
internal sealed class SelectInstanceFeatureHandler(LauncherContext context)
    : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token)
    {
        new InstanceCommands(context).SelectInstance(ReadString(arguments, "instance"));
        return Task.FromResult(new SavedInfo("已切换实例。"));
    }
}

[FeatureHandler(nameof(ILauncherService.SelectVersionAsync))]
internal sealed class SelectVersionFeatureHandler(LauncherContext context)
    : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token)
    {
        new InstanceCommands(context).SelectVersion(ReadString(arguments, "version"));
        return Task.FromResult(new SavedInfo("已切换版本。"));
    }
}

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

[FeatureHandler(nameof(ILauncherService.ApplyUpdatesAsync))]
internal sealed class ApplyUpdatesFeatureHandler(LauncherContext context)
    : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult(new ContentUpdateCommands(context).Apply(ReadString(arguments, "token")));
}
