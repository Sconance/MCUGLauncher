using System.Collections.Concurrent;
using System.Diagnostics;

namespace BlockLaunch.Downloads;

/// <summary>
/// 轻量源健康检查：优先 HEAD，服务器不允许 HEAD 时退化为 <c>Range: bytes=0-0</c> 探测。
/// </summary>
/// <remarks>
/// 结果按主机缓存 3 分钟。CmlLib 一次安装会对同一镜像主机发起大量文件检查，
/// 不缓存会把每个文件都放大成两次额外请求。
/// </remarks>
internal static class SourceHealth
{
    static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(3);
    static readonly ConcurrentDictionary<string, Entry> cache = new(StringComparer.OrdinalIgnoreCase);

    readonly record struct Entry(bool Ok, TimeSpan Latency, DateTimeOffset MeasuredAt);

    /// <summary>
    /// 清空健康缓存；仅测试与切换下载源后需要手动刷新时使用。
    /// </summary>
    public static void ClearCache() => cache.Clear();

    /// <summary>
    /// 检查候选源是否可达，并返回往返延迟。
    /// </summary>
    public static async Task<(bool Ok, TimeSpan Latency)> CheckAsync(HttpClient http, Uri uri, int timeoutSeconds, CancellationToken token)
    {
        if (cache.TryGetValue(uri.Authority, out var cached) && DateTimeOffset.UtcNow - cached.MeasuredAt <= CacheTtl)
            return (cached.Ok, cached.Latency);
        var result = await CheckCoreAsync(http, uri, timeoutSeconds, token).ConfigureAwait(false);
        if (!token.IsCancellationRequested)
        {
            if (cache.Count > 256)
                cache.Clear();
            cache[uri.Authority] = new Entry(result.Ok, result.Latency, DateTimeOffset.UtcNow);
        }

        return result;
    }

    static async Task<(bool Ok, TimeSpan Latency)> CheckCoreAsync(HttpClient http, Uri uri, int timeoutSeconds, CancellationToken token)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            using var headTimeout = Linked(token, timeoutSeconds);
            using var request = DownloadHttp.Head(uri);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, headTimeout.Token).ConfigureAwait(false);
            if (Alive(response.StatusCode))
                return (true, Stopwatch.GetElapsedTime(started));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
        }

        try
        {
            using var rangeTimeout = Linked(token, timeoutSeconds);
            using var request = DownloadHttp.Get(uri);
            request.Headers.Range = new(0, 0);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, rangeTimeout.Token).ConfigureAwait(false);
            return (Alive(response.StatusCode), Stopwatch.GetElapsedTime(started));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return (false, Stopwatch.GetElapsedTime(started));
        }
    }

    static bool Alive(System.Net.HttpStatusCode status) => (int)status is >= 200 and < 500 && status != System.Net.HttpStatusCode.NotFound && status != System.Net.HttpStatusCode.RequestTimeout && status != System.Net.HttpStatusCode.TooManyRequests;

    static CancellationTokenSource Linked(CancellationToken token, int seconds)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(token);
        source.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(seconds, 2, 30)));
        return source;
    }
}
