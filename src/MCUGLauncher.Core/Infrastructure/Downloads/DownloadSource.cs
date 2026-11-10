namespace BlockLaunch.Downloads;

/// <summary>
/// 一个候选下载源。
/// </summary>
/// <param name="Name">展示与日志用的短名，例如 官方 或 BMCLAPI。</param>
/// <param name="Uri">完整下载地址。</param>
/// <param name="IsMirror">是否为镜像源（镜像源失败时静默切换，不影响任务结果）。</param>
public sealed record DownloadSource(string Name, Uri Uri, bool IsMirror = false)
{
    /// <summary>
    /// 创建官方源。
    /// </summary>
    public static DownloadSource Official(Uri uri) => new("官方", uri);

    /// <summary>
    /// 创建镜像源。
    /// </summary>
    public static DownloadSource Mirror(string name, Uri uri) => new(name, uri, true);
}
