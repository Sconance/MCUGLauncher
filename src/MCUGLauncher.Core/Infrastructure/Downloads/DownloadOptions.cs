namespace BlockLaunch.Downloads;

/// <summary>
/// 高速下载引擎的可调参数。
/// </summary>
/// <remarks>
/// 默认值偏保守：同时活跃文件 4 个、单文件分片 3 路、1 MiB 以下不分片。非法值经 <see cref="Normalize"/> 回退为默认值，
/// 保证配置文件被手工改坏时下载仍能继续。
/// </remarks>
public sealed class DownloadOptions
{
    public const int DefaultMaxFileConcurrency = 4;
    public const int DefaultMaxChunkConcurrency = 3;
    public const int DefaultChunkSizeThreshold = 1024 * 1024;
    public const int DefaultSmallFileThreshold = 256 * 1024;
    public const int DefaultSpeedTestBytes = 256 * 1024;
    public const int DefaultSpeedCacheMinutes = 3;
    public const int DefaultRequestTimeout = 20;
    public const int DefaultMaxRetries = 3;
    public const string DefaultHashAlgorithm = "SHA1";
    public const long DefaultMaxFileBytes = 4L * 1024 * 1024 * 1024;

    /// <summary>
    /// 单个大文件最多切分的片数，避免 2 GiB 文件生成上千条续传记录。
    /// </summary>
    public const int MaxChunksPerFile = 64;

    /// <summary>
    /// 获取或设置 同时活跃的文件数上限。
    /// </summary>
    public int MaxFileConcurrency { get; init; } = DefaultMaxFileConcurrency;
    /// <summary>
    /// 获取或设置 单个文件内部的分片并发上限。
    /// </summary>
    public int MaxChunkConcurrency { get; init; } = DefaultMaxChunkConcurrency;
    /// <summary>
    /// 获取或设置 允许分片的最小文件大小（字节）。
    /// </summary>
    public int ChunkSizeThreshold { get; init; } = DefaultChunkSizeThreshold;
    /// <summary>
    /// 获取或设置 小于该大小的文件完全不分片、不探测（字节）。
    /// </summary>
    public int SmallFileThreshold { get; init; } = DefaultSmallFileThreshold;
    /// <summary>
    /// 获取或设置 单源测速读取的字节数。
    /// </summary>
    public int SpeedTestBytes { get; init; } = DefaultSpeedTestBytes;
    /// <summary>
    /// 获取或设置 测速结果的缓存分钟数。
    /// </summary>
    public int SpeedCacheMinutes { get; init; } = DefaultSpeedCacheMinutes;
    /// <summary>
    /// 获取或设置 单次网络请求的超时秒数（0 表示沿用 HttpClient 的超时）。
    /// </summary>
    public int RequestTimeout { get; init; } = DefaultRequestTimeout;
    /// <summary>
    /// 获取或设置 单文件的重试次数（不含首次尝试）。
    /// </summary>
    public int MaxRetries { get; init; } = DefaultMaxRetries;
    /// <summary>
    /// 获取或设置 默认哈希算法名：SHA1、SHA256 或 SHA512。
    /// </summary>
    public string HashAlgorithm { get; init; } = DefaultHashAlgorithm;
    /// <summary>
    /// 获取或设置 是否启用 DNS 兜底接口。
    /// </summary>
    public bool EnableDnsFallback { get; init; }
    /// <summary>
    /// 获取或设置 是否启用分片下载；关闭后全部文件走单线程兼容路径。
    /// </summary>
    public bool EnableFastDownload { get; init; } = true;
    /// <summary>
    /// 获取或设置 取消时是否保留可续传的临时文件。
    /// </summary>
    public bool KeepPartialOnCancel { get; init; } = true;
    /// <summary>
    /// 获取或设置 单个文件的大小上限（字节）。
    /// </summary>
    public long MaxFileBytes { get; init; } = DefaultMaxFileBytes;
    /// <summary>
    /// 获取或设置 是否允许 http 明文源；仅用于本机测试服务器，生产配置不会打开。
    /// </summary>
    public bool AllowInsecureTransport { get; init; }
    /// <summary>
    /// 获取或设置 下载源偏好；镜像优先时任务会同时携带镜像与官方地址。
    /// </summary>
    public DownloadSourcePreference Preference { get; init; } = DownloadSourcePreference.Mirror;

