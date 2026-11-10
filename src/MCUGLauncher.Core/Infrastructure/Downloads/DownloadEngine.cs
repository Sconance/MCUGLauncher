using System.Net;

namespace BlockLaunch.Downloads;

/// <summary>
/// 批量下载引擎：文件级并发、候选源排序、分片与降级。
/// </summary>
/// <remarks>
/// 复用 <see cref="ReliableDownloader.PartialPath"/> 的临时文件命名、<see cref="AtomicFile"/> 的元数据写入和
/// <see cref="OperationExecution.Ambient"/> 的流量计量；不新建 HttpClient，也不绕过现有代理与限速。
/// 降级顺序：分片失败 → 同源单流重试 → 下一个候选源 → 明确错误。
/// </remarks>
public sealed class DownloadEngine
{
    readonly HttpClient http;
    readonly Action<string>? log;
    readonly SourceSelector selector;
    readonly DownloadMeter meter = new();

    /// <summary>
    /// 创建引擎；配置中的非法值会回退为默认值。
    /// </summary>
    public DownloadEngine(HttpClient http, DownloadOptions? options = null, Action<string>? log = null)
    {
        this.http = http ?? throw new ArgumentNullException(nameof(http));
        Options = (options ?? new DownloadOptions()).Normalize();
        this.log = log;
        selector = new(http, Options, log);
    }

    /// <summary>
    /// 获取生效的配置。
    /// </summary>
    public DownloadOptions Options
    {
        get;
    }

