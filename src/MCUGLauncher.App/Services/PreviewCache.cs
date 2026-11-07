
using System.Runtime.CompilerServices;

namespace MCUGLauncher.Services;

// Keeps confirmed requests opaque and bounded; clients cannot supply file deletion paths.
internal sealed class PreviewCache<T>
{
    // Feature handlers are created per operation, so confirmation tokens share state by application context.
    static readonly ConditionalWeakTable<object, PreviewCache<T>> ByContext = new();
    readonly Dictionary<string, (T Value, DateTime Expires)> entries = new();
    readonly object gate = new();

    public static PreviewCache<T> For(object owner) =>
        ByContext.GetValue(owner, static _ => new PreviewCache<T>());

    public string Add(T value)
    {
        lock (gate)
        {
            foreach (var key in entries.Where(p => p.Value.Expires <= DateTime.UtcNow).Select(p => p.Key).ToArray())
                entries.Remove(key);
            if (entries.Count >= 32)
                entries.Remove(entries.MinBy(p => p.Value.Expires).Key);
            var token = Guid.NewGuid().ToString("N");
            entries[token] = (value, DateTime.UtcNow.AddMinutes(10));
            return token;
        }
    }

    public T Get(string token, bool consume = false)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(token, out var entry) || entry.Expires <= DateTime.UtcNow)
                throw new InvalidOperationException("预览已过期，请重新生成。");
            if (consume)
                entries.Remove(token);
            return entry.Value;
        }
    }
}
