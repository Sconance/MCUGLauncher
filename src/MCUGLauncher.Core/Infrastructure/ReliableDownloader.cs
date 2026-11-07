using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace BlockLaunch;

/// <summary>
/// 可校验、可断点续传的下载器。
/// </summary>
/// <remarks>
/// 依赖 HttpClient、SHA1/SHA256 校验值、.part 临时文件与大小校验；下载完成后原子替换目标文件。
/// </remarks>
public static class ReliableDownloader
{
    /// <summary>
    /// 执行 Partial路径。
    /// </summary>
    public static string PartialPath(string target, string hash) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(target))!, ".blocklaunch-" + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(target) + "\n" + hash.ToLowerInvariant()))).ToLowerInvariant() + ".part");

    /// <summary>
    /// 执行 下载。
    /// </summary>
    /// <param name="http">HTTP 客户端</param>
    /// <param name="uri">下载地址</param>
    /// <param name="target">目标文件</param>
    /// <param name="hash">期望的校验值</param>
    /// <param name="sha1">true 表示 SHA1，否则 SHA256</param>
    /// <param name="size">可选的文件大小</param>
    /// <param name="progress">进度回调</param>
    /// <param name="token">取消令牌</param>
    public static async Task Download(HttpClient http, Uri uri, string target, string hash, bool sha1 = false, long? size = null, Action<string>? progress = null, CancellationToken token = default)
    {
        try
        {
            await DownloadCore(http, uri, target, hash, sha1, size, progress, token);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException("下载目录或文件无法写入，请检查目录权限或只读属性；已有文件与下载进度已保留。", ex);
        }
        catch (IOException ex) when (PermanentFileError(ex))
        {
            var code = ex.HResult & 0xffff;
            var reason = code is 39 or 112 ? "磁盘空间不足，请释放空间后重试" : code is 32 or 33 ? "文件正在被其他程序占用，请关闭占用程序后重试" : "下载目录不可用，请检查路径、权限或只读属性";
            throw new IOException(reason + "；已有文件与下载进度已保留。", ex);
        }
    }

    static bool PermanentFileError(Exception ex) => ex is IOException && (ex.HResult & 0xffff) is 3 or 5 or 19 or 32 or 33 or 39 or 112 or 206 or 267;

    static async Task DownloadCore(HttpClient http, Uri uri, string target, string hash, bool sha1, long? size, Action<string>? progress, CancellationToken token)
    {
        if (uri.Scheme != "https" || hash.Length != (sha1 ? 40 : 128) || hash.Any(c => !Uri.IsHexDigit(c)))
            throw new IOException("下载地址或文件校验信息无效。");
        const long limit = 1536L * 1024 * 1024;
        if (size is < 0 or > limit)
            throw new IOException("下载文件过大。");
        target = Path.GetFullPath(target);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var partial = PartialPath(target, hash);
        // Prevent two operations from appending to the same partial file.
        await using var lease = new FileStream(partial + ".lock", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        async Task<bool> Valid(string path)
        {
            if (!File.Exists(path) || (size.HasValue && new FileInfo(path).Length != size.Value) || new FileInfo(path).Length > limit)
                return false;
            await using var input = File.OpenRead(path);
            var actual = sha1 ? await SHA1.HashDataAsync(input, token) : await SHA512.HashDataAsync(input, token);
            return Convert.ToHexString(actual).Equals(hash, StringComparison.OrdinalIgnoreCase);
        }

        if (await Valid(target))
            return;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (await Valid(partial))
                {
                    File.Move(partial, target, true);
                    return;
                }

                var offset = File.Exists(partial) ? new FileInfo(partial).Length : 0;
                if (offset > limit || (size.HasValue && offset >= size))
                {
                    File.Delete(partial);
                    offset = 0;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
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
                if (expectedEnd > limit || (size.HasValue && expectedEnd.HasValue && expectedEnd != size))
                    throw new IOException("服务器返回的文件大小不匹配。");
                progress?.Invoke(offset > 0 ? $"继续下载：{offset / 1024:N0} KB" : "开始下载…");
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
                        readTimeout.CancelAfter(TimeSpan.FromSeconds(45));
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
                        if (total > limit || (size.HasValue && total > size))
                            throw new IOException("文件超出预期大小。");
                        await output.WriteAsync(buffer.AsMemory(0, count), token);
                        if ((DateTime.UtcNow - lastProgress).TotalMilliseconds >= 250)
                        {
                            lastProgress = DateTime.UtcNow;
                            var expected = size ?? expectedEnd;
                            progress?.Invoke(expected is > 0 ? $"下载 {total / 1024:N0} / {expected / 1024:N0} KB · {total * 100.0 / expected:F1}%" : $"已下载 {total / 1024:N0} KB");
                            if (expected is > 0)
                                OperationExecution.Ambient.Value?.ReportFileProgress(total, expected.Value);
                        }
                    }

                    if (expectedEnd.HasValue && total != expectedEnd)
                        throw new IOException("下载连接提前中断。");
                }

                if (!await Valid(partial))
                {
                    File.Delete(partial);
                    throw new IOException("文件完整性校验失败，正在重试。");
                }

                token.ThrowIfCancellationRequested();
                File.Move(partial, target, true);
                return;
            }
            catch (Exception ex) when (attempt < 3 && !token.IsCancellationRequested && !PermanentFileError(ex) && (ex is IOException || ex is HttpRequestException h && (h.StatusCode == null || h.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)h.StatusCode >= 500) || ex is OperationCanceledException))
            {
                progress?.Invoke($"下载中断，重试 {attempt + 1}/3（保留已下载部分）…");
                await Task.Delay(TimeSpan.FromSeconds(attempt + 1), token);
            }
        }
    }
}
