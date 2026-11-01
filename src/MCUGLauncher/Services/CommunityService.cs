using System.Diagnostics;
using System.Net.Http;

namespace MCUGLauncher.Services;

/// <summary>
/// 社区数据源访问。
/// </summary>
/// <param name="url">数据源地址</param>
/// <param name="token">取消令牌</param>
/// <returns>是否成功、响应文本与耗时（毫秒）</returns>
/// <remarks>
/// 依赖 <see cref="System.Net.Http.HttpClient"/>；用于测试数据源连通性与读取公告/推荐 JSON。
/// </remarks>
public sealed class CommunityService
{
    readonly HttpClient _http;
    public CommunityService(HttpClient http) => _http = http;

    public async Task<(bool Ok, string Text, int Ms)> TestAsync(string url, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        token = deadline.Token;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return (false, "地址需以 https:// 或 http:// 开头", 0);

        var watch = Stopwatch.StartNew();
        try
        {
            using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
            watch.Stop();
            return response.IsSuccessStatusCode
                ? (true, $"连接正常 · {watch.ElapsedMilliseconds} ms", (int)watch.ElapsedMilliseconds)
                : (false, $"HTTP {(int)response.StatusCode} · {response.ReasonPhrase}", (int)watch.ElapsedMilliseconds);
        }
        catch (TaskCanceledException)
        {
            return (false, "连接超时（8s）", 8000);
        }
        catch (HttpRequestException ex)
        {
            return (false, ex.Message, (int)watch.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// 读取数据源文本。
    /// </summary>
    /// <param name="url">数据源地址</param>
    /// <param name="token">取消令牌</param>
    /// <returns>响应文本；失败时为 null</returns>
    /// <remarks>
    /// 成功返回响应体，失败返回 null，不抛出网络异常。
    /// </remarks>
    public async Task<string?> ReadAsync(string url, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        token = deadline.Token;
        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadAsStringAsync(token);
        }
        catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException)
        {
            return null;
        }
    }
}
