using System.Text.Json;
using MCUGLauncher.Contracts;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

[FeatureHandler(nameof(ILauncherService.ListVanillaVersionsAsync))]
internal sealed class VanillaVersionsFeatureHandler(LauncherContext context)
    : IFeatureHandler<VanillaVersionsResultInfo>
{
    public Task<VanillaVersionsResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new ResourceCommands(context).SendVanillaVersionsAsync();
}

[FeatureHandler(nameof(ILauncherService.ListLoadersAsync))]
internal sealed class LoaderListFeatureHandler(LauncherContext context)
    : IFeatureHandler<LoaderListResultInfo>
{
    public Task<LoaderListResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        context.Installations.LoadersAsync(arguments);
}

[FeatureHandler(nameof(ILauncherService.PreviewPackAsync))]
internal sealed class PackPreviewFeatureHandler(LauncherContext context)
    : IFeatureHandler<PackPreviewResultInfo>
{
    public Task<PackPreviewResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        context.Installations.PreviewPackAsync(arguments);
}

[FeatureHandler(nameof(ILauncherService.PreviewLocalImportAsync))]
internal sealed class LocalImportPreviewFeatureHandler(LauncherContext context)
    : IFeatureHandler<LocalImportPreviewInfo>
{
    public Task<LocalImportPreviewInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        context.LocalImports.PreviewAsync(
            ReadString(arguments, "path"),
            ReadString(arguments, "instance"),
            ReadString(arguments, "resourceKind"));
}

[FeatureHandler(nameof(ILauncherService.ConfirmLocalImportAsync))]
internal sealed class LocalImportConfirmFeatureHandler(LauncherContext context)
    : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        context.LocalImports.ConfirmAsync(ReadString(arguments, "token"));
}

[FeatureHandler(nameof(ILauncherService.ListContentVersionsAsync))]
internal sealed class ResourceVersionsFeatureHandler(LauncherContext context)
    : IFeatureHandler<ResourceVersionsResultInfo>
{
    public Task<ResourceVersionsResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new ResourceChoiceCommands(context).ListAsync(arguments);
}
