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

/// <summary>收藏启动的类型化入口，复用统一游戏启动用例。</summary>
/// <remarks>B 类：依赖 ProtocolValues.ReadString(JsonElement, string)、LauncherContext
/// 以及 GameCommands.LaunchToServerAsync(string, string)，不读取控件或重新实现启动逻辑。</remarks>
[FeatureHandler(nameof(ILauncherService.LaunchToServerAsync))]
internal sealed class LaunchToServerFeatureHandler(LauncherContext context)
    : IFeatureHandler<LaunchResultInfo>
{
    public Task<LaunchResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new GameCommands(context).LaunchToServerAsync(
            ReadString(arguments, "instance"), ReadString(arguments, "serverAddress"));
}

[FeatureHandler(nameof(ILauncherService.ReadAvatarAsync))]
internal sealed class ReadAvatarFeatureHandler(LauncherContext context)
    : IFeatureHandler<AvatarInfo?>
{
    public Task<AvatarInfo?> HandleAsync(JsonElement arguments, CancellationToken token) =>
        context.Avatars.ReadAsync();
}

[FeatureHandler(nameof(ILauncherService.SetOfflineSkinAsync))]
internal sealed class SetOfflineSkinFeatureHandler(LauncherContext context)
    : IFeatureHandler<AvatarInfo?>
{
    public Task<AvatarInfo?> HandleAsync(JsonElement arguments, CancellationToken token) =>
        context.Avatars.SetAsync(
            JsonSerializer.SerializeToElement(new
            {
                path = ReadString(arguments, "path")
            }));
}
