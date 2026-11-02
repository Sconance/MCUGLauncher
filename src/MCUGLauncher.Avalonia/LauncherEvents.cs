using System.Text.Json;

namespace MCUGLauncher.Avalonia;

internal sealed record JavaInfo(string Path, int Major, string Arch, string Vendor, string Error)
{
    public static JavaInfo From(JsonElement element) => new(
        JsonRead.String(element, "path"),
        JsonRead.Int(element, "major"),
        JsonRead.String(element, "arch"),
        JsonRead.String(element, "vendor"),
        JsonRead.String(element, "error"));
}

internal sealed record GameInfo(string Id, string Version, string Player, int Pid)
{
    public static GameInfo From(JsonElement element) => new(
        JsonRead.String(element, "id"),
        JsonRead.String(element, "version"),
        JsonRead.String(element, "player"),
        JsonRead.Int(element, "pid"));
}

internal sealed record AuthResultInfo(bool? Ok, bool Pending, string Message)
{
    public static AuthResultInfo From(JsonElement element) => new(
        element.TryGetProperty("ok", out var ok) && ok.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? ok.GetBoolean()
            : null,
        JsonRead.Bool(element, "pending"),
        JsonRead.String(element, "message"));
}

internal sealed record AccountSwitchResultInfo(string Account, bool NeedsLogin, string Message)
{
    public static AccountSwitchResultInfo From(JsonElement element) => new(
        JsonRead.String(element, "account"),
        JsonRead.Bool(element, "needsLogin"),
        JsonRead.String(element, "message"));
}

internal sealed record AccountStateInfo(string Player, string AccountType, string AccountLabel)
{
    public static AccountStateInfo From(JsonElement element) => new(
        JsonRead.String(element, "player"),
        JsonRead.String(element, "accountType"),
        JsonRead.String(element, "accountLabel"));
}

internal sealed record SaveBackupInfo(string Id, string Created, IReadOnlyList<string> Worlds)
{
    public static SaveBackupInfo From(JsonElement element) => new(
        JsonRead.String(element, "id"),
        JsonRead.String(element, "created"),
        JsonRead.Strings(element, "worlds"));
}

internal sealed record SystemInfoInfo(
    string App,
    string Dotnet,
    string Webview,
    string Os,
    string Root,
    string Data)
{
    public static SystemInfoInfo From(JsonElement element) => new(
        JsonRead.String(element, "app"),
        JsonRead.String(element, "dotnet"),
        JsonRead.String(element, "webview"),
        JsonRead.String(element, "os"),
        JsonRead.String(element, "root"),
        JsonRead.String(element, "data"));
}

internal sealed record ToastInfo(string Message, bool Bad)
{
    public static ToastInfo From(JsonElement element) => new(
        JsonRead.String(element, "message"),
        JsonRead.Bool(element, "bad"));
}

internal sealed record LaunchResultInfo(bool Ok, string Message)
{
    public static LaunchResultInfo From(JsonElement element) => new(
        JsonRead.Bool(element, "ok"),
        JsonRead.String(element, "message"));
}

internal sealed record UninstallPreviewInfo(
    string Token,
    string Version,
    string Name,
    string Notice,
    IReadOnlyList<string> Paths,
    bool Shared)
{
    public static UninstallPreviewInfo From(JsonElement element) => new(
        JsonRead.String(element, "token"),
        JsonRead.String(element, "version"),
        JsonRead.String(element, "name"),
        JsonRead.String(element, "notice"),
        JsonRead.Strings(element, "paths"),
        JsonRead.Bool(element, "shared"));
}

internal sealed record ContentUpdateInfo(
    string Id,
    string Name,
    string Before,
    string After,
    bool Available,
    string Error)
{
    public static ContentUpdateInfo From(JsonElement element) => new(
        JsonRead.String(element, "id"),
        JsonRead.String(element, "name"),
        JsonRead.String(element, "before"),
        JsonRead.String(element, "after"),
        JsonRead.Bool(element, "available"),
        JsonRead.String(element, "error"));
}

internal sealed record ContentUpdatesInfo(
    string Token,
    string Version,
    IReadOnlyList<ContentUpdateInfo> Items)
{
    public static ContentUpdatesInfo From(JsonElement element) => new(
        JsonRead.String(element, "token"),
        JsonRead.String(element, "version"),
        JsonRead.Items(element, "items", ContentUpdateInfo.From));
}

internal sealed record ContentChangeInfo(string Project, string Before, string After, string Path, string OldPath)
{
    public static ContentChangeInfo From(JsonElement element) => new(
        JsonRead.String(element, "project"),
        JsonRead.String(element, "before"),
        JsonRead.String(element, "after"),
        JsonRead.String(element, "path"),
        JsonRead.String(element, "oldPath"));
}

internal sealed record ContentUpdatePreviewInfo(
    string Token,
    string Version,
    long Bytes,
    IReadOnlyList<ContentChangeInfo> Changes)
{
    public static ContentUpdatePreviewInfo From(JsonElement element) => new(
        JsonRead.String(element, "token"),
        JsonRead.String(element, "version"),
        JsonRead.Long(element, "bytes"),
        JsonRead.Items(element, "changes", ContentChangeInfo.From));
}

