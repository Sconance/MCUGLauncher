using System.Text.Json;
using MCUGLauncher.Contracts;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

/// <summary>将类型化收藏请求交给既有社区用例，保留可选实例的空值语义。</summary>
/// <remarks>B 类：依赖 ProtocolValues.ReadString(JsonElement, string)、LauncherContext，
/// 以及 CommunityCommands.SaveServer(string, string, string, string?, string?)；不访问 UI 或 Core 启动实现。</remarks>
[FeatureHandler(nameof(ILauncherService.SaveServerAsync))]
internal sealed class SaveServerFeatureHandler(LauncherContext context)
    : IFeatureHandler<ServerResultInfo>
{
    public Task<ServerResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult(new CommunityCommands(context).SaveServer(
            ReadString(arguments, "name"),
            ReadString(arguments, "address"),
            ReadString(arguments, "oldAddress"),
            arguments.TryGetProperty("instance", out var instance) && instance.ValueKind != JsonValueKind.Null
                ? instance.GetString()
                : null,
            arguments.TryGetProperty("gameRoot", out var gameRoot) && gameRoot.ValueKind != JsonValueKind.Null
                ? gameRoot.GetString() : null));
}

[FeatureHandler(nameof(ILauncherService.PingServerAsync))]
internal sealed class PingServerFeatureHandler(LauncherContext context)
    : IFeatureHandler<ServerPingInfo>
{
    public Task<ServerPingInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new CommunityCommands(context).PingAsync(
            ReadString(arguments, "address"),
            ReadString(arguments, "id"));
}

[FeatureHandler(nameof(ILauncherService.VerifyKeyAsync))]
internal sealed class VerifyKeyFeatureHandler(LauncherContext context)
    : IFeatureHandler<KeyResultInfo>
{
    public Task<KeyResultInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult(new CommunityCommands(context).VerifyKey(
            ReadString(arguments, "role"),
            ReadString(arguments, "key")));
}

[FeatureHandler(nameof(ILauncherService.SaveRecommendationsAsync))]
internal sealed class SaveRecommendationsFeatureHandler(LauncherContext context)
    : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token)
    {
        var items = arguments.GetProperty("items").EnumerateArray()
            .Select(item => ParseRaw(item.GetString() ?? ""))
            .OfType<JsonElement>()
            .ToArray();
        return Task.FromResult(new CommunityCommands(context).SaveRecommendationsLocal(
            JsonSerializer.SerializeToElement(new
            {
                items
            })));
    }

    static JsonElement? ParseRaw(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        try
        {
            using var document = JsonDocument.Parse(raw);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

[FeatureHandler(nameof(ILauncherService.DeleteServerAsync))]
internal sealed class DeleteServerFeatureHandler(LauncherContext context)
    : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult(new CommunityCommands(context).DeleteServer(ReadString(arguments, "address")));
}

[FeatureHandler(nameof(ILauncherService.ToggleBookmarkAsync))]
internal sealed class ToggleBookmarkFeatureHandler(LauncherContext context)
    : IFeatureHandler<SavedInfo>
{
    public Task<SavedInfo> HandleAsync(JsonElement arguments, CancellationToken token) =>
        Task.FromResult(new BookmarkCommands(context).Toggle(arguments));
}

[FeatureHandler(nameof(ILauncherService.DownloadImageAsync))]
internal sealed class DownloadImageFeatureHandler(LauncherContext context)
    : IFeatureHandler<byte[]>
{
    public Task<byte[]> HandleAsync(JsonElement arguments, CancellationToken token) =>
        new CommunityCommands(context).DownloadImageAsync(ReadString(arguments, "url"));
}
