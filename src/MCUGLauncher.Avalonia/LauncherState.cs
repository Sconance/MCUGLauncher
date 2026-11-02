using System.Text.Json;

namespace MCUGLauncher.Avalonia;

internal sealed record InstanceInfo(string Name, string Loader);

internal sealed record ContentItem(string Id, string Name, string Description)
{
    public static ContentItem From(JsonElement element) => new(
        Read(element, "id"),
        Read(element, "name"),
        Read(element, "description"));

    static string Read(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
}

internal sealed record TaskInfo(string Id, string Name, string State, double Percent, string Message)
{
    public static TaskInfo From(JsonElement element) => new(
        Read(element, "id"),
        Read(element, "name"),
        Read(element, "state"),
        element.TryGetProperty("percent", out var percent) && percent.TryGetDouble(out var value) ? value : 0,
        Read(element, "message"));

    static string Read(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
}

internal sealed record LauncherState(
    string Player,
    string AccountType,
    string AccountLabel,
    string Root,
    string Java,
    int Ram,
    string SelectedVersion,
    IReadOnlyList<InstanceInfo> Instances)
{
    public static LauncherState From(JsonElement element)
    {
        var instances = new List<InstanceInfo>();
        if (element.TryGetProperty("instances", out var items) && items.ValueKind == JsonValueKind.Array)
            foreach (var item in items.EnumerateArray())
                instances.Add(new InstanceInfo(ReadString(item, "name"), ReadString(item, "loader")));
        return new LauncherState(
            ReadString(element, "player"),
            ReadString(element, "accountType"),
            ReadString(element, "accountLabel"),
            ReadString(element, "root"),
            ReadString(element, "java"),
            element.TryGetProperty("ram", out var ram) && ram.TryGetInt32(out var memory) ? memory : 4096,
            ReadString(element, "selectedVersion"),
            instances);
    }

    static string ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
}
