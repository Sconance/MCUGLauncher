namespace MCUGLauncher.Contracts;

/// <summary>日志、系统信息、打开目录与图片下载。</summary>
public interface IDiagnosticService
{
    /// <summary>打开某个目录；instance 留空时跟随当前实例（版本、模组等目录）。</summary>
    Task OpenFolderAsync(string kind, string instance = "");

    [FeatureOperation]
    Task<string> ReadLogAsync() =>
        FeatureRequest.InvokeAsync<string>(this, nameof(ReadLogAsync), new
        {
        });

    [FeatureOperation]
    Task<SystemInfoInfo> SystemInfoAsync() =>
        FeatureRequest.InvokeAsync<SystemInfoInfo>(this, nameof(SystemInfoAsync), new
        {
        });

    [FeatureOperation]
    Task<byte[]> DownloadImageAsync(string url) =>
        FeatureRequest.InvokeAsync<byte[]>(this, nameof(DownloadImageAsync), new
        {
            url
        });
}
