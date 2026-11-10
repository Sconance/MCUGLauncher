using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace BlockLaunch.Downloads;

/// <summary>
/// 单文件分片并发下载器：预分配临时文件、按偏移并发写入、台账续传、哈希校验后原子提交。
/// </summary>
/// <remarks>
/// 首个分片串行请求，用它确认服务器真的支持 Range（返回 206 且 Content-Range 匹配）；服务器返回 200 时立即抛出
/// <see cref="RangeNotSupportedException"/> 让调用方降级，而不是把多个完整响应写进同一个文件。
/// 已完成分片记录在 .chunked.json 台账中，进程重启后只补缺失区间。
/// </remarks>
internal sealed class ChunkedDownload
{
    readonly HttpClient http;
    readonly Uri uri;
    readonly string targetPath;
    readonly string hash;
    readonly HashAlgorithmName algorithm;
    readonly long size;
    readonly DownloadOptions options;
    readonly string sourceName;
    readonly Action<long, long>? progress;
    readonly Action<string>? message;

    long[] downloaded = [];
    IReadOnlyList<ChunkRange> ranges = [];
    HashSet<int> completed = [];
    ChunkLedger? ledger;
    string dataPath = "";
    string ledgerPath = "";
    long lastReportTicks;

    public ChunkedDownload(HttpClient http, Uri uri, string targetPath, string hash, HashAlgorithmName algorithm, long size, DownloadOptions options, string sourceName, Action<long, long>? progress = null, Action<string>? message = null)
    {
        this.http = http;
        this.uri = uri;
        this.targetPath = Path.GetFullPath(targetPath);
        this.hash = hash;
        this.algorithm = algorithm;
        this.size = size;
        this.options = options;
        this.sourceName = sourceName;
        this.progress = progress;
        this.message = message;
    }

    /// <summary>
    /// 执行分片下载；取消时按配置决定保留还是删除断点文件。
    /// </summary>
    public async Task RunAsync(CancellationToken token)
    {
        try
        {
            await RunCoreAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!options.KeepPartialOnCancel)
        {
            ChunkedPaths.Delete(targetPath, hash);
            throw;
        }
    }

