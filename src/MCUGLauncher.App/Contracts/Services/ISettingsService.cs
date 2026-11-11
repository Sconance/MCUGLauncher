namespace MCUGLauncher.Contracts;

/// <summary>全局设置、Java 扫描与数据源。</summary>
public interface ISettingsService
{
    IReadOnlyList<JavaInfo> JavaList
    {
        get;
    }

    [FeatureOperation]
    Task<IReadOnlyList<JavaInfo>> ScanJavaAsync() =>
        FeatureRequest.InvokeAsync<IReadOnlyList<JavaInfo>>(this, nameof(ScanJavaAsync), new
        {
        });

    Task<SavedInfo> SaveSettingsAsync(string root, string java, int ram, string? player = null);

    [FeatureOperation]
    Task<SavedInfo> SaveSettingsAsync(AppSettingsPatch patch) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(SaveSettingsAsync), patch);

    [FeatureOperation]
    Task<TestResultInfo> TestSourceAsync(string name, string url) =>
        FeatureRequest.InvokeAsync<TestResultInfo>(
            this,
            nameof(TestSourceAsync),
            new
            {
                name,
                url
            });

    [FeatureOperation]
    Task<SavedInfo> SaveCustomSourcesAsync(DataSourceSet sources) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(SaveCustomSourcesAsync), new
        {
            dataSources = sources
        });

    [FeatureOperation]
    Task<SavedInfo> UseDataSourcesAsync(string mode) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(UseDataSourcesAsync), new
        {
            mode
        });

    [FeatureOperation]
    Task<SavedInfo> ResetDataSourcesAsync() =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(ResetDataSourcesAsync), new
        {
        });
}
