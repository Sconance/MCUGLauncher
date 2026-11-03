using System.Text.Json;
using MCUGLauncher.Contracts;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

[FeatureHandler(nameof(ILauncherService.LaunchAsync))]
internal sealed class LaunchFeatureHandler(LauncherContext context)
    : IFeatureHandler<LaunchResultInfo>
{
    public Task<LaunchResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new GameCommands(context).LaunchAsync(ReadString(arguments, "version"));
}

[FeatureHandler(nameof(ILauncherService.ReadAvatarAsync))]
internal sealed class ReadAvatarFeatureHandler(LauncherContext context)
    : IFeatureHandler<AvatarInfo?>
{
    public Task<AvatarInfo?> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new AvatarCommands(context, context.AvatarEncoder).ReadAsync();
}

[FeatureHandler(nameof(ILauncherService.SetOfflineSkinAsync))]
internal sealed class SetOfflineSkinFeatureHandler(LauncherContext context)
    : IFeatureHandler<AvatarInfo?>
{
    public Task<AvatarInfo?> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new AvatarCommands(context, context.AvatarEncoder).SetAsync(
            JsonSerializer.SerializeToElement(new
            {
                path = ReadString(arguments, "path")
            }));
}