internal sealed record ServerPingInfo(
    string Id,
    bool Online,
    int Players,
    int Max,
    int Ping,
    string Motd,
    string Version,
    string Error)
{
    public static ServerPingInfo From(JsonElement element) => new(
        JsonRead.String(element, "id"),
        JsonRead.Bool(element, "online"),
        JsonRead.Int(element, "players"),
        JsonRead.Int(element, "max"),
        JsonRead.Int(element, "ping"),
        JsonRead.String(element, "motd"),
        JsonRead.String(element, "version"),
        JsonRead.String(element, "error"));
}

internal sealed record ServerResultInfo(bool Ok, string Message)
{
    public static ServerResultInfo From(JsonElement element) => new(
        JsonRead.Bool(element, "ok"),
        JsonRead.String(element, "message"));
}

internal sealed record TestResultInfo(
    string RequestId,
    string Name,
    string Url,
    bool Ok,
    string Message,
    int Ms)
{
    public static TestResultInfo From(JsonElement element) => new(
        JsonRead.String(element, "requestId"),
        JsonRead.String(element, "name"),
        JsonRead.String(element, "url"),
        JsonRead.Bool(element, "ok"),
        JsonRead.String(element, "message"),
        JsonRead.Int(element, "ms"));
}

internal sealed record KeyResultInfo(string Role, bool Ok, string Message)
{
    public static KeyResultInfo From(JsonElement element) => new(
        JsonRead.String(element, "role"),
        JsonRead.Bool(element, "ok"),
        JsonRead.String(element, "message"));
}

internal sealed record ResourceVersionInfo(string Id, string Name, IReadOnlyList<string> Files)
{
    public static ResourceVersionInfo From(JsonElement element) => new(
        JsonRead.String(element, "id"),
        JsonRead.String(element, "name"),
        JsonRead.Strings(element, "files"));
}

internal sealed record ResourceVersionsInfo(
    string RequestId,
    string Token,
    string Game,
    string Loader,
    IReadOnlyList<ResourceVersionInfo> Items)
{
    public static ResourceVersionsInfo From(JsonElement element) => new(
        JsonRead.String(element, "requestId"),
        JsonRead.String(element, "token"),
        JsonRead.String(element, "game"),
        JsonRead.String(element, "loader"),
        JsonRead.Items(element, "items", ResourceVersionInfo.From));
}

internal sealed record LoaderListInfo(string Version, string Loader, IReadOnlyList<string> Versions)
{
    public static LoaderListInfo From(JsonElement element) => new(
        JsonRead.String(element, "version"),
        JsonRead.String(element, "loader"),
        JsonRead.Strings(element, "versions"));
}

internal sealed record VanillaVersionInfo(string Id, string Type, string Released)
{
    public static VanillaVersionInfo From(JsonElement element) => new(
        JsonRead.String(element, "id"),
        JsonRead.String(element, "type"),
        JsonRead.String(element, "released"));
}

internal sealed record PackPreviewInfo(string Token, string Name, string Root, string Game, string Loader)
{
    public static PackPreviewInfo From(JsonElement element) => new(
        JsonRead.String(element, "token"),
        JsonRead.String(element, "name"),
        JsonRead.String(element, "root"),
        JsonRead.String(element, "game"),
        JsonRead.String(element, "loader"));
}

internal sealed record AvatarInfo(string Image, string Label, bool Fallback, string Account, string Player)
{
    public static AvatarInfo From(JsonElement element) => new(
        JsonRead.String(element, "image"),
        JsonRead.String(element, "label"),
        JsonRead.Bool(element, "fallback"),
        JsonRead.String(element, "account"),
        JsonRead.String(element, "player"));
}

internal sealed record MuaProfilesInfo(string Token, IReadOnlyList<MuaProfileInfo> Items)
{
    public static MuaProfilesInfo From(JsonElement element) => new(
        JsonRead.String(element, "token"),
        JsonRead.Items(element, "items", MuaProfileInfo.From));
}

internal sealed record MuaProfileInfo(string Id, string Name)
{
    public static MuaProfileInfo From(JsonElement element) => new(
        JsonRead.String(element, "id"),
        JsonRead.String(element, "name"));
}

internal sealed record ContentErrorInfo(string Category, string RequestId, string Message)
{
    public static ContentErrorInfo From(JsonElement element) => new(
        JsonRead.String(element, "category"),
        JsonRead.String(element, "requestId"),
        JsonRead.String(element, "message"));
}

internal sealed record PickedPathInfo(string Target, string Path)
{
    public static PickedPathInfo From(JsonElement element) => new(
        JsonRead.String(element, "target"),
        JsonRead.String(element, "path"));
}

