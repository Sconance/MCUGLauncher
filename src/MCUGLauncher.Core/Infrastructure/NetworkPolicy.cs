using System.Net;

namespace BlockLaunch;

/// <summary>
/// 表示 Minecraft 文件下载源的偏好。
/// </summary>
public enum DownloadSourcePreference
{
    /// <summary>
    /// 优先使用 Mojang 官方服务器。
    /// </summary>
    Official = 0,
    /// <summary>
    /// 优先使用国内镜像服务器。
    /// </summary>
    Mirror = 1
}

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
    /// 获取或设置 下载源偏好；旧配置缺少该字段时默认镜像优先。
    /// </summary>
    public DownloadSourcePreference DownloadSourcePreference { get; set; } = DownloadSourcePreference.Mirror;
    /// <summary>
    /// 获取实际生效的下载源偏好，并兼容旧的 <see cref="DownloadSource"/> 字符串。
    /// </summary>
    public DownloadSourcePreference EffectiveDownloadSource => DownloadSource switch
    {
        "仅官方" => DownloadSourcePreference.Official,
        "BMCLAPI 优先" => DownloadSourcePreference.Mirror,
        _ => DownloadSourcePreference
    };
    /// <summary>
    /// 获取或设置 是否启用分片并发下载。
    /// </summary>
    public bool EnableFastDownload { get; set; } = true;
    /// <summary>
    /// 获取或设置 同时活跃的下载文件数。
    /// </summary>
    public int MaxFileConcurrency { get; set; } = 4;
    /// <summary>
    /// 获取或设置 单个文件内部的分片并发数。
    /// </summary>
    public int MaxChunkConcurrency { get; set; } = 3;
    /// <summary>
    /// 获取或设置 允许分片的最小文件大小（字节）。
    /// </summary>
    public long ChunkSizeThreshold { get; set; } = 1024 * 1024;
    /// <summary>
    /// 获取或设置 小于该大小的文件完全不分片（字节）。
    /// </summary>
    public long SmallFileThreshold { get; set; } = 256 * 1024;
    /// <summary>
    /// 获取或设置 单源测速读取的字节数。
    /// </summary>
    public long SpeedTestBytes { get; set; } = 256 * 1024;
    /// <summary>
    /// 获取或设置 测速结果的缓存分钟数。
    /// </summary>
    public int SpeedCacheMinutes { get; set; } = 3;
    /// <summary>
    /// 获取或设置 单次网络请求的超时秒数，0 表示沿用 HttpClient 超时。
    /// </summary>
    public int RequestTimeout { get; set; } = 20;
    /// <summary>
    /// 获取或设置 单文件的重试次数（不含首次尝试）。
    /// </summary>
    public int MaxRetries { get; set; } = 3;
    /// <summary>
    /// 获取或设置 默认哈希算法：SHA1、SHA256 或 SHA512。
    /// </summary>
    public string HashAlgorithm { get; set; } = "SHA1";
    /// <summary>
    /// 获取或设置 是否启用 DNS 兜底接口（默认关闭，见 IHostResolver 说明）。
    /// </summary>
    public bool EnableDnsFallback { get; set; }
    /// <summary>
    /// 获取或设置 取消时是否保留可续传的临时文件。
    /// </summary>
    public bool KeepPartialOnCancel { get; set; } = true;

    /// <summary>
    /// 执行 Validate。
    /// </summary>
    public void Validate()
    {
        if (LimitKibPerSecond is < 0 or > 1048576)
            throw new IOException("下载限速应为 0–1048576 KiB/s，0 为不限速。");
        if (ProxyMode is not ("系统代理" or "直连" or "自定义代理") || DownloadSource is not ("" or "仅官方" or "官方优先，失败切换 BMCLAPI" or "BMCLAPI 优先"))
            throw new IOException("网络设置无效。");
        if (!Enum.IsDefined(DownloadSourcePreference))
            DownloadSourcePreference = DownloadSourcePreference.Mirror;
        if (ProxyMode == "自定义代理" && (!Uri.TryCreate(ProxyAddress, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length > 0 || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0))
            throw new IOException("请输入不含账号密码的 HTTP(S) 代理地址，例如 http://127.0.0.1:7890。");
        // 下载参数被手工改坏时静默夹取，不让启动流程因配置文件而失败。
        MaxFileConcurrency = Math.Clamp(MaxFileConcurrency, 1, 16);
        MaxChunkConcurrency = Math.Clamp(MaxChunkConcurrency, 1, 8);
        ChunkSizeThreshold = Math.Clamp(ChunkSizeThreshold, 256 * 1024, 64 * 1024 * 1024);
        SmallFileThreshold = Math.Clamp(SmallFileThreshold, 16 * 1024, ChunkSizeThreshold);
        SpeedTestBytes = Math.Clamp(SpeedTestBytes, 16 * 1024, 4 * 1024 * 1024);
        SpeedCacheMinutes = Math.Clamp(SpeedCacheMinutes, 1, 60);
        RequestTimeout = Math.Clamp(RequestTimeout, 0, 120);
        MaxRetries = Math.Clamp(MaxRetries, 0, 10);
        if (HashAlgorithm is not ("SHA1" or "SHA256" or "SHA512"))
            HashAlgorithm = "SHA1";
    }
}

/// <summary>
/// 提供 NetworkPolicy处理器 相关功能。
/// </summary>
public sealed class NetworkPolicyHandler(NetworkSettings settings, HttpMessageHandler inner) : DelegatingHandler(inner)
{
    /// <summary>
    /// 标记由下载引擎自行选源的请求。命中该标记时网络层原样透传，避免与引擎重复换源。
    /// </summary>
    internal static readonly HttpRequestOptionsKey<bool> DirectSource = new("MCUGLauncher.DirectSource");

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
        if (request.Options.TryGetValue(DirectSource, out var direct) && direct)
            return await base.SendAsync(request, token);
        if (request.Method != HttpMethod.Get || request.Content != null || request.RequestUri == null || request.Headers.Authorization != null || request.Headers.Contains("Cookie"))
            return await base.SendAsync(request, token);
        var mirror = Mirror(request.RequestUri);
        var endpoints = mirror == null ? new[] {
            request.RequestUri
        } : settings.EffectiveDownloadSource == DownloadSourcePreference.Mirror ? new[] {
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
