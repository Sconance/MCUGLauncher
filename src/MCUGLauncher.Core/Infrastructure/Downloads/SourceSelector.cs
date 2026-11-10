using System.Diagnostics;

namespace BlockLaunch.Downloads;

/// <summary>
/// 多源选择器：健康检查剔除失效源，对延迟靠前的少量源做 256KB 实测，按带宽 90% / 延迟 10% 排序并缓存结果。
/// </summary>
public sealed class SourceSelector
{
    readonly HttpClient http;
    readonly DownloadOptions options;
    readonly Action<string>? log;

    /// <summary>
    /// 创建选择器。
    /// </summary>
    public SourceSelector(HttpClient http, DownloadOptions options, Action<string>? log = null)
    {
        this.http = http ?? throw new ArgumentNullException(nameof(http));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.log = log;
    }

    /// <summary>
    /// 返回按偏好与实际速度排序的候选源；偏好组排在前面，同组内再按健康与带宽排序。
    /// </summary>
    /// <param name="measure">是否允许对同组源做 256KB 测速；小文件传 false 可完全避免测速流量。</param>
    public async Task<IReadOnlyList<DownloadSource>> RankAsync(IReadOnlyList<DownloadSource> sources, CancellationToken token, string id = "", bool measure = true)
    {
        var ordered = Order(sources);
        if (ordered.Count < 2 || !options.EnableFastDownload)
            return ordered;
        var mirrorFirst = options.Preference == DownloadSourcePreference.Mirror;
        var preferredCount = ordered.Count(source => source.IsMirror == mirrorFirst);
        var ranked = new List<DownloadSource>();
        ranked.AddRange(await RankGroupAsync(ordered.Take(preferredCount), measure && preferredCount > 1, token).ConfigureAwait(false));
        ranked.AddRange(await RankGroupAsync(ordered.Skip(preferredCount), false, token).ConfigureAwait(false));
        if (id.Length > 0)
            log?.Invoke($"[download] id={id} 源排序：{string.Join(" > ", ranked.Select(source => source.Name))}");
        return ranked;
    }

    /// <summary>
    /// 按下载源偏好做稳定分组并去重，不发起任何网络请求。
    /// </summary>
    IReadOnlyList<DownloadSource> Order(IReadOnlyList<DownloadSource> sources)
    {
        var mirrorFirst = options.Preference == DownloadSourcePreference.Mirror;
        var preferred = new List<DownloadSource>();
        var fallback = new List<DownloadSource>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (!seen.Add(source.Uri.AbsoluteUri))
                continue;
            if (source.IsMirror == mirrorFirst)
                preferred.Add(source);
            else
                fallback.Add(source);
        }

        preferred.AddRange(fallback);
        return preferred;
    }

    async Task<IReadOnlyList<DownloadSource>> RankGroupAsync(IEnumerable<DownloadSource> group, bool measure, CancellationToken token)
    {
        var items = group.ToList();
        if (items.Count == 0)
            return items;
        var probes = await Task.WhenAll(items.Select(async source =>
        {
            var result = await SourceHealth.CheckAsync(http, source.Uri, options.RequestTimeout, token).ConfigureAwait(false);
            return (Source: source, result.Ok, result.Latency);
        })).ConfigureAwait(false);
        var healthy = probes.Where(probe => probe.Ok).ToList();
        var pool = (healthy.Count > 0 ? healthy : probes.ToList()).OrderBy(probe => probe.Latency).ToList();
        var result = new List<DownloadSource>();
        if (measure)
        {
            var ttl = TimeSpan.FromMinutes(options.SpeedCacheMinutes);
            var measured = new List<(DownloadSource Source, double Speed, double Latency)>();
            foreach (var probe in pool.Take(2))
                measured.Add((probe.Source, await MeasureAsync(probe.Source, ttl, token).ConfigureAwait(false), probe.Latency.TotalMilliseconds));
            var fastest = Math.Max(measured.Max(item => item.Speed), 1);
            var slowest = Math.Max(measured.Max(item => item.Latency), 1);
            result.AddRange(measured
                .Select(item => (item.Source, Score: 0.9 * (item.Speed / fastest) + 0.1 * (1 - item.Latency / slowest)))
                .OrderByDescending(item => item.Score)
                .Select(item => item.Source));
            result.AddRange(pool.Skip(measured.Count).Select(probe => probe.Source));
        }
        else
        {
            result.AddRange(pool.Select(probe => probe.Source));
        }

        foreach (var probe in probes.Where(probe => !probe.Ok))
            if (!result.Contains(probe.Source))
                result.Add(probe.Source);
        return result;
    }

    async Task<double> MeasureAsync(DownloadSource source, TimeSpan ttl, CancellationToken token)
    {
        if (DownloadSpeedCache.TryGet(source.Uri, ttl, out var cached))
            return cached;
        var started = Stopwatch.GetTimestamp();
        var total = 0L;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(6));
            using var request = DownloadHttp.Get(source.Uri);
            request.Headers.Range = new(0, Math.Max(0, options.SpeedTestBytes - 1));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return 0;
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            var buffer = new byte[16384];
            while (total < options.SpeedTestBytes)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, options.SpeedTestBytes - total)), timeout.Token).ConfigureAwait(false);
                if (read == 0)
                    break;
                total += read;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return 0;
        }

        var seconds = Stopwatch.GetElapsedTime(started).TotalSeconds;
        var speed = seconds <= 0 ? 0 : total / seconds;
        if (total > 0)
            DownloadSpeedCache.Set(source.Uri, speed);
        return speed;
    }
}
