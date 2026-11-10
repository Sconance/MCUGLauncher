using System.Security.Cryptography;

namespace BlockLaunch.Downloads;

/// <summary>
/// 一个待下载文件的完整描述。
/// </summary>
/// <remarks>
/// 由启动器流程构造后交给 <see cref="DownloadEngine"/>；引擎不关心资源来自版本清单、模组站点还是整合包。
/// </remarks>
public sealed class DownloadTask
{
    /// <summary>
    /// 获取任务标识，仅用于日志与进度展示。
    /// </summary>
    public required string Id { get; init; }
    /// <summary>
    /// 获取资源类型。
    /// </summary>
    public DownloadKind Kind { get; init; }
    /// <summary>
    /// 获取本地目标路径。
    /// </summary>
    public required string TargetPath { get; init; }
    /// <summary>
    /// 获取按优先级排列的候选源，第一个为首选。
    /// </summary>
    public required IReadOnlyList<DownloadSource> Sources { get; init; }
    /// <summary>
    /// 获取期望的十六进制哈希；为空表示无法校验，只能按大小接受。
    /// </summary>
    public string? Hash { get; init; }
    /// <summary>
    /// 获取哈希算法。
    /// </summary>
    public HashAlgorithmName Algorithm { get; init; } = HashAlgorithmName.SHA1;
    /// <summary>
    /// 获取文件大小；0 表示未知，需要在下载时探测。
    /// </summary>
    public long Size { get; init; }
    /// <summary>
    /// 获取调度优先级，数值小的先下载。
    /// </summary>
    public int Priority { get; init; }
    /// <summary>
    /// 获取是否允许对该文件分片下载。
    /// </summary>
    public bool AllowSplitting { get; init; } = true;

    /// <summary>
    /// 创建只有一个源的简单任务。
    /// </summary>
    public static DownloadTask FromUri(string id, DownloadKind kind, string target, Uri uri, string? hash = null, HashAlgorithmName algorithm = default, long size = 0, bool allowSplitting = true)
        => new()
        {
            Id = id,
            Kind = kind,
            TargetPath = target,
            Sources = [DownloadSource.Official(uri)],
            Hash = hash,
            Algorithm = algorithm == default ? HashAlgorithmName.SHA1 : algorithm,
            Size = size,
            AllowSplitting = allowSplitting
        };
}
