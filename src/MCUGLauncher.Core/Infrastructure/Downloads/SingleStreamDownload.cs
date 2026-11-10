using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace BlockLaunch.Downloads;

/// <summary>
/// 单流下载的不可调策略：大小上限、尝试次数、读取超时与退避。
/// </summary>
internal sealed record SingleStreamPolicy(long MaxBytes, int MaxAttempts, TimeSpan ReadTimeout, Func<int, TimeSpan> Backoff, bool AllowInsecureTransport = false, bool AllowMissingHash = false)
{
    /// <summary>
    /// 现有 <see cref="ReliableDownloader"/> 的线上行为：1.5 GB 上限、4 次尝试、45 秒读取超时、线性退避、必须带哈希。
    /// </summary>
    public static SingleStreamPolicy Legacy { get; } = new(1536L * 1024 * 1024, 4, TimeSpan.FromSeconds(45), attempt => TimeSpan.FromSeconds(attempt + 1));
}

/// <summary>
/// 单流（不断点分片）下载器，逐条保留原有请求序列与异常文案。
/// </summary>
/// <remarks>
/// 不发送 HEAD 或额外的探测请求：每次尝试恰好一个 GET，断点续传只依赖 .part 文件长度。
/// 分片下载由 <see cref="ChunkedDownload"/> 负责，两者共用同一份 .part 命名规则，但分片数据使用独立后缀，互不误读。
/// </remarks>
internal sealed class SingleStreamDownload(
    HttpClient http,
    Uri uri,
    string targetPath,
    string hash,
    HashAlgorithmName algorithm,
    long? size,
    SingleStreamPolicy policy,
    Action<string>? message = null,
    Action<long, long>? bytes = null)
{
    /// <summary>
    /// 执行下载；失败时抛出原始异常，由调用方翻译或换源。
    /// </summary>
    public async Task RunAsync(CancellationToken token)
    {
        if (uri.Scheme != "https" && !policy.AllowInsecureTransport || hash.Length > 0 && !FileChecker.IsValidHash(hash, algorithm) || hash.Length == 0 && !policy.AllowMissingHash)
            throw new IOException("下载地址或文件校验信息无效。");
        if (size is < 0 || size > policy.MaxBytes)
            throw new IOException("下载文件过大。");
        targetPath = Path.GetFullPath(targetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var partial = ReliableDownloader.PartialPath(targetPath, hash);
        // 与旧实现共用 .lock，保证同机两个进程不会同时追加同一个临时文件。
        await using var lease = new FileStream(partial + ".lock", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        async Task<bool> Valid(string path)
        {
            if (!File.Exists(path) || size.HasValue && new FileInfo(path).Length != size.Value || new FileInfo(path).Length > policy.MaxBytes)
                return false;
            if (hash.Length == 0)
                return size.HasValue && new FileInfo(path).Length == size.Value;
            await using var input = File.OpenRead(path);
            var actual = algorithm.Name switch
            {
                "SHA1" => await SHA1.HashDataAsync(input, token),
                "SHA512" => await SHA512.HashDataAsync(input, token),
                _ => await SHA256.HashDataAsync(input, token)
            };
            return Convert.ToHexString(actual).Equals(hash, StringComparison.OrdinalIgnoreCase);
        }

        if (await Valid(targetPath))
            return;
        // 空哈希且大小未知时，旧 .part 无法证明属于同一内容，直接丢弃，避免把旧字节当断点。
        if (hash.Length == 0 && !size.HasValue && File.Exists(partial))
            File.Delete(partial);
        for (var attempt = 0; attempt < policy.MaxAttempts; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (await Valid(partial))
                {
                    File.Move(partial, targetPath, true);
                    return;
                }

                var offset = File.Exists(partial) ? new FileInfo(partial).Length : 0;
                if (offset > policy.MaxBytes || size.HasValue && offset >= size)
                {
                    File.Delete(partial);
                    offset = 0;
                }

                using var request = DownloadHttp.Get(uri);
                request.Headers.AcceptEncoding.ParseAdd("identity");
                if (offset > 0)
                    request.Headers.Range = new RangeHeaderValue(offset, null);
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    File.Delete(partial);
                    throw new IOException("服务器无法继续下载，正在重新获取。");
                }

                response.EnsureSuccessStatusCode();
                var range = response.Content.Headers.ContentRange;
                if (response.StatusCode == HttpStatusCode.PartialContent && (range?.From != offset || range.Unit != "bytes"))
                    throw new IOException("服务器返回的续传位置无效。");
                if (response.StatusCode != HttpStatusCode.PartialContent)
                    offset = 0;
                var expectedEnd = response.Content.Headers.ContentLength is long length ? checked(offset + length) : (long?)null;
                if (expectedEnd > policy.MaxBytes || size.HasValue && expectedEnd.HasValue && expectedEnd != size)
                    throw new IOException("服务器返回的文件大小不匹配。");
                message?.Invoke(offset > 0 ? $"继续下载：{offset / 1024:N0} KB" : "开始下载…");
                await using (var input = await response.Content.ReadAsStreamAsync(token))
                await using (var output = new FileStream(partial, offset > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920];
                    var total = offset;
                    int count;
                    var lastProgress = DateTime.UtcNow;
                    while (true)
                    {
                        using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                        if (policy.ReadTimeout > TimeSpan.Zero)
                            readTimeout.CancelAfter(policy.ReadTimeout);
                        try
                        {
                            count = await input.ReadAsync(buffer, readTimeout.Token);
                        }
                        catch (OperationCanceledException) when (!token.IsCancellationRequested)
                        {
                            throw new IOException("下载读取超时，已保留下载进度。");
                        }

                        if (count == 0)
                            break;
                        total += count;
                        if (total > policy.MaxBytes || size.HasValue && total > size)
                            throw new IOException("文件超出预期大小。");
                        await output.WriteAsync(buffer.AsMemory(0, count), token);
                        if ((DateTime.UtcNow - lastProgress).TotalMilliseconds >= 250)
                        {
                            lastProgress = DateTime.UtcNow;
                            var expected = size ?? expectedEnd;
                            message?.Invoke(expected is > 0 ? $"下载 {total / 1024:N0} / {expected / 1024:N0} KB · {total * 100.0 / expected:F1}%" : $"已下载 {total / 1024:N0} KB");
                            if (expected is > 0)
                            {
                                bytes?.Invoke(total, expected.Value);
                                OperationExecution.Ambient.Value?.ReportFileProgress(total, expected.Value);
                            }
                        }
                    }

                    if (expectedEnd.HasValue && total != expectedEnd)
                        throw new IOException("下载连接提前中断。");
                }

                if (hash.Length > 0 && !await Valid(partial)
                    || hash.Length == 0 && size.HasValue && new FileInfo(partial).Length != size.Value)
                {
                    File.Delete(partial);
                    throw new IOException("文件完整性校验失败，正在重试。");
                }

                token.ThrowIfCancellationRequested();
                File.Move(partial, targetPath, true);
                return;
            }
            catch (Exception ex) when (attempt < policy.MaxAttempts - 1 && !token.IsCancellationRequested && !DownloadErrors.IsPermanentFileError(ex) && (ex is IOException || ex is HttpRequestException h && (h.StatusCode == null || h.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)h.StatusCode >= 500) || ex is OperationCanceledException))
            {
                message?.Invoke($"下载中断，重试 {attempt + 1}/{policy.MaxAttempts - 1}（保留已下载部分）…");
                await Task.Delay(policy.Backoff(attempt), token);
            }
        }
    }
}