    async Task RunCoreAsync(CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        dataPath = ChunkedPaths.Data(targetPath, hash);
        ledgerPath = ChunkedPaths.Ledger(dataPath);
        // 与单流路径共用同一个 .lock，保证同一目标文件不会被两个下载流程同时改写。
        await using var lease = new FileStream(ReliableDownloader.PartialPath(targetPath, hash) + ".lock", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        ledger = ChunkLedger.Load(ledgerPath);
        if (ledger is null || !ledger.Matches(hash, size) || !File.Exists(dataPath))
        {
            DeleteQuietly(dataPath);
            ledger = new()
            {
                Hash = hash,
                Size = size,
                ChunkSize = ChunkPlan.ChooseChunkSize(size, options.MaxChunkConcurrency, options.ChunkSizeThreshold),
                Source = sourceName
            };
            ChunkLedger.Save(ledgerPath, ledger);
        }

        ledger.Source = sourceName;
        ranges = ChunkPlan.Build(size, ledger.ChunkSize);
        downloaded = new long[ranges.Count];
        completed = [.. ledger.Completed.Where(index => index >= 0 && index < ranges.Count)];
        foreach (var index in completed)
            downloaded[index] = ranges[index].Length;

        await using (var file = new FileStream(dataPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
        {
            if (file.Length != size)
                file.SetLength(size);
            var handle = file.SafeFileHandle;
            var pending = ranges.Where(range => !completed.Contains(range.Index)).ToList();
            if (pending.Count > 0)
            {
                await FetchWithRetryAsync(pending[0], handle, token).ConfigureAwait(false);
                pending.RemoveAt(0);
            }

            if (pending.Count > 0)
            {
                using var gate = new SemaphoreSlim(options.MaxChunkConcurrency);
                await Task.WhenAll(pending.Select(async range =>
                {
                    await gate.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        await FetchWithRetryAsync(range, handle, token).ConfigureAwait(false);
                    }
                    finally
                    {
                        gate.Release();
                    }
                })).ConfigureAwait(false);
            }

            await file.FlushAsync(token).ConfigureAwait(false);
        }

        token.ThrowIfCancellationRequested();
        if (hash.Length > 0)
        {
            progress?.Invoke(size, size);
            var actual = await FileChecker.ComputeAsync(dataPath, algorithm, token).ConfigureAwait(false);
            if (!actual.Equals(hash, StringComparison.OrdinalIgnoreCase))
            {
                ChunkedPaths.Delete(targetPath, hash);
                throw new HashMismatchException("文件完整性校验失败。");
            }
        }

        File.Move(dataPath, targetPath, true);
        DeleteQuietly(ledgerPath);
        Report(true);
    }

    async Task FetchWithRetryAsync(ChunkRange range, SafeFileHandle handle, CancellationToken token)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await FetchAsync(range, handle, token).ConfigureAwait(false);
                MarkComplete(range);
                return;
            }
            catch (Exception ex) when (attempt < options.MaxRetries && !token.IsCancellationRequested && ex is not RangeNotSupportedException && DownloadErrors.IsTransient(ex))
            {
                message?.Invoke($"分片 {range.Index + 1}/{ranges.Count} 中断，重试 {attempt + 1}/{options.MaxRetries}…");
                await Task.Delay(DownloadBackoff.Exponential(attempt), token).ConfigureAwait(false);
            }
        }
    }

    async Task FetchAsync(ChunkRange range, SafeFileHandle handle, CancellationToken token)
    {
        Interlocked.Exchange(ref downloaded[range.Index], 0);
        try
        {
            using var request = DownloadHttp.Get(uri);
            request.Headers.Range = new RangeHeaderValue(range.Start, range.End);
            using var headerTimeout = Linked(token, options.RequestTimeout);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, headerTimeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                throw new RangeNotSupportedException("服务器拒绝了分片请求。");
            if (response.StatusCode != HttpStatusCode.PartialContent)
                throw new RangeNotSupportedException("服务器没有按分片返回数据。");
            var contentRange = response.Content.Headers.ContentRange;
            if (contentRange?.From != range.Start || contentRange.To != range.End)
                throw new IOException("服务器返回的分片位置无效。");
            await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            var buffer = new byte[81920];
            var offset = range.Start;
            var read = 0L;
            while (read < range.Length)
            {
                int count;
                using (var readTimeout = Linked(token, options.RequestTimeout))
                {
                    count = await input.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, range.Length - read)), readTimeout.Token).ConfigureAwait(false);
                }

                if (count == 0)
                    throw new IOException("分片连接提前中断。");
                await RandomAccess.WriteAsync(handle, buffer.AsMemory(0, count), offset, token).ConfigureAwait(false);
                offset += count;
                read += count;
                Interlocked.Add(ref downloaded[range.Index], count);
                Report(false);
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new IOException("分片下载超时，已保留已下载部分。");
        }
    }

    void MarkComplete(ChunkRange range)
    {
        downloaded[range.Index] = range.Length;
        lock (completed)
        {
            completed.Add(range.Index);
            if (ledger != null)
            {
                ledger.Completed = [.. completed.Order()];
                ChunkLedger.Save(ledgerPath, ledger);
            }
        }

        Report(true);
    }

    void Report(bool force)
    {
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref lastReportTicks);
        if (!force && now - last < 250)
            return;
        if (Interlocked.CompareExchange(ref lastReportTicks, now, last) != last)
            return;
        var done = 0L;
        for (var index = 0; index < downloaded.Length; index++)
            done += Interlocked.Read(ref downloaded[index]);
        progress?.Invoke(done, size);
    }

    static CancellationTokenSource Linked(CancellationToken token, int seconds)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(token);
        if (seconds > 0)
            source.CancelAfter(TimeSpan.FromSeconds(seconds));
        return source;
    }

    static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
