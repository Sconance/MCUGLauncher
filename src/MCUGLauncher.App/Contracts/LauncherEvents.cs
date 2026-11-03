using System.Text.Json;

namespace MCUGLauncher.Contracts;

public sealed record JavaInfo(string Path, int Major, string Arch, string Vendor, string Error)
{
    internal static JavaInfo From(JsonElement element) => new(
        JsonRead.String(element, "path"),
        JsonRead.Int(element, "major"),
        JsonRead.String(element, "arch"),
        JsonRead.String(element, "vendor"),
        JsonRead.String(element, "error"));
}

public sealed record GameInfo(string Id, string Version, string Player, int Pid)
{
    internal static GameInfo From(JsonElement element) => new(
        JsonRead.String(element, "id"),
        JsonRead.String(element, "version"),
        JsonRead.String(element, "player"),
        JsonRead.Int(element, "pid"));
}

public sealed record AuthResultInfo(bool? Ok, bool Pending, string Message)
{
    internal static AuthResultInfo From(JsonElement element) => new(
        element.TryGetProperty("ok", out var ok) && ok.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? ok.GetBoolean()
            : null,
        JsonRead.Bool(element, "pending"),
        JsonRead.String(element, "message"));
}

public sealed record AccountSwitchResultInfo(string Account, bool NeedsLogin, string Message)
{
    internal static AccountSwitchResultInfo From(JsonElement element) => new(
        JsonRead.String(element, "account"),
        JsonRead.Bool(element, "needsLogin"),
        JsonRead.String(element, "message"));
}

public sealed record AccountStateInfo(string Player, string AccountType)
{
    internal static AccountStateInfo From(JsonElement element) => new(
        JsonRead.String(element, "player"),
        JsonRead.String(element, "accountType"));
}

public sealed record SaveBackupInfo(string Id, string Created, IReadOnlyList<string> Worlds)
{
    internal static SaveBackupInfo From(JsonElement element) => new(
        JsonRead.String(element, "id"),
        JsonRead.String(element, "created"),
        JsonRead.Strings(element, "worlds"));
}

public sealed record SystemInfoInfo(
    string App,
    string Dotnet,
    string Webview,
    string Os,
    string Root,
    string Data)
{
    internal static SystemInfoInfo From(JsonElement element) => new(
        JsonRead.String(element, "app"),
        JsonRead.String(element, "dotnet"),
        JsonRead.String(element, "webview"),
        JsonRead.String(element, "os"),
        JsonRead.String(element, "root"),
        JsonRead.String(element, "data"));
}

public sealed record ToastInfo(string Message, bool Bad)
{
    internal static ToastInfo From(JsonElement element) => new(
        JsonRead.String(element, "message"),
        JsonRead.Bool(element, "bad"));
}

public sealed record SavedInfo(string Message)
{
    internal static SavedInfo From(JsonElement element) => new(JsonRead.String(element, "message"));
}

public sealed record LaunchResultInfo(bool Ok, string Message)
{
    internal static LaunchResultInfo From(JsonElement element) => new(
        JsonRead.Bool(element, "ok"),
        JsonRead.String(element, "message"));
}

public sealed record UninstallPreviewInfo(
    string Token,
    string Version,
    string Name,
    string Notice,
    IReadOnlyList<string> Paths,
    bool Shared)
{
    internal static UninstallPreviewInfo From(JsonElement element) => new(
        JsonRead.String(element, "token"),
        JsonRead.String(element, "version"),
        JsonRead.String(element, "name"),
        JsonRead.String(element, "notice"),
        JsonRead.Strings(element, "paths"),
        JsonRead.Bool(element, "shared"));
}

public sealed record ContentUpdateInfo(
    string Id,
    string Name,
    string Before,
    string After,
    bool Available,
    string Error)
{
    internal static ContentUpdateInfo From(JsonElement element) => new(
        JsonRead.String(element, "id"),
        JsonRead.String(element, "name"),
        JsonRead.String(element, "before"),
        JsonRead.String(element, "after"),
        JsonRead.Bool(element, "available"),
        JsonRead.String(element, "error"));
}

public sealed record ContentUpdatesInfo(
    string Token,
    string Version,
    IReadOnlyList<ContentUpdateInfo> Items)
{
    internal static ContentUpdatesInfo From(JsonElement element) => new(
        JsonRead.String(element, "token"),
        JsonRead.String(element, "version"),
        JsonRead.Items(element, "items", ContentUpdateInfo.From));
}