    /// <summary>
    /// 从启动器网络设置创建配置；未填写的项沿用引擎默认值。
    /// </summary>
    public static DownloadOptions From(NetworkSettings settings) => new()
    {
        EnableFastDownload = settings.EnableFastDownload,
        MaxFileConcurrency = settings.MaxFileConcurrency,
        MaxChunkConcurrency = settings.MaxChunkConcurrency,
        ChunkSizeThreshold = (int)Math.Clamp(settings.ChunkSizeThreshold, 256 * 1024, 64L * 1024 * 1024),
        SmallFileThreshold = (int)Math.Clamp(settings.SmallFileThreshold, 16 * 1024, 64L * 1024 * 1024),
        SpeedTestBytes = (int)Math.Clamp(settings.SpeedTestBytes, 16 * 1024, 4L * 1024 * 1024),
        SpeedCacheMinutes = settings.SpeedCacheMinutes,
        RequestTimeout = settings.RequestTimeout,
        MaxRetries = settings.MaxRetries,
        HashAlgorithm = settings.HashAlgorithm,
        EnableDnsFallback = settings.EnableDnsFallback,
        KeepPartialOnCancel = settings.KeepPartialOnCancel,
        Preference = settings.EffectiveDownloadSource
    };

    /// <summary>
    /// 返回取值合法的新实例；越界项回退为默认值，不抛异常。
    /// </summary>
    public DownloadOptions Normalize()
    {
        var chunkThreshold = Between(ChunkSizeThreshold, 256 * 1024, 64 * 1024 * 1024, DefaultChunkSizeThreshold);
        return new()
        {
            MaxFileConcurrency = Between(MaxFileConcurrency, 1, 16, DefaultMaxFileConcurrency),
            MaxChunkConcurrency = Between(MaxChunkConcurrency, 1, 8, DefaultMaxChunkConcurrency),
            ChunkSizeThreshold = chunkThreshold,
            SmallFileThreshold = Between(SmallFileThreshold, 16 * 1024, chunkThreshold, DefaultSmallFileThreshold),
            SpeedTestBytes = Between(SpeedTestBytes, 16 * 1024, 4 * 1024 * 1024, DefaultSpeedTestBytes),
            SpeedCacheMinutes = Between(SpeedCacheMinutes, 1, 60, DefaultSpeedCacheMinutes),
            RequestTimeout = Between(RequestTimeout, 0, 120, DefaultRequestTimeout),
            MaxRetries = Between(MaxRetries, 0, 10, DefaultMaxRetries),
            HashAlgorithm = NormalizeAlgorithm(HashAlgorithm),
            EnableDnsFallback = EnableDnsFallback,
            EnableFastDownload = EnableFastDownload,
            KeepPartialOnCancel = KeepPartialOnCancel,
            MaxFileBytes = Math.Max(1024 * 1024, MaxFileBytes),
            AllowInsecureTransport = AllowInsecureTransport,
            Preference = Enum.IsDefined(Preference) ? Preference : DownloadSourcePreference.Mirror
        };
    }

    static int Between(int value, int minimum, int maximum, int fallback) => value < minimum || value > maximum ? fallback : value;

    static string NormalizeAlgorithm(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "SHA1" or "SHA-1" => "SHA1",
        "SHA256" or "SHA-256" => "SHA256",
        "SHA512" or "SHA-512" => "SHA512",
        _ => DefaultHashAlgorithm
    };
}
