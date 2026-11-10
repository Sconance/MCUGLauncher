using System.Text.Json;

namespace MCUGLauncher.Contracts;

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

    // 后端会把没有进度的数值写成 null，而 TryGetDouble 遇到 null 会抛 InvalidOperationException，
    // 所以这里必须先判类型；启动器曾因任务列表里的 "percent": null 在 UI 线程崩掉。
    public static double Double(JsonElement element, string name, double fallback = 0) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number)
            ? number
            : fallback;

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
