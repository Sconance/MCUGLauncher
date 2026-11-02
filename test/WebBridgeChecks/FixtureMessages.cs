using System.Text.Json;
using System.Text.Json.Nodes;
using MCUGLauncher.Contracts;

// Legacy command scenarios identify message kinds; production state now travels as a typed DTO.
internal static class FixtureMessages
{
    public static JsonElement Capture(object value)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var node = JsonSerializer.SerializeToNode(value, options)!;
        if (value is LauncherState)
            node["type"] = "state";
        return JsonSerializer.SerializeToElement(node);
    }
}
