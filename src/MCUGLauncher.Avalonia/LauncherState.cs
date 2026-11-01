using System.Text.Json;

namespace MCUGLauncher.Avalonia;

internal sealed record InstanceInfo(string Name, string Loader);

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
