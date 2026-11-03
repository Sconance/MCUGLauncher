

namespace MCUGLauncher.Services;

// Keeps confirmed requests opaque and bounded; clients cannot supply file deletion paths.
internal sealed class PreviewCache<T>
{
    readonly Dictionary<string, (T Value, DateTime Expires)> entries = new();

    public string Add(T value)
    {
        foreach (var key in entries.Where(p => p.Value.Expires <= DateTime.UtcNow).Select(p => p.Key).ToArray())
            entries.Remove(key);
        if (entries.Count >= 32)
            entries.Remove(entries.MinBy(p => p.Value.Expires).Key);
        var token = Guid.NewGuid().ToString("N");
        entries[token] = (value, DateTime.UtcNow.AddMinutes(10));
        return token;
    }

    public T Get(string token, bool consume = false)
    {
        if (!entries.TryGetValue(token, out var entry) || entry.Expires <= DateTime.UtcNow)
            throw new InvalidOperationException("预览已过期，请重新生成。");
        if (consume)
            entries.Remove(token);
        return entry.Value;
    }
}
