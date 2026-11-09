using System.Text.Json;

namespace MCUGLauncher.Contracts;

/// <summary>Marks an interface operation that returns its result directly as Task&lt;T&gt;.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class FeatureOperationAttribute : Attribute
{
}

/// <summary>Associates a feature handler with a marked service operation.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class FeatureHandlerAttribute(string operation) : Attribute
{
    public string Operation { get; } = operation;
}

internal interface IFeatureRequestInvoker
{
    Task<TResponse> InvokeFeatureAsync<TResponse>(string operation, object arguments);
}

internal interface IFeatureHandler<TResponse>
{
    Task<TResponse> HandleAsync(JsonElement arguments, CancellationToken token);
}

internal static class FeatureRequest
{
    public static Task<TResponse> InvokeAsync<TResponse>(
        ILauncherService service,
        string operation,
        object arguments) =>
        ((IFeatureRequestInvoker)service).InvokeFeatureAsync<TResponse>(operation, arguments);
}

/// <summary>
/// UI 与业务之间的唯一入口。所有操作都是强类型调用，状态与事件使用共享 DTO；
/// 页面不得依赖内部命令协议、配置对象或 App 内部实现。
/// </summary>
public interface ILauncherService : IDisposable
{
    event Action<LauncherState>? StateChanged;
    event Action<IReadOnlyList<TaskInfo>>? TasksChanged;
    event Action<IReadOnlyList<GameInfo>>? GamesChanged;
    event Action<AuthResultInfo>? AuthResultReceived;
    event Action<AccountStateInfo>? AccountStateChanged;
    event Action<AccountSwitchResultInfo>? AccountSwitchResultReceived;
    event Action<PickedPathInfo>? PathPicked;
    event Action<ToastInfo>? ToastReceived;
    event Action<string>? ErrorReceived;
    event Action<CommunityPayload>? CommunityReceived;
    event Action<CommunityData>? CommunityChanged;
    event Action<ContentErrorInfo>? ContentErrorReceived;
    event Action<MuaProfilesInfo>? MuaProfilesReceived;

    WindowPlacement WindowPlacement
    {
        get;
    }
    LauncherState? State
    {
        get;
    }
    IReadOnlyList<TaskInfo> Tasks
    {
        get;
    }
    IReadOnlyList<ContentItem> ContentItems
    {
        get;
    }
    IReadOnlyList<GameInfo> Games
    {
        get;
    }
    IReadOnlyList<JavaInfo> JavaList
    {
        get;
    }
    IReadOnlyList<SaveBackupInfo> SaveBackups
    {
        get;
    }
    CommunityData Community
    {
        get;
    }

