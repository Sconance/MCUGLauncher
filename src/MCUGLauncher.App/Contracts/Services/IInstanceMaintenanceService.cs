namespace MCUGLauncher.Contracts;

/// <summary>实例克隆、导出、卸载、模组与内容更新。</summary>
public interface IInstanceMaintenanceService
{
    Task CloneInstanceAsync(string version, string name, bool saves = false, bool servers = false);
    Task ExportInstanceAsync(string version, string path, bool servers = false, bool saves = false);

    [FeatureOperation]
    Task<UninstallPreviewInfo> PreviewUninstallAsync(string version) =>
        FeatureRequest.InvokeAsync<UninstallPreviewInfo>(this, nameof(PreviewUninstallAsync), new
        {
            version
        });

    [FeatureOperation]
    Task<UninstallPreviewInfo> PreviewDeleteVersionAsync(string version) =>
        FeatureRequest.InvokeAsync<UninstallPreviewInfo>(this, nameof(PreviewDeleteVersionAsync), new
        {
            version
        });

    Task ApplyUninstallAsync(string token);

    [FeatureOperation]
    Task<SavedInfo> DeleteModAsync(string file) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(DeleteModAsync), new
        {
            file
        });

    [FeatureOperation]
    Task<SavedInfo> ToggleModAsync(string file, bool enabled) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(ToggleModAsync), new
        {
            file,
            enabled
        });

    [FeatureOperation]
    Task<ContentUpdatesInfo> CheckUpdatesAsync(string version) =>
        FeatureRequest.InvokeAsync<ContentUpdatesInfo>(this, nameof(CheckUpdatesAsync), new
        {
            version
        });

    [FeatureOperation]
    Task<ContentUpdatePreviewInfo> PreviewUpdatesAsync(string token, IReadOnlyList<string> projects) =>
        FeatureRequest.InvokeAsync<ContentUpdatePreviewInfo>(
            this,
            nameof(PreviewUpdatesAsync),
            new
            {
                token,
                projects
            });

    [FeatureOperation]
    Task<SavedInfo> ApplyUpdatesAsync(string token) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(ApplyUpdatesAsync), new
        {
            token
        });
}
