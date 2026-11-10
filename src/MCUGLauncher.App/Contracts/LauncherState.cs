using System.Text.Json;

namespace MCUGLauncher.Contracts;

public sealed record InstanceInfo(
    string Name,
    string Loader,
    int Mods,
    int Updates,
    string DisplayName,
    string Minecraft,
    bool IsPlaceholder = false)
{
}

public sealed record ModInfo(
    string Id,
    string Name,
    string File,
    string Version,
    bool Enabled,
    string Path,
    long Size)
{
}

public sealed record ContentItem(
    string Id,
    string Name,
    string Description,
    string Author,
    string Icon,
    string Downloads,
    IReadOnlyList<string> Tags)
{
    internal static ContentItem From(JsonElement element) => new(
        JsonRead.String(element, "id"),
        JsonRead.String(element, "name"),
        JsonRead.String(element, "description"),
        JsonRead.String(element, "author"),
        JsonRead.String(element, "icon"),
        JsonRead.String(element, "downloads"),
        JsonRead.Strings(element, "tags"));
}

public sealed record ContentSearchResultInfo(
    string Category,
    string RequestId,
    IReadOnlyList<ContentItem> Items,
    string Error)
{
    public bool Ok => Error.Length == 0;
}

public sealed record TaskInfo(
    string Id,
    string Name,
    string State,
    double Percent,
    string Message,
    bool Committing)
{
    internal static TaskInfo From(JsonElement element) => new(
        JsonRead.String(element, "id"),
        JsonRead.String(element, "name"),
        JsonRead.String(element, "state"),
        element.TryGetProperty("percent", out var percent) && percent.TryGetDouble(out var value) ? value : 0,
        JsonRead.String(element, "message"),
        JsonRead.Bool(element, "committing"));
}

public sealed record SavedServerInfo(string Name, string Address, string Instance = "")
{
}

public sealed record FavoriteInfo(string Id, string Kind, string Name, string Description)
{
}

public sealed record DataSourceSet(string Announcements, string Servers, string Recommendations, string Quotes)
{
    public static DataSourceSet Empty { get; } = new("", "", "", "");
}

public sealed record VersionChoice(string Name, string Meta);

public sealed record VersionGroupInfo(string Label, int Count, IReadOnlyList<VersionChoice> Versions)
{
}

public sealed record LauncherState(
    string AppVersion,
    string Player,
    string AccountType,
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
    bool AutoChinese = true)
{
}
