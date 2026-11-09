using System.Text.Json;
using MCUGLauncher.Contracts;
using MCUGLauncher.Services;

namespace MCUGLauncher.UseCases;

/// <summary>
/// 应用服务：对 UI 暴露统一类型化接口，内部命令协议与桌面适配保持隐藏。
/// </summary>
public sealed class LauncherService : ILauncherService, IFeatureRequestInvoker
{
    static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    readonly LauncherApplication application;
    readonly Action<Action> dispatch;

    public event Action<LauncherState>? StateChanged;
    public event Action<IReadOnlyList<TaskInfo>>? TasksChanged;
    public event Action<IReadOnlyList<GameInfo>>? GamesChanged;
    public event Action<AuthResultInfo>? AuthResultReceived;
    public event Action<AccountStateInfo>? AccountStateChanged;
    public event Action<AccountSwitchResultInfo>? AccountSwitchResultReceived;
    public event Action<PickedPathInfo>? PathPicked;
    public event Action<ToastInfo>? ToastReceived;
    public event Action<string>? ErrorReceived;
    public event Action<CommunityPayload>? CommunityReceived;
    public event Action<CommunityData>? CommunityChanged;
    public event Action<ContentErrorInfo>? ContentErrorReceived;
    public event Action<MuaProfilesInfo>? MuaProfilesReceived;

    public WindowPlacement WindowPlacement => new(
        application.Config.WindowWidth,
        application.Config.WindowHeight,
        application.Config.WindowMaximized);

    public LauncherState? State
    {
        get; private set;
    }
    public IReadOnlyList<TaskInfo> Tasks { get; private set; } = Array.Empty<TaskInfo>();
    public IReadOnlyList<ContentItem> ContentItems { get; private set; } = Array.Empty<ContentItem>();
    public IReadOnlyList<GameInfo> Games { get; private set; } = Array.Empty<GameInfo>();
    public IReadOnlyList<JavaInfo> JavaList { get; private set; } = Array.Empty<JavaInfo>();
    public IReadOnlyList<SaveBackupInfo> SaveBackups { get; private set; } = Array.Empty<SaveBackupInfo>();
    public CommunityData Community { get; private set; } = CommunityData.Empty;

    public LauncherService(
        IDesktopHost desktop,
        Action<Action> dispatch,
        Func<string, XboxAuthNet.OAuth.CodeFlow.IWebUI> browser,
        Func<byte[]?, string> avatarEncoder,
        ConfigStore? store = null)
    {
        this.dispatch = dispatch;
        application = new LauncherApplication(
            Send,
            desktop,
            store,
            microsoftBrowser: browser,
            avatarEncoder: avatarEncoder);
    }

    public Task StartAsync() => application.HandleAsync("""{"type":"ready"}""");

    public void RememberWindow(double width, double height, bool maximized) =>
        application.RememberWindow(width, height, maximized);

    public Task<LaunchResultInfo> LaunchAsync(string version) =>
        FeatureRequest.InvokeAsync<LaunchResultInfo>(this, nameof(LaunchAsync), new
        {
            version
        });

    /// <summary>使用明确的实例和服务器地址启动；不永久修改实例启动设置。</summary>
    public Task<LaunchResultInfo> LaunchToServerAsync(string instance, string serverAddress) =>
        FeatureRequest.InvokeAsync<LaunchResultInfo>(this, nameof(LaunchToServerAsync), new
        {
            instance,
            serverAddress
        });

    public Task RequestStateAsync() => application.HandleAsync("""{"type":"requestState"}""");

    public Task SelectInstanceAsync(string instance) => CommandAsync(new { type = "selectInstance", instance });

    public Task SelectVersionAsync(string version) => CommandAsync(new { type = "selectVersion", version });

    public async Task<IReadOnlyList<JavaInfo>> ScanJavaAsync()
    {
        JavaList = await FeatureRequest.InvokeAsync<IReadOnlyList<JavaInfo>>(this, nameof(ScanJavaAsync), new
        {
        });
        return JavaList;
    }

    public Task<SavedInfo> SaveSettingsAsync(string root, string java, int ram, string? player = null) =>
        SaveSettingsAsync(new AppSettingsPatch(root, java, ram, Player: player));

