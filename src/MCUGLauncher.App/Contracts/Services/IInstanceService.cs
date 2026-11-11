namespace MCUGLauncher.Contracts;

/// <summary>实例选择、启动与启动选项。</summary>
public interface IInstanceService
{
    [FeatureOperation]
    Task<LaunchResultInfo> LaunchAsync(string version) =>
        FeatureRequest.InvokeAsync<LaunchResultInfo>(this, nameof(LaunchAsync), new
        {
            version
        });

    /// <summary>使用明确的实例和服务器地址启动；不永久修改实例启动设置。</summary>
    [FeatureOperation]
    Task<LaunchResultInfo> LaunchToServerAsync(string instance, string serverAddress) =>
        FeatureRequest.InvokeAsync<LaunchResultInfo>(this, nameof(LaunchToServerAsync), new
        {
            instance,
            serverAddress
        });

    [FeatureOperation]
    Task<SavedInfo> SelectInstanceAsync(string instance) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(SelectInstanceAsync), new
        {
            instance
        });

    [FeatureOperation]
    Task<SavedInfo> SelectVersionAsync(string version) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(SelectVersionAsync), new
        {
            version
        });

    [FeatureOperation]
    Task<LaunchOptionsInfo> ReadLaunchOptionsAsync(string version, string requestId = "") =>
        FeatureRequest.InvokeAsync<LaunchOptionsInfo>(
            this,
            nameof(ReadLaunchOptionsAsync),
            new
            {
                version,
                requestId
            });

    [FeatureOperation]
    Task<SavedInfo> SaveLaunchOptionsAsync(
        string token,
        string version,
        string name,
        LaunchOptionsSettings settings,
        bool inherit) =>
        FeatureRequest.InvokeAsync<SavedInfo>(
            this,
            nameof(SaveLaunchOptionsAsync),
            new
            {
                token,
                version,
                name,
                settings,
                inherit
            });
}
