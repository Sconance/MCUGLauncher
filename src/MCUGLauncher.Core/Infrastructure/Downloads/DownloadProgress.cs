namespace BlockLaunch.Downloads;

/// <summary>
/// 下载阶段。
/// </summary>
public enum DownloadPhase
{
    /// <summary>
    /// 排队等待。
    /// </summary>
    Queued,
    /// <summary>
    /// 校验本地文件或探测源。
    /// </summary>
    Checking,
    /// <summary>
    /// 正在传输。
    /// </summary>
    Downloading,
    /// <summary>
    /// 失败后等待重试。
    /// </summary>
    Retrying,
    /// <summary>
    /// 校验完整文件。
    /// </summary>
    Verifying,
    /// <summary>
    /// 已完成。
    /// </summary>
    Completed,
    /// <summary>
    /// 失败。
    /// </summary>
    Failed
}

/// <summary>
/// 下载进度快照，供 UI 或日志消费。
/// </summary>
public sealed record DownloadProgress(
    string TaskId,
    DownloadKind Kind,
    string FilePath,
    DownloadPhase Phase,
    long CompletedBytes,
    long TotalBytes,
    double BytesPerSecond,
    TimeSpan? Remaining,
    int CompletedFiles,
    int TotalFiles,
    string Source,
    int Retry)
{
    /// <summary>
    /// 生成可直接显示的进度文本。
    /// </summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (Phase == DownloadPhase.Downloading && TotalBytes > 0)
            parts.Add($"{FormatBytes(CompletedBytes)} / {FormatBytes(TotalBytes)}");
        else
            parts.Add(Phase switch
            {
                DownloadPhase.Queued => "等待下载",
                DownloadPhase.Checking => "校验文件",
                DownloadPhase.Verifying => "校验完整文件",
                DownloadPhase.Retrying => "准备重试",
                DownloadPhase.Completed => "下载完成",
                DownloadPhase.Failed => "下载失败",
                _ => "开始下载"
            });
        if (Phase == DownloadPhase.Downloading && BytesPerSecond > 0)
            parts.Add(FormatSpeed(BytesPerSecond));
        if (Phase == DownloadPhase.Downloading && Remaining is { } remaining && remaining.TotalSeconds is > 0 and < 86400)
            parts.Add("剩余 " + FormatDuration(remaining));
        if (TotalFiles > 1)
            parts.Add($"{CompletedFiles}/{TotalFiles}");
        if (!string.IsNullOrEmpty(Source))
            parts.Add(Source);
        if (Retry > 0)
            parts.Add($"重试 {Retry}");
        return string.Join(" · ", parts);
    }

    /// <summary>
    /// 格式化字节数。
    /// </summary>
    public static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024):F2} GB",
        >= 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        >= 1024 => $"{bytes / 1024.0:F0} KB",
        _ => bytes + " B"
    };

    /// <summary>
    /// 格式化速度。
    /// </summary>
    public static string FormatSpeed(double bytesPerSecond) => FormatBytes((long)Math.Max(0, bytesPerSecond)) + "/s";

    /// <summary>
    /// 格式化时长。
    /// </summary>
    public static string FormatDuration(TimeSpan value) => value.TotalHours >= 1 ? $"{(int)value.TotalHours} 小时 {value.Minutes} 分" : value.TotalMinutes >= 1 ? $"{(int)value.TotalMinutes} 分 {value.Seconds} 秒" : $"{Math.Max(1, (int)Math.Ceiling(value.TotalSeconds))} 秒";
}