    public Task<SavedInfo> SaveSettingsAsync(AppSettingsPatch patch)
    {
        var payload = new Dictionary<string, object> { ["type"] = "saveSettings" };
        if (patch.Root is not null)
            payload["root"] = patch.Root;
        if (patch.Java is not null)
            payload["java"] = patch.Java;
        if (patch.Ram is { } ram)
            payload["ram"] = ram;
        if (patch.Width is { } width)
            payload["width"] = width;
        if (patch.Height is { } height)
            payload["height"] = height;
        if (patch.AutoMemory is { } autoMemory)
            payload["autoMemory"] = autoMemory;
        if (patch.Anonymous is { } anonymous)
            payload["anonymous"] = anonymous;
        if (patch.Player is not null)
            payload["player"] = patch.Player;
        if (patch.DataSources is { } sources)
            payload["dataSources"] = new Dictionary<string, string>
            {
                ["announcements"] = sources.Announcements,
                ["servers"] = sources.Servers,
                ["recommendations"] = sources.Recommendations,
                ["quotes"] = sources.Quotes
            };
        return FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(SaveSettingsAsync), payload);
    }

    public Task RequestTasksAsync() => application.HandleAsync("""{"type":"requestTasks"}""");

    public Task TaskActionAsync(string id, string action) => CommandAsync(new { type = "taskAction", id, action });

    public Task<AuthResultInfo> LoginMicrosoftAsync() =>
        FeatureRequest.InvokeAsync<AuthResultInfo>(this, nameof(LoginMicrosoftAsync), new
        {
        });

    public Task<AuthResultInfo> LoginMuaAsync(string account, string password) =>
        FeatureRequest.InvokeAsync<AuthResultInfo>(this, nameof(LoginMuaAsync), new
        {
            account,
            password
        });

    public Task<AccountSwitchResultInfo> SwitchAccountModeAsync(string mode) =>
        FeatureRequest.InvokeAsync<AccountSwitchResultInfo>(this, nameof(SwitchAccountModeAsync), new
        {
            mode
        });

    public Task<AuthResultInfo> ForgetAccountAsync(string mode) =>
        FeatureRequest.InvokeAsync<AuthResultInfo>(this, nameof(ForgetAccountAsync), new
        {
            mode
        });

    public Task<AuthResultInfo> LogoutAccountAsync() =>
        FeatureRequest.InvokeAsync<AuthResultInfo>(this, nameof(LogoutAccountAsync), new
        {
        });

    public async Task<ContentSearchResultInfo> SearchContentAsync(string category, string query, string requestId = "")
    {
        var result = await FeatureRequest.InvokeAsync<ContentSearchResultInfo>(
            this,
            nameof(SearchContentAsync),
            new
            {
                category,
                query,
                requestId
            });
        ContentItems = result.Items;
        return result;
    }

    public Task InstallContentAsync(
        string id,
        string category,
        string versionId,
        string mode = "instance",
        string path = "") =>
        CommandAsync(new
        {
            type = "installContent",
            id,
            category,
            versionId,
            mode,
            path
        });

    public Task InstallModAsync(string id, string versionId, string mode, string path = "") =>
        CommandAsync(new
        {
            type = "installMod",
            id,
            versionId,
            mode,
            path
        });

    public Task<ResourceVersionsResultInfo> ListContentVersionsAsync(
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

    public Task QueueContentVersionAsync(string token, string versionId, string mode = "instance", string path = "") =>
        CommandAsync(new
        {
            type = "queueContentVersion",
            token,
            versionId,
            mode,
            path
        });

    public Task<VanillaVersionsResultInfo> ListVanillaVersionsAsync() =>
        FeatureRequest.InvokeAsync<VanillaVersionsResultInfo>(this, nameof(ListVanillaVersionsAsync), new
        {
        });

    public Task InstallVanillaAsync(string version) => CommandAsync(new { type = "installVanilla", version });

    public Task<LoaderListResultInfo> ListLoadersAsync(string version, string loader) =>
        FeatureRequest.InvokeAsync<LoaderListResultInfo>(this, nameof(ListLoadersAsync), new
        {
            version,
            loader
        });

    public Task InstallLoaderAsync(string version, string loader, string loaderVersion) =>
        CommandAsync(new
        {
            type = "installLoader",
            version,
            loader,
            loaderVersion
        });

    public Task<PackPreviewResultInfo> PreviewPackAsync(string path) =>
        FeatureRequest.InvokeAsync<PackPreviewResultInfo>(this, nameof(PreviewPackAsync), new
        {
            path
        });

    public Task ConfirmPackAsync(string token, string name, string directory) =>
        CommandAsync(new
        {
            type = "confirmPack",
            token,
            name,
            directory
        });

    public Task CloneInstanceAsync(string version, string name, bool saves = false, bool servers = false) =>
        CommandAsync(new
        {
            type = "cloneInstance",
            version,
            name,
            saves,
            servers
        });

    public Task ExportInstanceAsync(string version, string path, bool servers = false, bool saves = false) =>
        CommandAsync(new
        {
            type = "exportInstance",
            version,
            path,
            servers,
            saves
        });

    public Task<UninstallPreviewInfo> PreviewUninstallAsync(string version) =>
        FeatureRequest.InvokeAsync<UninstallPreviewInfo>(this, nameof(PreviewUninstallAsync), new
        {
            version
        });

    public Task<UninstallPreviewInfo> PreviewDeleteVersionAsync(string version) =>
        FeatureRequest.InvokeAsync<UninstallPreviewInfo>(this, nameof(PreviewDeleteVersionAsync), new
        {
            version
        });

    public Task ApplyUninstallAsync(string token) => CommandAsync(new { type = "applyUninstall", token });

    public Task<SavedInfo> ToggleModAsync(string file, bool enabled) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(ToggleModAsync), new
        {
            file,
            enabled
        });

    public Task<SavedInfo> DeleteModAsync(string file) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(DeleteModAsync), new
        {
            file
        });

    public Task<ContentUpdatesInfo> CheckUpdatesAsync(string version) =>
        FeatureRequest.InvokeAsync<ContentUpdatesInfo>(this, nameof(CheckUpdatesAsync), new
        {
            version
        });

    public Task<ContentUpdatePreviewInfo> PreviewUpdatesAsync(string token, IReadOnlyList<string> projects) =>
        FeatureRequest.InvokeAsync<ContentUpdatePreviewInfo>(
            this,
            nameof(PreviewUpdatesAsync),
            new
            {
                token,
                projects
            });

    public Task<SavedInfo> ApplyUpdatesAsync(string token) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(ApplyUpdatesAsync), new
        {
            token
        });

    public Task RequestGamesAsync() => application.HandleAsync("""{"type":"requestGames"}""");

    public Task StopGameAsync(string id) => CommandAsync(new { type = "stopGame", id });

    public Task<SavedInfo> BackupSavesAsync() =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(BackupSavesAsync), new
        {
        });

    public async Task<IReadOnlyList<SaveBackupInfo>> ListSaveBackupsAsync()
    {
        SaveBackups = await FeatureRequest.InvokeAsync<IReadOnlyList<SaveBackupInfo>>(
            this,
            nameof(ListSaveBackupsAsync),
            new
            {
            });
        return SaveBackups;
    }

    public Task<SavedInfo> RestoreSaveAsync(string id, string world) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(RestoreSaveAsync), new
        {
            id,
            world
        });

    public Task OpenFolderAsync(string kind) => CommandAsync(new { type = "openFolder", kind });

    public Task<string> ReadLogAsync() =>
        FeatureRequest.InvokeAsync<string>(this, nameof(ReadLogAsync), new
        {
        });

    public Task<SystemInfoInfo> SystemInfoAsync() =>
        FeatureRequest.InvokeAsync<SystemInfoInfo>(this, nameof(SystemInfoAsync), new
        {
        });

    public Task<ServerResultInfo> SaveServerAsync(string name, string address, string oldAddress = "", string? instance = null, string? gameRoot = null) =>
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

    public Task<SavedInfo> DeleteServerAsync(string address) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(DeleteServerAsync), new
        {
            address
        });

    public Task<ServerPingInfo> PingServerAsync(string address, string id) =>
        FeatureRequest.InvokeAsync<ServerPingInfo>(this, nameof(PingServerAsync), new
        {
            address,
            id
        });

    public Task<SavedInfo> SaveCustomSourcesAsync(DataSourceSet sources) =>
        FeatureRequest.InvokeAsync<SavedInfo>(
            this,
            nameof(SaveCustomSourcesAsync),
            new
            {
                dataSources = new Dictionary<string, string>
                {
                    ["announcements"] = sources.Announcements,
                    ["servers"] = sources.Servers,
                    ["recommendations"] = sources.Recommendations,
                    ["quotes"] = sources.Quotes
                }
            });

    public Task<SavedInfo> UseDataSourcesAsync(string mode) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(UseDataSourcesAsync), new
        {
            mode
        });

    public Task<SavedInfo> ResetDataSourcesAsync() =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(ResetDataSourcesAsync), new
        {
        });

    public Task<KeyResultInfo> VerifyKeyAsync(string role, string key) =>
        FeatureRequest.InvokeAsync<KeyResultInfo>(this, nameof(VerifyKeyAsync), new
        {
            role,
            key
        });

    public Task RefreshCommunityAsync() => application.HandleAsync("""{"type":"refreshCommunity"}""");

    public Task<SavedInfo> SaveRecommendationsAsync(IReadOnlyList<RecommendationInfo> items) =>
        FeatureRequest.InvokeAsync<SavedInfo>(
            this,
            nameof(SaveRecommendationsAsync),
            new
            {
                items = items.Select(item => item.Raw).ToArray()
            });

    public Task<SavedInfo> ToggleBookmarkAsync(string id, string name, string description, string author, string kind) =>
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

    public Task<byte[]> DownloadImageAsync(string url) =>
        FeatureRequest.InvokeAsync<byte[]>(this, nameof(DownloadImageAsync), new
        {
            url
        });

    public Task<AvatarInfo?> ReadAvatarAsync() =>
        FeatureRequest.InvokeAsync<AvatarInfo?>(this, nameof(ReadAvatarAsync), new
        {
        });

    public Task<AvatarInfo?> SetOfflineSkinAsync(string path) =>
        FeatureRequest.InvokeAsync<AvatarInfo?>(this, nameof(SetOfflineSkinAsync), new
        {
            path
        });

    public Task SelectMuaProfileAsync(string token, string profileId) =>
        CommandAsync(new
        {
            type = "selectMuaProfile",
            token,
            profileId
        });

    public Task PickFolderAsync(string target) => CommandAsync(new { type = "pickFolder", target });

    public Task PickFileAsync(string target) => CommandAsync(new { type = "pickFile", target });

    public Task PickSaveFileAsync(string target) => CommandAsync(new { type = "pickSaveFile", target });

    public Task ResetWindowAsync() => application.HandleAsync("""{"type":"resetWindow"}""");

    public Task<SavedInfo> SaveLaunchOptionsAsync(string token, string version, string name, LaunchOptionsSettings settings, bool inherit) =>
        FeatureRequest.InvokeAsync<SavedInfo>(
            this,
            nameof(SaveLaunchOptionsAsync),
            new
            {
                token,
                version,
                name,
                inherit,
                settings
            });

    public Task<LaunchOptionsInfo> ReadLaunchOptionsAsync(string version, string requestId = "") =>
        FeatureRequest.InvokeAsync<LaunchOptionsInfo>(
            this,
            nameof(ReadLaunchOptionsAsync),
            new
            {
                version,
                requestId
            });

    Task CommandAsync(object payload) => application.HandleAsync(JsonSerializer.Serialize(payload, Options));

    Task<TResponse> IFeatureRequestInvoker.InvokeFeatureAsync<TResponse>(
        string operation,
        object arguments) =>
        application.ExecuteFeatureAsync<TResponse>(operation, arguments);

    void Send(object payload)
    {
        if (payload is LauncherState state)
        {
            dispatch(() =>
            {
                State = state;
                StateChanged?.Invoke(state);
            });
            return;
        }
        var json = JsonSerializer.Serialize(payload, Options);
        dispatch(() => Handle(json));
    }

    void Handle(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement.Clone();
        if (!root.TryGetProperty("type", out var typeValue) || typeValue.GetString() is not { } type)
            return;
        switch (type)
        {
            case "tasks":
                Tasks = ParseItems(root, "items", TaskInfo.From);
                TasksChanged?.Invoke(Tasks);
                break;
            case "contentList":
                ContentItems = ParseItems(root, "items", ContentItem.From);
                break;
            case "games":
                Games = ParseItems(root, "items", GameInfo.From);
                GamesChanged?.Invoke(Games);
                break;
            case "saveBackups":
                SaveBackups = ParseItems(root, "items", SaveBackupInfo.From);
                break;
            case "authResult":
                AuthResultReceived?.Invoke(AuthResultInfo.From(root));
                break;
            case "accountState":
                AccountStateChanged?.Invoke(AccountStateInfo.From(root));
                break;
            case "accountSwitchResult":
                AccountSwitchResultReceived?.Invoke(AccountSwitchResultInfo.From(root));
                break;
            case "pickedPath":
                PathPicked?.Invoke(PickedPathInfo.From(root));
                break;
            case "toast":
                ToastReceived?.Invoke(ToastInfo.From(root));
                break;
            case "error":
                ErrorReceived?.Invoke(JsonRead.String(root, "message"));
                break;
            case "community":
                var payload = CommunityPayload.From(root);
                Community = CommunityData.From(payload);
                CommunityChanged?.Invoke(Community);
                CommunityReceived?.Invoke(payload);
                break;
            case "contentError":
                ContentErrorReceived?.Invoke(ContentErrorInfo.From(root));
                break;
            case "muaProfiles":
                MuaProfilesReceived?.Invoke(MuaProfilesInfo.From(root));
                break;
        }
    }

    static IReadOnlyList<T> ParseItems<T>(JsonElement root, string name, Func<JsonElement, T> map) =>
        JsonRead.Items(root, name, map);

    public void Dispose() => application.Dispose();
}
