using System.Net;

namespace BlockLaunch.Downloads;

/// <summary>
/// 用 <c>Range: bytes=0-0</c> 探测源是否支持分片，并顺带取得文件总大小。
/// </summary>
/// <remarks>
/// 只在任务大小未知或需要确认分片能力时调用；已知大小的文件直接由首个分片的 206 响应当作探测，省掉一次请求。
/// </remarks>
internal static class RangeProbe
{
    /// <summary>
    /// 探测结果。
    /// </summary>
    /// <param name="Supported">源是否按 Range 返回 206。</param>
    /// <param name="Size">文件总大小；0 表示未知。</param>
    public readonly record struct Result(bool Supported, long Size);

    /// <summary>
    /// 执行探测；网络异常视为不支持分片，交由单流路径重试。
    /// </summary>
    public static async Task<Result> CheckAsync(HttpClient http, Uri uri, int timeoutSeconds, CancellationToken token)
    {
        try
        {
            using var request = DownloadHttp.Get(uri);
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            if (timeoutSeconds > 0)
                timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.PartialContent)
            {
                var length = response.Content.Headers.ContentRange?.Length ?? 0;
                return new(length > 0, length);
            }

            if (response.StatusCode == HttpStatusCode.OK)
                return new(false, response.Content.Headers.ContentLength ?? 0);
            return new(false, 0);
        }
        catch (Exception ex) when (!token.IsCancellationRequested && ex is HttpRequestException or OperationCanceledException or IOException)
        {
            return new(false, 0);
        }
    }
}
