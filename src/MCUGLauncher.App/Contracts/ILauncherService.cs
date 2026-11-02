namespace MCUGLauncher.Contracts;

/// <summary>
/// UI 与业务之间的唯一入口。所有操作都是强类型调用，状态与事件使用共享 DTO；
/// 页面不得依赖内部命令协议、配置对象或 App 内部实现。
/// </summary>
public interface ILauncherService : IDisposable
{
    event Action<LauncherState>? StateChanged;
    event Action<IReadOnlyList<TaskInfo>>? TasksChanged;
    event Action<IReadOnlyList<ContentItem>>? ContentChanged;
    event Action<IReadOnlyList<GameInfo>>? GamesChanged;
    event Action<IReadOnlyList<JavaInfo>>? JavaListChanged;
    event Action<IReadOnlyList<SaveBackupInfo>>? SaveBackupsChanged;
    event Action<string>? LogTextReceived;
    event Action<SystemInfoInfo>? SystemInfoReceived;
    event Action<AuthResultInfo>? AuthResultReceived;
    event Action<AccountStateInfo>? AccountStateChanged;
    event Action<AccountSwitchResultInfo>? AccountSwitchResultReceived;
    event Action<PickedPathInfo>? PathPicked;
    event Action<ToastInfo>? ToastReceived;
    event Action<string>? ErrorReceived;
    event Action<LaunchResultInfo>? LaunchResultReceived;
    event Action<UninstallPreviewInfo>? UninstallPreviewReceived;
    event Action<ContentUpdatesInfo>? ContentUpdatesReceived;
    event Action<ContentUpdatePreviewInfo>? ContentUpdatePreviewReceived;
    event Action<ServerPingInfo>? ServerPingReceived;
    event Action<ServerResultInfo>? ServerResultReceived;
    event Action<TestResultInfo>? TestResultReceived;
    event Action<KeyResultInfo>? KeyResultReceived;
    event Action<CommunityPayload>? CommunityReceived;
    event Action<CommunityData>? CommunityChanged;
    event Action<AvatarInfo>? AvatarReceived;
    event Action<LoaderListInfo>? LoaderListReceived;
    event Action<VanillaVersionInfo[]>? VanillaListReceived;
    event Action<ResourceVersionsInfo>? ResourceVersionsReceived;
    event Action<PackPreviewInfo>? PackPreviewReceived;
    event Action<ContentErrorInfo>? ContentErrorReceived;
    event Action<MuaProfilesInfo>? MuaProfilesReceived;
    event Action<LaunchOptionsInfo>? LaunchOptionsReceived;
    event Action? LaunchOptionsSaved;
    event Action<PublishResultInfo>? PublishResultReceived;
    event Action<SavedInfo>? SavedReceived;
    event Action? SettingsSaved;

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

    Task LaunchAsync(string version);
    Task RequestStateAsync();
    Task SelectInstanceAsync(string instance);
    Task SelectVersionAsync(string version);
    Task ScanJavaAsync();
    Task SaveSettingsAsync(string root, string java, int ram, string? player = null);
    Task SaveSettingsAsync(AppSettingsPatch patch);
    Task RequestTasksAsync();
    Task TaskActionAsync(string id, string action);
    Task LoginMicrosoftAsync();
    Task LoginMuaAsync(string account, string password);
    Task SwitchAccountModeAsync(string mode);
    Task ForgetAccountAsync(string mode);
    Task LogoutAccountAsync();
    Task SearchContentAsync(string category, string query, string requestId = "");
    Task InstallContentAsync(string id, string category, string versionId, string mode = "instance", string path = "");
    Task InstallModAsync(string id, string versionId, string mode, string path = "");
    Task ListContentVersionsAsync(string id, string category, string instance = "", string game = "", string loader = "", string requestId = "");
    Task QueueContentVersionAsync(string token, string versionId, string mode = "instance", string path = "");
    Task ListVanillaVersionsAsync();
    Task InstallVanillaAsync(string version);
    Task ListLoadersAsync(string version, string loader);
    Task InstallLoaderAsync(string version, string loader, string loaderVersion);
    Task PreviewPackAsync(string path);
    Task ConfirmPackAsync(string token, string name, string directory);
    Task CloneInstanceAsync(string version, string name, bool saves = false, bool servers = false);
    Task ExportInstanceAsync(string version, string path, bool servers = false, bool saves = false);
    Task PreviewUninstallAsync(string version);
    Task PreviewDeleteVersionAsync(string version);
    Task ApplyUninstallAsync(string token);
    Task ToggleModAsync(string file, bool enabled);
    Task DeleteModAsync(string file);
    Task CheckUpdatesAsync(string version);
    Task PreviewUpdatesAsync(string token, IReadOnlyList<string> projects);
    Task ApplyUpdatesAsync(string token);
    Task RequestGamesAsync();
    Task StopGameAsync(string id);
    Task BackupSavesAsync();
    Task ListSaveBackupsAsync();
    Task RestoreSaveAsync(string id, string world);
    Task OpenFolderAsync(string kind);
    Task ReadLogAsync();
    Task SystemInfoAsync();
    Task SaveServerAsync(string name, string address, string oldAddress = "");
    Task DeleteServerAsync(string address);
    Task PingServerAsync(string address, string id);
    Task TestSourceAsync(string name, string url, string requestId = "");
    Task SaveCustomSourcesAsync(DataSourceSet sources);
    Task UseDataSourcesAsync(string mode);
    Task ResetDataSourcesAsync();
    Task VerifyKeyAsync(string role, string key);
    Task RefreshCommunityAsync();
    Task PublishAsync(string title, string body, string expiresAt, string imageData = "");
    Task SaveAnnouncementsAsync(IReadOnlyList<AnnouncementInfo> items);
    Task SaveRecommendationsAsync(IReadOnlyList<RecommendationInfo> items);
    Task ToggleBookmarkAsync(string id, string name, string description, string author, string kind);
    Task<byte[]> DownloadImageAsync(string url);
    Task ReadAvatarAsync();
    Task SetOfflineSkinAsync(string path);
    Task SelectMuaProfileAsync(string token, string profileId);
    Task PickFolderAsync(string target);
    Task PickFileAsync(string target);
    Task PickSaveFileAsync(string target);
    Task ResetWindowAsync();
    Task SaveLaunchOptionsAsync(string token, string version, string name, LaunchOptionsSettings settings, bool inherit);
    Task ReadLaunchOptionsAsync(string version, string requestId = "");
}
