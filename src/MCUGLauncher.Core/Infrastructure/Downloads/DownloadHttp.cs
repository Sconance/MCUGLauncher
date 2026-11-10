using System.Net.Http;

namespace BlockLaunch.Downloads;

/// <summary>
/// 构建下载引擎自己的请求：统一 User-Agent、Accept 与「由引擎选源」标记。
/// 这些请求不经过 <see cref="NetworkPolicyHandler"/> 的镜像改写，避免同一次下载被换两次源。
/// </summary>
internal static class DownloadHttp
{
    static readonly string Agent = ProductInfo.Name.Replace(" ", "") + "/" + ProductInfo.Version;

    public static HttpRequestMessage Get(Uri uri) => Create(HttpMethod.Get, uri);

    public static HttpRequestMessage Head(Uri uri) => Create(HttpMethod.Head, uri);

    static HttpRequestMessage Create(HttpMethod method, Uri uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation("User-Agent", Agent);
        request.Headers.TryAddWithoutValidation("Accept", "*/*");
        request.Options.Set(NetworkPolicyHandler.DirectSource, true);
        return request;
    }
}