public sealed record ContentChangeInfo(string Project, string Before, string After, string Path, string OldPath)
{
    internal static ContentChangeInfo From(JsonElement element) => new(
        JsonRead.String(element, "project"),
        JsonRead.String(element, "before"),
        JsonRead.String(element, "after"),
        JsonRead.String(element, "path"),
        JsonRead.String(element, "oldPath"));
}

public sealed record ContentUpdatePreviewInfo(
    string Token,
    string Version,
    long Bytes,
    IReadOnlyList<ContentChangeInfo> Changes)
{
    internal static ContentUpdatePreviewInfo From(JsonElement element) => new(
        JsonRead.String(element, "token"),
        JsonRead.String(element, "version"),
        JsonRead.Long(element, "bytes"),
        JsonRead.Items(element, "changes", ContentChangeInfo.From));
}

public sealed record ServerPingInfo(
    string Id,
    bool Online,
    int Players,
    int Max,
    int Ping,
    string Motd,
    string Version,
    string Error)
{
    internal static ServerPingInfo From(JsonElement element) => new(
        JsonRead.String(element, "id"),
        JsonRead.Bool(element, "online"),
        JsonRead.Int(element, "players"),
        JsonRead.Int(element, "max"),
        JsonRead.Int(element, "ping"),
        JsonRead.String(element, "motd"),
        JsonRead.String(element, "version"),
        JsonRead.String(element, "error"));
}

public sealed record ServerResultInfo(bool Ok, string Message)
{
    internal static ServerResultInfo From(JsonElement element) => new(
        JsonRead.Bool(element, "ok"),
        JsonRead.String(element, "message"));
}

public sealed record TestResultInfo(
    string Name,
    string Url,
    bool Ok,
    string Message,
    int Ms);

public sealed record KeyResultInfo(string Role, bool Ok, string Message)
{
    internal static KeyResultInfo From(JsonElement element) => new(
        JsonRead.String(element, "role"),
        JsonRead.Bool(element, "ok"),
        JsonRead.String(element, "message"));
}

public sealed record ResourceVersionInfo(string Id, string Name, IReadOnlyList<string> Files)
{
    internal static ResourceVersionInfo From(JsonElement element) => new(
        JsonRead.String(element, "id"),
        JsonRead.String(element, "name"),
        JsonRead.Strings(element, "files"));
}

public sealed record ResourceVersionsInfo(
    string RequestId,
    string Token,
    string Game,
    string Loader,
    IReadOnlyList<ResourceVersionInfo> Items)
{
    internal static ResourceVersionsInfo From(JsonElement element) => new(
        JsonRead.String(element, "requestId"),
        JsonRead.String(element, "token"),
        JsonRead.String(element, "game"),
        JsonRead.String(element, "loader"),
        JsonRead.Items(element, "items", ResourceVersionInfo.From));
}

public sealed record ResourceVersionsResultInfo(bool Ok, ResourceVersionsInfo? Result, string Error)
{
    public static ResourceVersionsResultInfo Success(ResourceVersionsInfo result) => new(true, result, "");
    public static ResourceVersionsResultInfo Failure(string error) => new(false, null, error);
}

public sealed record LoaderListInfo(string Version, string Loader, IReadOnlyList<string> Versions)
{
    internal static LoaderListInfo From(JsonElement element) => new(
        JsonRead.String(element, "version"),
        JsonRead.String(element, "loader"),
        JsonRead.Strings(element, "versions"));
}

public sealed record LoaderListResultInfo(bool Ok, LoaderListInfo? Result, string Error)
{
    public static LoaderListResultInfo Success(LoaderListInfo result) => new(true, result, "");
    public static LoaderListResultInfo Failure(string error) => new(false, null, error);
}

public sealed record VanillaVersionInfo(string Id, string Type, string Released)
{
    internal static VanillaVersionInfo From(JsonElement element) => new(
        JsonRead.String(element, "id"),
        JsonRead.String(element, "type"),
        JsonRead.String(element, "released"));
}

public sealed record VanillaVersionsResultInfo(bool Ok, IReadOnlyList<VanillaVersionInfo> Items, string Error)
{
    public static VanillaVersionsResultInfo Success(IReadOnlyList<VanillaVersionInfo> items) => new(true, items, "");
    public static VanillaVersionsResultInfo Failure(string error) => new(false, Array.Empty<VanillaVersionInfo>(), error);
}

