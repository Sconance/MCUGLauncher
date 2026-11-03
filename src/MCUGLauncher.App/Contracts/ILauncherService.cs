namespace MCUGLauncher.Contracts;

/// <summary>Only business entry for native clients. Protocol and mutable configuration stay internal.</summary>
public interface ILauncherService : IDisposable
{
    event Action<LauncherState>? StateChanged;
    event Action<TaskInfo[]>? TasksChanged;
    event Action<GameInfo[]>? GamesChanged;
    event Action<IReadOnlyDictionary<string, CommunityEntry[]>>? CommunityChanged;
    event Action<AvatarInfo>? AvatarReceived;
    event Action<AccountSwitchResultInfo>? AccountSwitchResultReceived;
    event Action<MuaProfilesInfo>? MuaProfilesReceived;
    event Action<ServerPingInfo>? ServerPingReceived;
    event Action<TestResultInfo>? TestResultReceived;
    event Action<SaveBackupInfo[]>? SaveBackupsChanged;
    event Action<string>? FeedbackReceived;
    LauncherState State
    {
        get;
    }
    TaskInfo[] Tasks
    {
        get;
    }
    GameInfo[] Games
    {
        get;
    }
    IReadOnlyDictionary<string, CommunityEntry[]> Community
    {
        get;
    }
    WindowPlacement WindowPlacement
    {
        get;
    }
    void RememberWindow(double width, double height, bool maximized);
    Task<byte[]> DownloadImageAsync(string url);
    Task<OperationReceipt> SaveCommunityAsync(string feed, CommunityEntry[] items);
    Task StartAsync();
    Task ReadAvatarAsync();
    Task<AuthResultInfo> LoginMicrosoftAsync();
    Task<AuthResultInfo> LoginMuaAsync(string account = "", string password = "");
    Task<OperationReceipt> SaveSettingsAsync(AppSettingsPatch patch);
    Task SetOfflineSkinAsync(string path = "");
    Task SelectMuaProfileAsync(string token = "", string profileId = "");
    Task ResetWindowAsync();
    Task<KeyResultInfo> VerifyKeyAsync(string role = "", string key = "");
    Task ForgetAccountAsync(string account = "");
    Task RefreshCommunityAsync();
    Task PingServerAsync(string address = "", string id = "");
    Task DeleteServerAsync(string address = "");
    Task<ContentListInfo> SearchContentAsync(string category = "", string query = "", string game = "", string loader = "", string requestId = "");
    Task ToggleBookmarkAsync(string id = "", string kind = "", string name = "", string description = "", string author = "");
    Task<OperationReceipt> PublishAsync(string title = "", string body = "", string expiresAt = "", string imageData = "");
    Task<OperationReceipt> SaveRecommendationsAsync(CommunityEntry[] items);
    Task<OperationReceipt> SaveAnnouncementsAsync(CommunityEntry[] items);
    Task<LocalImportPreviewInfo> PreviewLocalImportAsync(string path = "", string instance = "", string resourceKind = "");
    Task<OperationReceipt> ConfirmLocalImportAsync(string token = "");
    Task<ServerResultInfo> SaveServerAsync(string name = "", string address = "", string oldAddress = "");
    Task SwitchAccountModeAsync(string account = "");
    Task LaunchAsync(string version = "");
    /// <summary>启动指定实例并尝试直连；启动结果沿用 FeedbackReceived，不代表已连接服务器。</summary>
    Task LaunchServerAsync(ServerLaunchInfo request);
    Task<PackPreviewInfo> PreviewPackAsync(string path = "");
    Task<OperationReceipt> ConfirmPackAsync(string token = "", string name = "", string directory = "");
    Task<OperationReceipt> InstallLoaderAsync(string version = "", string loader = "", string loaderVersion = "");
    Task<LoaderListInfo> ListLoadersAsync(string version = "", string loader = "", string requestId = "");
    Task RequestStateAsync();
    Task SelectVersionAsync(string version = "");
    Task SelectInstanceAsync(string instance = "");
    Task CloneInstanceAsync(string version = "", string name = "", bool saves = false, bool servers = false);
    Task ExportInstanceAsync(string version = "", string path = "", bool saves = false, bool servers = false);
    Task OpenFolderAsync(string kind = "");
    Task ToggleModAsync(string file = "", bool enabled = false);
    Task DeleteModAsync(string file = "");
    Task<JavaListInfo> ScanJavaAsync();
    Task<LaunchOptionsInfo> ReadLaunchOptionsAsync(string version = "", string requestId = "");
    Task<OperationReceipt> SaveLaunchOptionsAsync(string token, string name, bool inherit, LaunchOptionsSettings settings);
    Task<UninstallPreviewInfo> PreviewUninstallAsync(string version = "");
    Task<OperationReceipt> ApplyUninstallAsync(string token = "");
    Task<ContentUpdatesInfo> CheckUpdatesAsync(string version = "");
    Task<ContentUpdatePreviewInfo> PreviewUpdatesAsync(string token, string[] projects);
    Task<OperationReceipt> ApplyUpdatesAsync(string token = "");
    Task<OperationReceipt> QueueContentVersionAsync(string token = "", string versionId = "", string mode = "", string path = "");
    Task<ResourceVersionsInfo> ListContentVersionsAsync(string id = "", string category = "", string instance = "", string game = "", string loader = "", string requestId = "");
    Task BackupSavesAsync();
    Task ListSaveBackupsAsync();
    Task RestoreSaveAsync(string id = "", string world = "");
    Task UseDataSourcesAsync(string mode = "");
    Task SaveCustomSourcesAsync(DataSourceSet dataSources);
    Task TestSourceAsync(string name = "", string url = "", string requestId = "");
    Task RequestTasksAsync();
    Task TaskActionAsync(string id = "", string action = "");
    Task RequestGamesAsync();
    Task StopGameAsync(string id = "");
    Task<LogTextInfo> ReadLogAsync();
    Task<SystemInfoInfo> SystemInfoAsync();
    Task<VanillaListInfo> ListVanillaVersionsAsync();
    Task InstallVanillaAsync(string version = "");
}