    Task StartAsync();
    void RememberWindow(double width, double height, bool maximized);

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
    Task RequestStateAsync();
    [FeatureOperation]
    Task<IReadOnlyList<GameDirectoryInfo>> ScanGameDirectoriesAsync() =>
        FeatureRequest.InvokeAsync<IReadOnlyList<GameDirectoryInfo>>(this, nameof(ScanGameDirectoriesAsync), new
        {
        });
    [FeatureOperation]
    Task<SavedInfo> AddGameDirectoryAsync(string root, bool select = false) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(AddGameDirectoryAsync), new
        {
            root,
            select
        });
    [FeatureOperation]
    Task<SavedInfo> RemoveGameDirectoryAsync(string root) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(RemoveGameDirectoryAsync), new
        {
            root
        });
    [FeatureOperation]
    Task<SavedInfo> SelectManagedInstanceAsync(InstanceTarget target) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(SelectManagedInstanceAsync), new
        {
            target
        });
    [FeatureOperation]
    Task<LaunchResultInfo> LaunchManagedInstanceAsync(InstanceTarget target, string? serverAddress = null) =>
        FeatureRequest.InvokeAsync<LaunchResultInfo>(this, nameof(LaunchManagedInstanceAsync), new
        {
            target,
            serverAddress
        });
    [FeatureOperation]
    Task<SavedInfo> CloneManagedInstanceAsync(InstanceTarget target, string name, bool saves = false, bool servers = false) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(CloneManagedInstanceAsync), new
        {
            target,
            name,
            saves,
            servers
        });
    [FeatureOperation]
    Task<SavedInfo> ExportManagedInstanceAsync(InstanceTarget target, string path, bool servers = false, bool saves = false) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(ExportManagedInstanceAsync), new
        {
            target,
            path,
            servers,
            saves
        });
    [FeatureOperation]
    Task<UninstallPreviewInfo> PreviewManagedUninstallAsync(InstanceTarget target) =>
        FeatureRequest.InvokeAsync<UninstallPreviewInfo>(this, nameof(PreviewManagedUninstallAsync), new
        {
            target
        });
    [FeatureOperation]
    Task<IReadOnlyList<ModInfo>> ListManagedModsAsync(InstanceTarget target) =>
        FeatureRequest.InvokeAsync<IReadOnlyList<ModInfo>>(this, nameof(ListManagedModsAsync), new
        {
            target
        });
    [FeatureOperation]
    Task<SavedInfo> ToggleManagedModAsync(InstanceTarget target, string file, bool enabled) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(ToggleManagedModAsync), new
        {
            target,
            file,
            enabled
        });
    [FeatureOperation]
    Task<SavedInfo> DeleteManagedModAsync(InstanceTarget target, string file) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(DeleteManagedModAsync), new
        {
            target,
            file
        });
    Task SelectInstanceAsync(string instance);
    Task SelectVersionAsync(string version);
    [FeatureOperation]
    Task<IReadOnlyList<JavaInfo>> ScanJavaAsync() =>
        FeatureRequest.InvokeAsync<IReadOnlyList<JavaInfo>>(this, nameof(ScanJavaAsync), new
        {
        });
    Task<SavedInfo> SaveSettingsAsync(string root, string java, int ram, string? player = null);
    [FeatureOperation]
    Task<SavedInfo> SaveSettingsAsync(AppSettingsPatch patch) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(SaveSettingsAsync), patch);
    Task RequestTasksAsync();
    Task TaskActionAsync(string id, string action);
    [FeatureOperation]
    Task<AuthResultInfo> LoginMicrosoftAsync() =>
        FeatureRequest.InvokeAsync<AuthResultInfo>(this, nameof(LoginMicrosoftAsync), new
        {
        });
    [FeatureOperation]
    Task<AuthResultInfo> LoginMuaAsync(string account, string password) =>
        FeatureRequest.InvokeAsync<AuthResultInfo>(this, nameof(LoginMuaAsync), new
        {
            account,
            password
        });
    [FeatureOperation]
    Task<AccountSwitchResultInfo> SwitchAccountModeAsync(string mode) =>
        FeatureRequest.InvokeAsync<AccountSwitchResultInfo>(this, nameof(SwitchAccountModeAsync), new
        {
            mode
        });
    [FeatureOperation]
    Task<AuthResultInfo> ForgetAccountAsync(string mode) =>
        FeatureRequest.InvokeAsync<AuthResultInfo>(this, nameof(ForgetAccountAsync), new
        {
            mode
        });
    [FeatureOperation]
    Task<AuthResultInfo> LogoutAccountAsync() =>
        FeatureRequest.InvokeAsync<AuthResultInfo>(this, nameof(LogoutAccountAsync), new
        {
        });
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
    Task RequestGamesAsync();
    Task StopGameAsync(string id);
    [FeatureOperation]
    Task<SavedInfo> BackupSavesAsync() =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(BackupSavesAsync), new
        {
        });
    [FeatureOperation]
    Task<IReadOnlyList<SaveBackupInfo>> ListSaveBackupsAsync() =>
        FeatureRequest.InvokeAsync<IReadOnlyList<SaveBackupInfo>>(this, nameof(ListSaveBackupsAsync), new
        {
        });
    [FeatureOperation]
    Task<SavedInfo> RestoreSaveAsync(string id, string world) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(RestoreSaveAsync), new
        {
            id,
            world
        });
    Task OpenFolderAsync(string kind);
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
    /// <summary>保存服务器收藏；instance 为 null 时保留绑定，空字符串时清除绑定。</summary>
    [FeatureOperation]
    Task<ServerResultInfo> SaveServerAsync(string name, string address, string oldAddress = "", string? instance = null, string? gameRoot = null) =>
        FeatureRequest.InvokeAsync<ServerResultInfo>(
            this,
            nameof(SaveServerAsync),
            new
            {
                name,
                address,
                oldAddress,
                instance,
                gameRoot
            });
    [FeatureOperation]
    Task<SavedInfo> DeleteServerAsync(string address) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(DeleteServerAsync), new
        {
            address
        });
    [FeatureOperation]
    Task<ServerPingInfo> PingServerAsync(string address, string id) =>
        FeatureRequest.InvokeAsync<ServerPingInfo>(this, nameof(PingServerAsync), new
        {
            address,
            id
        });
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
    [FeatureOperation]
    Task<KeyResultInfo> VerifyKeyAsync(string role, string key) =>
        FeatureRequest.InvokeAsync<KeyResultInfo>(this, nameof(VerifyKeyAsync), new
        {
            role,
            key
        });
    Task RefreshCommunityAsync();
    [FeatureOperation]
    Task<SavedInfo> SaveRecommendationsAsync(IReadOnlyList<RecommendationInfo> items) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(SaveRecommendationsAsync), new
        {
            items = items.Select(item => item.Raw).ToArray()
        });
    [FeatureOperation]
    Task<SavedInfo> ToggleBookmarkAsync(string id, string name, string description, string author, string kind) =>
        FeatureRequest.InvokeAsync<SavedInfo>(
            this,
            nameof(ToggleBookmarkAsync),
            new
            {
                id,
                name,
                description,
                author,
                kind
            });
    [FeatureOperation]
    Task<byte[]> DownloadImageAsync(string url) =>
        FeatureRequest.InvokeAsync<byte[]>(this, nameof(DownloadImageAsync), new
        {
            url
        });
    [FeatureOperation]
    Task<AvatarInfo?> ReadAvatarAsync() =>
        FeatureRequest.InvokeAsync<AvatarInfo?>(this, nameof(ReadAvatarAsync), new
        {
        });
    [FeatureOperation]
    Task<AvatarInfo?> SetOfflineSkinAsync(string path) =>
        FeatureRequest.InvokeAsync<AvatarInfo?>(this, nameof(SetOfflineSkinAsync), new
        {
            path
        });
    Task SelectMuaProfileAsync(string token, string profileId);
    Task PickFolderAsync(string target);
    Task PickFileAsync(string target);
    Task PickSaveFileAsync(string target);
    Task ResetWindowAsync();
    [FeatureOperation]
    Task<SavedInfo> SaveLaunchOptionsAsync(string token, string version, string name, LaunchOptionsSettings settings, bool inherit) =>
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
}