internal sealed record LaunchSettingsInfo(
    bool AutoMemory,
    bool FullScreen,
    int RamMb,
    int Width,
    int Height,
    string Java,
    string JvmArguments,
    string WindowTitle,
    string Server)
{
    public static LaunchSettingsInfo From(JsonElement element) => new(
        element.ValueKind == JsonValueKind.Object ? JsonRead.Bool(element, "autoMemory") : true,
        element.ValueKind == JsonValueKind.Object && JsonRead.Bool(element, "fullScreen"),
        element.ValueKind == JsonValueKind.Object ? JsonRead.Int(element, "ramMb", 4096) : 4096,
        element.ValueKind == JsonValueKind.Object ? JsonRead.Int(element, "width", 1280) : 1280,
        element.ValueKind == JsonValueKind.Object ? JsonRead.Int(element, "height", 720) : 720,
        JsonRead.String(element, "java"),
        JsonRead.String(element, "jvmArguments"),
        JsonRead.String(element, "windowTitle"),
        JsonRead.String(element, "server"));
}

internal sealed record LaunchOptionsInfo(
    string RequestId,
    string Token,
    string Version,
    string Name,
    bool Inherited,
    LaunchSettingsInfo Settings)
{
    public static LaunchOptionsInfo From(JsonElement element) => new(
        JsonRead.String(element, "requestId"),
        JsonRead.String(element, "token"),
        JsonRead.String(element, "version"),
        JsonRead.String(element, "name"),
        JsonRead.Bool(element, "inherited"),
        element.TryGetProperty("settings", out var settings)
            ? LaunchSettingsInfo.From(settings)
            : LaunchSettingsInfo.From(default));
}

internal sealed record CommunityPayload(IReadOnlyDictionary<string, string> Payload)
{
    public static CommunityPayload From(JsonElement element)
    {
        var payload = new Dictionary<string, string>(StringComparer.Ordinal);
        if (element.TryGetProperty("payload", out var value) && value.ValueKind == JsonValueKind.Object)
            foreach (var item in value.EnumerateObject())
                if (item.Value.ValueKind == JsonValueKind.String)
                    payload[item.Name] = item.Value.GetString() ?? "";
        return new CommunityPayload(payload);
    }
}

internal sealed record AnnouncementInfo(
    string Id,
    string Title,
    string Body,
    string Tag,
    string Author,
    DateTimeOffset? PublishedAt,
    string ImageUrl)
{
    public static AnnouncementInfo From(JsonElement element)
    {
        var date = JsonRead.String(element, "publishedAt");
        if (date.Length == 0)
            date = JsonRead.String(element, "date");
        return new AnnouncementInfo(
            JsonRead.String(element, "id"),
            JsonRead.String(element, "title"),
            JsonRead.String(element, "body") is { Length: > 0 } body ? body : JsonRead.String(element, "text"),
            JsonRead.String(element, "tag") is { Length: > 0 } tag ? tag : "公告",
            JsonRead.String(element, "author"),
            DateTimeOffset.TryParse(date, out var published) ? published : null,
            JsonRead.String(element, "imageUrl") is { Length: > 0 } image ? image : JsonRead.String(element, "image"));
    }
}

internal sealed record RecommendationInfo(
    string Id,
    string Type,
    string Name,
    string Author,
    string Description,
    IReadOnlyList<string> Tags,
    string Version,
    string Size,
    string Url)
{
    public static RecommendationInfo From(JsonElement element)
    {
        var type = JsonRead.String(element, "type");
        return new RecommendationInfo(
            JsonRead.String(element, "id"),
            type is "server" or "material" or "shader" ? type : "server",
            JsonRead.String(element, "name"),
            JsonRead.String(element, "author"),
            JsonRead.String(element, "description") is { Length: > 0 } description
                ? description
                : JsonRead.String(element, "desc"),
            JsonRead.Strings(element, "tags"),
            JsonRead.String(element, "version"),
            JsonRead.String(element, "size"),
            JsonRead.String(element, "url") is { Length: > 0 } url ? url : JsonRead.String(element, "address"));
    }
}

internal sealed record QuoteInfo(
    string Id,
    string Type,
    string Text,
    string Author,
    string Source,
    string ImageUrl)
{
    public static QuoteInfo From(JsonElement element) => new(
        JsonRead.String(element, "id"),
        JsonRead.String(element, "type") is { Length: > 0 } type ? type : "text",
        JsonRead.String(element, "text"),
        JsonRead.String(element, "author"),
        JsonRead.String(element, "source"),
        JsonRead.String(element, "imageUrl"));
}

internal sealed record CommunityData(
    IReadOnlyList<AnnouncementInfo> Announcements,
    IReadOnlyList<RecommendationInfo> Recommendations,
    IReadOnlyList<QuoteInfo> Quotes)
{
    public static CommunityData Empty { get; } = new([], [], []);

    public static CommunityData From(CommunityPayload payload) => new(
        Parse(payload, "announcements", AnnouncementInfo.From),
        Parse(payload, "recommendations", RecommendationInfo.From),
        Parse(payload, "quotes", QuoteInfo.From));

    static IReadOnlyList<T> Parse<T>(CommunityPayload payload, string key, Func<JsonElement, T> map)
    {
        if (!payload.Payload.TryGetValue(key, out var json) || string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Array
                ? document.RootElement.EnumerateArray().Select(map).ToArray()
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
