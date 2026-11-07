using System.Net;

namespace BlockLaunch;

/// <summary>
/// 提供 Network设置 相关功能。
/// </summary>
public sealed class NetworkSettings
{
    public int LimitKibPerSecond
    {
        get;
        set;
    }
    /// <summary>
    /// 获取或设置 ProxyMode。
    /// </summary>
    public string ProxyMode { get; set; } = "系统代理";
    /// <summary>
    /// 获取或设置 Proxy地址。
    /// </summary>
    public string ProxyAddress { get; set; } = "";
    /// <summary>
    /// 获取或设置 下载Source。
    /// </summary>
    public string DownloadSource { get; set; } = "官方优先，失败切换 BMCLAPI";

    /// <summary>
    /// 执行 Validate。
    /// </summary>
    public void Validate()
    {
        if (LimitKibPerSecond is < 0 or > 1048576)
            throw new IOException("下载限速应为 0–1048576 KiB/s，0 为不限速。");
        if (ProxyMode is not ("系统代理" or "直连" or "自定义代理") || DownloadSource is not ("仅官方" or "官方优先，失败切换 BMCLAPI" or "BMCLAPI 优先"))
            throw new IOException("网络设置无效。");
        if (ProxyMode == "自定义代理" && (!Uri.TryCreate(ProxyAddress, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length > 0 || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0))
            throw new IOException("请输入不含账号密码的 HTTP(S) 代理地址，例如 http://127.0.0.1:7890。");
    }
}

/// <summary>
/// 提供 NetworkPolicy处理器 相关功能。
/// </summary>
public sealed class NetworkPolicyHandler(NetworkSettings settings, HttpMessageHandler inner) : DelegatingHandler(inner)
{
    /// <summary>
    /// 执行 Mirror。
    /// </summary>
    public static Uri? Mirror(Uri uri) => uri.Host switch
    {
        "libraries.minecraft.net" => new("https://bmclapi2.bangbang93.com/maven" + uri.PathAndQuery),
        "resources.download.minecraft.net" => new("https://bmclapi2.bangbang93.com/assets" + uri.PathAndQuery),
        "launchermeta.mojang.com" or "launcher.mojang.com" => new("https://bmclapi2.bangbang93.com" + uri.PathAndQuery),
        _ => null
    };

    /// <summary>
    /// 发送。
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        if (request.Method != HttpMethod.Get || request.Content != null || request.RequestUri == null || request.Headers.Authorization != null || request.Headers.Contains("Cookie"))
            return await base.SendAsync(request, token);
        var mirror = settings.DownloadSource == "仅官方" ? null : Mirror(request.RequestUri);
        var endpoints = mirror == null ? new[] {
            request.RequestUri
        } : settings.DownloadSource == "BMCLAPI 优先" ? new[] {
            mirror,
            request.RequestUri
        } : new[] {
            request.RequestUri,
            mirror
        };
        Exception? last = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            token.ThrowIfCancellationRequested();
            var endpoint = endpoints[Math.Min(attempt, endpoints.Length - 1)];
            using var clone = new HttpRequestMessage(HttpMethod.Get, endpoint);
            foreach (var header in request.Headers)
                if (!header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                    clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            try
            {
                var response = await base.SendAsync(clone, timeout.Token);
                if (response.IsSuccessStatusCode || (int)response.StatusCode < 500 && response.StatusCode is not (HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests) && (mirror == null || response.StatusCode != HttpStatusCode.NotFound))
                    return response;
                if (attempt == 2)
                    return response;
                response.Dispose();
            }
            catch (Exception ex) when (!token.IsCancellationRequested && ex is HttpRequestException or OperationCanceledException)
            {
                last = ex;
                if (attempt == 2)
                    throw;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(400 * (attempt + 1)), token);
        }

        throw new HttpRequestException("网络请求失败。", last);
    }

    /// <summary>
    /// 创建。
    /// </summary>
    public static HttpClient Create(NetworkSettings settings)
    {
        settings.Validate();
        var handler = new HttpClientHandler
        {
            UseProxy = settings.ProxyMode != "直连"
        };
        if (settings.ProxyMode == "自定义代理")
            handler.Proxy = new WebProxy(settings.ProxyAddress);
        return new(new DownloadTransferHandler(new NetworkPolicyHandler(settings, handler), () => settings.LimitKibPerSecond))
        {
            Timeout = TimeSpan.FromMinutes(3)
        };
    }
}
