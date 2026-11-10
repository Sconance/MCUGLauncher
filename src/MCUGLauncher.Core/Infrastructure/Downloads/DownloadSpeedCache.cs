using System.Collections.Concurrent;

namespace BlockLaunch.Downloads;

/// <summary>
/// 测速结果缓存：按主机（含端口）记录带宽，默认 3 分钟内复用，避免同一站点的多个文件重复测速。
/// </summary>
public static class DownloadSpeedCache
{
    static readonly ConcurrentDictionary<string, Entry> entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 读取缓存带宽（字节/秒）；过期或不存在时返回 false。
    /// </summary>
    public static bool TryGet(Uri uri, TimeSpan ttl, out double bytesPerSecond)
    {
        if (entries.TryGetValue(uri.Authority, out var entry) && DateTimeOffset.UtcNow - entry.MeasuredAt <= ttl)
        {
            bytesPerSecond = entry.BytesPerSecond;
            return true;
        }

        bytesPerSecond = 0;
        return false;
    }

    /// <summary>
    /// 写入测速结果。
    /// </summary>
    public static void Set(Uri uri, double bytesPerSecond)
    {
        if (entries.Count > 256)
            entries.Clear();
        entries[uri.Authority] = new(DateTimeOffset.UtcNow, bytesPerSecond);
    }

    /// <summary>
    /// 清空缓存（测试用）。
    /// </summary>
    public static void Clear() => entries.Clear();

    readonly record struct Entry(DateTimeOffset MeasuredAt, double BytesPerSecond);
}