public sealed record PackPreviewInfo(string Token, string Name, string Root, string Game, string Loader)
{
    internal static PackPreviewInfo From(JsonElement element) => new(
        JsonRead.String(element, "token"),
        JsonRead.String(element, "name"),
        JsonRead.String(element, "root"),
        JsonRead.String(element, "game"),
        JsonRead.String(element, "loader"));
}

public sealed record PackPreviewResultInfo(bool Ok, PackPreviewInfo? Preview, string Error)
{
    public static PackPreviewResultInfo Success(PackPreviewInfo preview) => new(true, preview, "");
    public static PackPreviewResultInfo Failure(string error) => new(false, null, error);
}

public sealed record AvatarInfo(string Image, string Label, bool Fallback, string Account, string Player)
{
    internal static AvatarInfo From(JsonElement element) => new(
        JsonRead.String(element, "image"),
        JsonRead.String(element, "label"),
        JsonRead.Bool(element, "fallback"),
        JsonRead.String(element, "account"),
        JsonRead.String(element, "player"));
}

public sealed record MuaProfilesInfo(string Token, IReadOnlyList<MuaProfileInfo> Items)
{
    internal static MuaProfilesInfo From(JsonElement element) => new(
        JsonRead.String(element, "token"),
        JsonRead.Items(element, "items", MuaProfileInfo.From));
}

public sealed record MuaProfileInfo(string Id, string Name)
{
    internal static MuaProfileInfo From(JsonElement element) => new(
        JsonRead.String(element, "id"),
        JsonRead.String(element, "name"));
}

public sealed record ContentErrorInfo(string Category, string RequestId, string Message)
{
    internal static ContentErrorInfo From(JsonElement element) => new(
        JsonRead.String(element, "category"),
        JsonRead.String(element, "requestId"),
        JsonRead.String(element, "message"));
}

public sealed record PickedPathInfo(string Target, string Path)
{
    internal static PickedPathInfo From(JsonElement element) => new(
        JsonRead.String(element, "target"),
        JsonRead.String(element, "path"));
}

public sealed record LaunchSettingsInfo(
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
    internal static LaunchSettingsInfo From(JsonElement element) => new(
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

public sealed record LaunchOptionsInfo(
    string RequestId,
    string Token,
    string Version,
    string Name,
    bool Inherited,
    LaunchSettingsInfo Settings)
{
    internal static LaunchOptionsInfo From(JsonElement element) => new(
        JsonRead.String(element, "requestId"),
        JsonRead.String(element, "token"),
        JsonRead.String(element, "version"),
        JsonRead.String(element, "name"),
        JsonRead.Bool(element, "inherited"),
        element.TryGetProperty("settings", out var settings)
            ? LaunchSettingsInfo.From(settings)
            : LaunchSettingsInfo.From(default));
}

public sealed record CommunityPayload(IReadOnlyDictionary<string, string> Payload)
{
    internal static CommunityPayload From(JsonElement element)
    {
        var payload = new Dictionary<string, string>(StringComparer.Ordinal);
        if (element.TryGetProperty("payload", out var value) && value.ValueKind == JsonValueKind.Object)
            foreach (var item in value.EnumerateObject())
                if (item.Value.ValueKind == JsonValueKind.String)
                    payload[item.Name] = item.Value.GetString() ?? "";
        return new CommunityPayload(payload);
    }
}

public sealed record AnnouncementInfo(
    string Id,
    string Title,
    string Body,
    string Tag,
    string Author,
    DateTimeOffset? PublishedAt,
    string ImageUrl)
{
    public string Raw { get; init; } = "";

    internal static AnnouncementInfo From(JsonElement element)
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
            JsonRead.String(element, "imageUrl") is { Length: > 0 } image ? image : JsonRead.String(element, "image"))
        {
            Raw = element.GetRawText()
        };
    }
}

public sealed record RecommendationInfo(
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
    public string Raw { get; init; } = "";

    internal static RecommendationInfo From(JsonElement element)
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
            JsonRead.String(element, "url") is { Length: > 0 } url ? url : JsonRead.String(element, "address"))
        {
            Raw = element.GetRawText()
        };
    }
}

public sealed record QuoteInfo(
    string Id,
    string Type,
    string Text,
    string Author,
    string Source,
    string ImageUrl)
{
    internal static QuoteInfo From(JsonElement element) => new(
        JsonRead.String(element, "id"),
        JsonRead.String(element, "type") is { Length: > 0 } type ? type : "text",
        JsonRead.String(element, "text"),
        JsonRead.String(element, "author"),
        JsonRead.String(element, "source"),
        JsonRead.String(element, "imageUrl"));
}

public sealed record CommunityData(
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
