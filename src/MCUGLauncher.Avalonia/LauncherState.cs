using System.Text.Json;

namespace MCUGLauncher.Avalonia;

internal sealed record InstanceInfo(
    string Name,
    string Loader,
    int Mods,
    int Updates,
    string DisplayName,
    string Minecraft)
{
    public static InstanceInfo From(JsonElement element) => new(
        JsonRead.String(element, "name"),
        JsonRead.String(element, "loader"),
        JsonRead.Int(element, "mods"),
        JsonRead.Int(element, "updates"),
        JsonRead.String(element, "displayName") is { Length: > 0 } display ? display : JsonRead.String(element, "name"),
        JsonRead.String(element, "minecraft"));
}

internal sealed record ModInfo(
    string Id,
    string Name,
    string File,
    string Version,
    bool Enabled,
    string Path,
    long Size)
{
    public static ModInfo From(JsonElement element) => new(
        JsonRead.String(element, "id"),
        JsonRead.String(element, "name"),
        JsonRead.String(element, "file"),
        JsonRead.String(element, "v"),
        JsonRead.Bool(element, "enabled"),
        JsonRead.String(element, "path"),
        JsonRead.Long(element, "size"));
}

internal sealed record ContentItem(
    string Id,
    string Name,
    string Description,
    string Author,
    string Icon,
    string Downloads,
    IReadOnlyList<string> Tags)
{
    public static ContentItem From(JsonElement element) => new(
        JsonRead.String(element, "id"),
        JsonRead.String(element, "name"),
        JsonRead.String(element, "description"),
        JsonRead.String(element, "author"),
        JsonRead.String(element, "icon"),
        JsonRead.String(element, "downloads"),
        JsonRead.Strings(element, "tags"));
}

internal sealed record TaskInfo(
    string Id,
    string Name,
    string State,
    double Percent,
    string Message,
    bool Committing)
{
    public static TaskInfo From(JsonElement element) => new(
        JsonRead.String(element, "id"),
        JsonRead.String(element, "name"),
        JsonRead.String(element, "state"),
        element.TryGetProperty("percent", out var percent) && percent.TryGetDouble(out var value) ? value : 0,
        JsonRead.String(element, "message"),
        JsonRead.Bool(element, "committing"));
}

internal sealed record SavedServerInfo(string Name, string Address)
{
    public static SavedServerInfo From(JsonElement element) => new(
        JsonRead.String(element, "name"),
        JsonRead.String(element, "address"));
}

internal sealed record FavoriteInfo(string Id, string Kind, string Name, string Description)
{
    public static FavoriteInfo From(JsonElement element) => new(
        JsonRead.String(element, "id"),
        JsonRead.String(element, "kind"),
        JsonRead.String(element, "name"),
        JsonRead.String(element, "description"));
}

internal sealed record DataSourceSet(string Announcements, string Servers, string Recommendations, string Quotes)
{
    public static DataSourceSet Empty { get; } = new("", "", "", "");

    public static DataSourceSet From(JsonElement element) => new(
        JsonRead.String(element, "announcements"),
        JsonRead.String(element, "servers"),
        JsonRead.String(element, "recommendations"),
        JsonRead.String(element, "quotes"));
}

internal sealed record VersionChoice(string Name, string Meta);

internal sealed record VersionGroupInfo(string Label, int Count, IReadOnlyList<VersionChoice> Versions)
{
    public static VersionGroupInfo From(JsonElement element)
    {
        var versions = new List<VersionChoice>();
        if (element.TryGetProperty("versions", out var items) && items.ValueKind == JsonValueKind.Array)
            foreach (var item in items.EnumerateArray())
                if (item.ValueKind == JsonValueKind.Array)
                {
                    var pair = item.EnumerateArray().ToArray();
                    versions.Add(new VersionChoice(
                        pair.Length > 0 ? pair[0].GetString() ?? "" : "",
                        pair.Length > 1 ? pair[1].GetString() ?? "" : ""));
                }
        return new VersionGroupInfo(
            JsonRead.String(element, "label"),
            JsonRead.Int(element, "count"),
            versions);
    }
}

internal sealed record LauncherState(
    string AppVersion,
    string Player,
    string AccountType,
    string AccountLabel,
    string Root,
    IReadOnlyList<string> GameRoots,
    string Java,
    int Width,
    int Height,
    int Ram,
    bool AutoMemory,
    bool Anonymous,
    bool HasCommunityKey,
    bool HasAdminKey,
    string SkinStation,
    string Cape,
    string SelectedVersion,
    string SelectedInstance,
    IReadOnlyList<InstanceInfo> Instances,
    IReadOnlyList<ModInfo> Mods,
    IReadOnlyList<SavedServerInfo> Servers,
    IReadOnlyList<FavoriteInfo> Favorites,
    IReadOnlyList<VersionGroupInfo> VersionGroups,
    DataSourceSet DataSources,
    DataSourceSet DefaultDataSources,
    DataSourceSet CustomDataSources,
    string DataSourceMode,
    string WriteEndpoint)
{
    public static LauncherState From(JsonElement element)
    {
        var gameRoots = JsonRead.Strings(element, "gameRoots");
        var root = JsonRead.String(element, "root");
        return new LauncherState(
            JsonRead.String(element, "appVersion"),
            JsonRead.String(element, "player"),
            JsonRead.String(element, "accountType"),
            JsonRead.String(element, "accountLabel"),
            root,
            gameRoots.Count > 0 ? gameRoots : [root],
            JsonRead.String(element, "java"),
            JsonRead.Int(element, "width", 1280),
            JsonRead.Int(element, "height", 720),
            JsonRead.Int(element, "ram", 4096),
            JsonRead.Bool(element, "autoMemory"),
            JsonRead.Bool(element, "anonymous"),
            JsonRead.Bool(element, "communityKey"),
            JsonRead.Bool(element, "adminKey"),
            JsonRead.String(element, "skinStation"),
            JsonRead.String(element, "cape"),
            JsonRead.String(element, "selectedVersion"),
            JsonRead.String(element, "selectedInstance"),
            JsonRead.Items(element, "instances", InstanceInfo.From),
            JsonRead.Items(element, "mods", ModInfo.From),
            JsonRead.Items(element, "servers", SavedServerInfo.From),
            JsonRead.Items(element, "favorites", FavoriteInfo.From),
            JsonRead.Items(element, "versionGroups", VersionGroupInfo.From),
            ReadSources(element, "dataSources"),
            ReadSources(element, "defaultDataSources"),
            ReadSources(element, "customDataSources"),
            JsonRead.String(element, "dataSourceMode"),
            JsonRead.String(element, "writeEndpoint"));
    }

    static DataSourceSet ReadSources(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object
            ? DataSourceSet.From(value)
            : DataSourceSet.Empty;
}

internal static class JsonRead
{
    public static string String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    public static bool Bool(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.True;

    public static int Int(JsonElement element, string name, int fallback = 0) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var number)
            ? number
            : fallback;

    public static long Long(JsonElement element, string name, long fallback = 0) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out var number)
            ? number
            : fallback;

    public static IReadOnlyList<string> Strings(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var items)
            || items.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();
        return items.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? "")
            .ToArray();
    }

    public static IReadOnlyList<T> Items<T>(JsonElement element, string name, Func<JsonElement, T> map)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var items)
            || items.ValueKind != JsonValueKind.Array)
            return Array.Empty<T>();
        return items.EnumerateArray().Select(map).ToArray();
    }
}