    /// <summary>
    /// 并发下载一组任务：按优先级出队，受 <see cref="DownloadOptions.MaxFileConcurrency"/> 限制同时活跃的文件数。
    /// </summary>
    public async Task DownloadAllAsync(IReadOnlyList<DownloadTask> tasks, IProgress<DownloadProgress>? progress = null, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        if (tasks.Count == 0)
            return;
        var ordered = tasks.OrderByDescending(task => task.Priority).ToArray();
        var total = ordered.Length;
        var completed = 0;
        meter.Reset();
        using var gate = new SemaphoreSlim(Options.MaxFileConcurrency);
        await Task.WhenAll(ordered.Select(async task =>
        {
            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await DownloadAsync(task, progress, Volatile.Read(ref completed), total, token).ConfigureAwait(false);
                Interlocked.Increment(ref completed);
            }
            finally
            {
                gate.Release();
            }
        })).ConfigureAwait(false);
    }

    /// <summary>
    /// 下载单个任务：先复用已校验文件，再按候选源顺序尝试，期间按配置降级为单流。
    /// </summary>
    public async Task DownloadAsync(DownloadTask task, IProgress<DownloadProgress>? progress = null, int completedFiles = 0, int totalFiles = 1, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (task.Sources.Count == 0)
            throw new IOException("下载任务缺少下载地址。");
        var target = Path.GetFullPath(task.TargetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        progress?.Report(Snapshot(task, DownloadPhase.Checking, 0, task.Size, completedFiles, totalFiles, task.Sources[0].Name));
        if (await FileChecker.MatchesAsync(target, task.Hash, task.Algorithm, task.Size, Options.MaxFileBytes, token).ConfigureAwait(false))
        {
            progress?.Report(Snapshot(task, DownloadPhase.Completed, task.Size, task.Size, completedFiles + 1, totalFiles, task.Sources[0].Name));
            return;
        }

        var sources = await selector.RankAsync(task.Sources, token, task.Id, task.Size >= Options.SmallFileThreshold).ConfigureAwait(false);
        var lastBytes = 0L;
        void Advance(long done)
        {
            var previous = Interlocked.Read(ref lastBytes);
            if (done < previous)
            {
                Interlocked.Exchange(ref lastBytes, done);
                return;
            }

            if (done > previous)
            {
                meter.Add(done - previous);
                Interlocked.Exchange(ref lastBytes, done);
            }
        }

        try
        {
            await TransferAsync(task, target, sources, progress, completedFiles, totalFiles, Advance, token).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw DownloadErrors.Translate(ex);
        }
        catch (IOException ex) when (DownloadErrors.IsPermanentFileError(ex))
        {
            throw DownloadErrors.Translate(ex);
        }
        catch (HttpRequestException ex)
        {
            var detail = ex.StatusCode is { } status ? DownloadErrors.StatusMessage(status) : ex.Message;
            throw new IOException($"下载 {Describe(task)} 失败：{detail}", ex);
        }
    }

    static string Describe(DownloadTask task)
    {
        var name = Path.GetFileName(task.TargetPath);
        return string.IsNullOrEmpty(name) ? task.Id : name;
    }

    async Task TransferAsync(DownloadTask task, string target, IReadOnlyList<DownloadSource> sources, IProgress<DownloadProgress>? progress, int completedFiles, int totalFiles, Action<long> advance, CancellationToken token)
    {
        Exception? last = null;
        for (var round = 0; round <= Options.MaxRetries; round++)
        {
            var hashFailure = false;
            for (var index = 0; index < sources.Count; index++)
            {
                var source = sources[index];
                try
                {
                    await DownloadOnceAsync(task, source, target, progress, completedFiles, totalFiles, round, advance, token).ConfigureAwait(false);
                    return;
                }
                catch (HashMismatchException ex)
                {
                    last = ex;
                    hashFailure = true;
                    Log($"id={task.Id} kind={task.Kind} src={source.Name} round={round + 1} hash=失败 清理后重下");
                    progress?.Report(Snapshot(task, DownloadPhase.Retrying, 0, task.Size, completedFiles, totalFiles, source.Name, round + 1));
                    break;
                }
                catch (HttpRequestException ex) when (!token.IsCancellationRequested && source.IsMirror && ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
                {
                    // 镜像并不保证覆盖全部文件；缺文件时不要重试同一个镜像，直接换下一个源。
                    last = ex;
                    Log($"id={task.Id} kind={task.Kind} src={source.Name} round={round + 1} reason=镜像缺少该文件({(int)ex.StatusCode}) 尝试下一个源");
                    progress?.Report(Snapshot(task, DownloadPhase.Retrying, 0, task.Size, completedFiles, totalFiles, source.Name, round + 1));
                }
                catch (Exception ex) when (!token.IsCancellationRequested && DownloadErrors.IsTransient(ex))
                {
                    last = ex;
                    Log($"id={task.Id} kind={task.Kind} src={source.Name} round={round + 1} reason={ex.Message} 尝试下一个源");
                    progress?.Report(Snapshot(task, DownloadPhase.Retrying, 0, task.Size, completedFiles, totalFiles, source.Name, round + 1));
                }
            }

            if (!hashFailure)
                break;
            await Task.Delay(DownloadBackoff.Exponential(round), token).ConfigureAwait(false);
        }

        if (last is HashMismatchException)
            throw new IOException("文件完整性校验连续失败，请更换下载源或稍后重试。", last);
        throw last ?? new IOException("下载失败。");
    }

    async Task DownloadOnceAsync(DownloadTask task, DownloadSource source, string target, IProgress<DownloadProgress>? progress, int completedFiles, int totalFiles, int round, Action<long> advance, CancellationToken token)
    {
        var size = task.Size;
        var canSplit = Options.EnableFastDownload && task.AllowSplitting;
        if (size <= 0)
        {
            // 大小未知时先探测一次：既用于分片，也让单流下载可以按大小校验与显示进度。
            var probe = await RangeProbe.CheckAsync(http, source.Uri, Options.RequestTimeout, token).ConfigureAwait(false);
            if (probe.Size > 0)
            {
                size = probe.Size;
                Log($"id={task.Id} kind={task.Kind} src={source.Name} 探测到大小 {size}");
            }

            if (!probe.Supported || probe.Size <= 0)
                canSplit = false;
        }

        if (canSplit && size >= Options.ChunkSizeThreshold)
        {
            try
            {
                var chunked = new ChunkedDownload(
                    http,
                    source.Uri,
                    target,
                    task.Hash ?? "",
                    task.Algorithm,
                    size,
                    Options,
                    source.Name,
                    (done, total) =>
                    {
                        advance(done);
                        progress?.Report(Snapshot(task, DownloadPhase.Downloading, done, total, completedFiles, totalFiles, source.Name, round));
                    },
                    detail => Log($"id={task.Id} kind={task.Kind} src={source.Name} {detail}"));
                await chunked.RunAsync(token).ConfigureAwait(false);
                progress?.Report(Snapshot(task, DownloadPhase.Completed, size, size, completedFiles + 1, totalFiles, source.Name, round));
                return;
            }
            catch (RangeNotSupportedException ex)
            {
                Log($"id={task.Id} kind={task.Kind} src={source.Name} 不支持分片，降级单流 reason={ex.Message}");
                ChunkedPaths.Delete(target, task.Hash ?? "");
            }
            catch (Exception ex) when (ex is not HashMismatchException && !token.IsCancellationRequested && DownloadErrors.IsTransient(ex))
            {
                Log($"id={task.Id} kind={task.Kind} src={source.Name} 分片失败，降级单流 reason={ex.Message}");
            }
        }

        var policy = new SingleStreamPolicy(
            Options.MaxFileBytes,
            Options.MaxRetries + 1,
            Options.RequestTimeout > 0 ? TimeSpan.FromSeconds(Options.RequestTimeout) : Timeout.InfiniteTimeSpan,
            DownloadBackoff.Exponential,
            Options.AllowInsecureTransport,
            AllowMissingHash: true);
        var download = new SingleStreamDownload(
            http,
            source.Uri,
            target,
            task.Hash ?? "",
            task.Algorithm,
            size > 0 ? size : null,
            policy,
            detail => LogRetry(task, source, detail),
            (done, total) =>
            {
                advance(done);
                progress?.Report(Snapshot(task, DownloadPhase.Downloading, done, total, completedFiles, totalFiles, source.Name, round));
            });
        await download.RunAsync(token).ConfigureAwait(false);
        ChunkedPaths.Delete(target, task.Hash ?? "");
        progress?.Report(Snapshot(task, DownloadPhase.Completed, size > 0 ? size : task.Size, size > 0 ? size : task.Size, completedFiles + 1, totalFiles, source.Name, round));
    }

    void LogRetry(DownloadTask task, DownloadSource source, string detail)
    {
        if (detail.Contains("重试", StringComparison.Ordinal) || detail.Contains("超时", StringComparison.Ordinal))
            Log($"id={task.Id} kind={task.Kind} src={source.Name} {detail}");
    }

    void Log(string message) => log?.Invoke("[download] " + message);

    DownloadProgress Snapshot(DownloadTask task, DownloadPhase phase, long done, long total, int completedFiles, int totalFiles, string source, int retry = 0)
    {
        var speed = meter.Speed;
        var remaining = speed > 0 && total > done ? TimeSpan.FromSeconds((total - done) / speed) : (TimeSpan?)null;
        return new(task.Id, task.Kind, task.TargetPath, phase, done, total, speed, remaining, completedFiles, totalFiles, source, retry);
    }
}
