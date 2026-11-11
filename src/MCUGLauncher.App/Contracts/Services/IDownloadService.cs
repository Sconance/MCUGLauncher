namespace MCUGLauncher.Contracts;

/// <summary>资源搜索、版本选择、安装任务与整合包导入。</summary>
public interface IDownloadService
{
    event Action<ContentErrorInfo>? ContentErrorReceived;

    IReadOnlyList<ContentItem> ContentItems
    {
        get;
    }

    [FeatureOperation]
    Task<ContentSearchResultInfo> SearchContentAsync(string category, string query, string requestId = "") =>
        FeatureRequest.InvokeAsync<ContentSearchResultInfo>(
            this,
            nameof(SearchContentAsync),
            new
            {
                category,
                query,
                requestId
            });

    Task InstallContentAsync(string id, string category, string versionId, string mode = "instance", string path = "");
    Task InstallModAsync(string id, string versionId, string mode, string path = "");

    [FeatureOperation]
    Task<ResourceVersionsResultInfo> ListContentVersionsAsync(
        string id,
        string category,
        string instance = "",
        string game = "",
        string loader = "",
        string requestId = "") =>
        FeatureRequest.InvokeAsync<ResourceVersionsResultInfo>(
            this,
            nameof(ListContentVersionsAsync),
            new
            {
                id,
                category,
                instance,
                game,
                loader,
                requestId
            });

    Task QueueContentVersionAsync(string token, string versionId, string mode = "instance", string path = "");

    [FeatureOperation]
    Task<VanillaVersionsResultInfo> ListVanillaVersionsAsync() =>
        FeatureRequest.InvokeAsync<VanillaVersionsResultInfo>(this, nameof(ListVanillaVersionsAsync), new
        {
        });

    Task InstallVanillaAsync(string version);

    [FeatureOperation]
    Task<LoaderListResultInfo> ListLoadersAsync(string version, string loader) =>
        FeatureRequest.InvokeAsync<LoaderListResultInfo>(this, nameof(ListLoadersAsync), new
        {
            version,
            loader
        });

    Task InstallLoaderAsync(string version, string loader, string loaderVersion);

    [FeatureOperation]
    Task<PackPreviewResultInfo> PreviewPackAsync(string path) =>
        FeatureRequest.InvokeAsync<PackPreviewResultInfo>(this, nameof(PreviewPackAsync), new
        {
            path
        });

    Task ConfirmPackAsync(string token, string name, string directory);

    [FeatureOperation]
    Task<LocalImportPreviewInfo> PreviewLocalImportAsync(string path, string instance, string resourceKind = "") =>
        FeatureRequest.InvokeAsync<LocalImportPreviewInfo>(this, nameof(PreviewLocalImportAsync), new
        {
            path,
            instance,
            resourceKind
        });

    [FeatureOperation]
    Task<SavedInfo> ConfirmLocalImportAsync(string token) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(ConfirmLocalImportAsync), new
        {
            token
        });
}
