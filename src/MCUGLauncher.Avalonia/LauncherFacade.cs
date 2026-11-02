using System.Text.Json;
using Avalonia.Threading;
using MCUGLauncher.UseCases;

namespace MCUGLauncher.Avalonia;

/// <summary>类型化 UI 门面：原生页面只调用强类型方法，消息协议留在内部。</summary>
internal sealed class LauncherFacade : IDisposable
{
    static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    readonly LauncherApplication application;

    public event Action<JsonElement>? MessageReceived;
    public event Action<LauncherState>? StateChanged;
    public event Action<IReadOnlyList<TaskInfo>>? TasksChanged;
    public event Action<IReadOnlyList<ContentItem>>? ContentChanged;
    public event Action<IReadOnlyList<GameInfo>>? GamesChanged;
    public event Action<IReadOnlyList<JavaInfo>>? JavaListChanged;
    public event Action<IReadOnlyList<SaveBackupInfo>>? SaveBackupsChanged;
    public event Action<string>? LogTextReceived;
    public event Action<SystemInfoInfo>? SystemInfoReceived;
    public event Action<AuthResultInfo>? AuthResultReceived;
    public event Action<AccountStateInfo>? AccountStateChanged;
    public event Action<AccountSwitchResultInfo>? AccountSwitchResultReceived;
    public event Action<PickedPathInfo>? PathPicked;
    public event Action<ToastInfo>? ToastReceived;
    public event Action<string>? ErrorReceived;
    public event Action<LaunchResultInfo>? LaunchResultReceived;
    public event Action<UninstallPreviewInfo>? UninstallPreviewReceived;
    public event Action<ContentUpdatesInfo>? ContentUpdatesReceived;
    public event Action<ContentUpdatePreviewInfo>? ContentUpdatePreviewReceived;
    public event Action<ServerPingInfo>? ServerPingReceived;
    public event Action<ServerResultInfo>? ServerResultReceived;
    public event Action<TestResultInfo>? TestResultReceived;
    public event Action<KeyResultInfo>? KeyResultReceived;
    public event Action<CommunityPayload>? CommunityReceived;
    public event Action<CommunityData>? CommunityChanged;
    public event Action<AvatarInfo>? AvatarReceived;
    public event Action<LoaderListInfo>? LoaderListReceived;
    public event Action<VanillaVersionInfo[]>? VanillaListReceived;
    public event Action<ResourceVersionsInfo>? ResourceVersionsReceived;
    public event Action<PackPreviewInfo>? PackPreviewReceived;
    public event Action<ContentErrorInfo>? ContentErrorReceived;
    public event Action<MuaProfilesInfo>? MuaProfilesReceived;
    public event Action<LaunchOptionsInfo>? LaunchOptionsReceived;
    public event Action? LaunchOptionsSaved;
    public event Action<PublishResultInfo>? PublishResultReceived;
    public event Action<SavedInfo>? SavedReceived;
    public event Action? SettingsSaved;

    public MCUGLauncher.Models.AppConfig Config => application.Config;

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

    public LauncherFacade(IDesktopHost desktop)
    {
        application = new LauncherApplication(
            Send,
            desktop,
            dispatch: action => Dispatcher.UIThread.Post(action),
            microsoftBrowser: _ => new MicrosoftLoginWebUi(),
            avatarEncoder: AvaloniaAvatarImage.Encode);
    }

    public Task StartAsync() => application.HandleAsync("""{"type":"ready"}""");

    public void RememberWindow(double width, double height, bool maximized) =>
        application.RememberWindow(width, height, maximized);

    public Task LaunchAsync(string version) => CommandAsync(new { type = "launch", version });

    public Task RequestStateAsync() => application.HandleAsync("""{"type":"requestState"}""");

    public Task SelectInstanceAsync(string instance) => CommandAsync(new { type = "selectInstance", instance });

    public Task SelectVersionAsync(string version) => CommandAsync(new { type = "selectVersion", version });

    public Task ScanJavaAsync() => application.HandleAsync("""{"type":"scanJava"}""");

    public Task SaveSettingsAsync(string root, string java, int ram, string? player = null) =>
        SaveSettingsAsync(new AppSettingsPatch(root, java, ram, Player: player));

    public Task SaveSettingsAsync(AppSettingsPatch patch)
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
        if (patch.WriteEndpoint is not null)
            payload["writeEndpoint"] = patch.WriteEndpoint;
        if (patch.DataSources is { } sources)
            payload["dataSources"] = new Dictionary<string, string>
            {
                ["announcements"] = sources.Announcements,
                ["servers"] = sources.Servers,
                ["recommendations"] = sources.Recommendations,
                ["quotes"] = sources.Quotes
            };
        return CommandAsync(payload);
    }

    public Task RequestTasksAsync() => application.HandleAsync("""{"type":"requestTasks"}""");

    public Task TaskActionAsync(string id, string action) => CommandAsync(new { type = "taskAction", id, action });

    public Task LoginMicrosoftAsync() => application.HandleAsync("""{"type":"loginMicrosoft"}""");

    public Task LoginMuaAsync(string account, string password) =>
        CommandAsync(new
        {
            type = "loginMua",
            account,
            password
        });

    public Task SwitchAccountModeAsync(string mode) =>
        CommandAsync(new
        {
            type = "switchAccountMode",
            account = mode
        });

    public Task ForgetAccountAsync(string mode) =>
        CommandAsync(new
        {
            type = "forgetAccount",
            account = mode
        });

    public Task LogoutAccountAsync() => application.HandleAsync("""{"type":"logoutAccount"}""");

    public Task SearchContentAsync(string category, string query, string requestId = "") =>
        CommandAsync(new
        {
            type = "searchContent",
            category,
            query,
            requestId
        });

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

    public Task ListContentVersionsAsync(
        string id,
        string category,
        string instance = "",
        string game = "",
        string loader = "",
        string requestId = "") =>
        CommandAsync(new
        {
            type = "listContentVersions",
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

    public Task ListVanillaVersionsAsync() => application.HandleAsync("""{"type":"vanillaVersions"}""");

    public Task InstallVanillaAsync(string version) => CommandAsync(new { type = "installVanilla", version });

    public Task ListLoadersAsync(string version, string loader) =>
        CommandAsync(new
        {
            type = "listLoaders",
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

    public Task PreviewPackAsync(string path) => CommandAsync(new { type = "previewPack", path });

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

    public Task PreviewUninstallAsync(string version) => CommandAsync(new { type = "previewUninstall", version });

    public Task PreviewDeleteVersionAsync(string version) => CommandAsync(new { type = "deleteVersion", version });

    public Task ApplyUninstallAsync(string token) => CommandAsync(new { type = "applyUninstall", token });

    public Task ToggleModAsync(string file, bool enabled) => CommandAsync(new { type = "toggleMod", file, enabled });

    public Task DeleteModAsync(string file) => CommandAsync(new { type = "deleteMod", file });

    public Task CheckUpdatesAsync(string version) => CommandAsync(new { type = "checkContentUpdates", version });

    public Task PreviewUpdatesAsync(string token, IReadOnlyList<string> projects) =>
        CommandAsync(new
        {
            type = "previewContentUpdates",
            token,
            projects
        });

    public Task ApplyUpdatesAsync(string token) => CommandAsync(new { type = "applyContentUpdates", token });

    public Task RequestGamesAsync() => application.HandleAsync("""{"type":"requestGames"}""");

    public Task StopGameAsync(string id) => CommandAsync(new { type = "stopGame", id });

    public Task BackupSavesAsync() => application.HandleAsync("""{"type":"backupSaves"}""");

    public Task ListSaveBackupsAsync() => application.HandleAsync("""{"type":"listSaveBackups"}""");

    public Task RestoreSaveAsync(string id, string world) =>
        CommandAsync(new
        {
            type = "restoreSave",
            id,
            world
        });

    public Task OpenFolderAsync(string kind) => CommandAsync(new { type = "openFolder", kind });

    public Task ReadLogAsync() => application.HandleAsync("""{"type":"readLog"}""");

    public Task SystemInfoAsync() => application.HandleAsync("""{"type":"systemInfo"}""");

    public Task SaveServerAsync(string name, string address, string oldAddress = "") =>
        CommandAsync(new
        {
            type = "saveServer",
            name,
            address,
            oldAddress
        });

    public Task DeleteServerAsync(string address) => CommandAsync(new { type = "deleteServer", address });

    public Task PingServerAsync(string address, string id) =>
        CommandAsync(new
        {
            type = "pingServer",
            address,
            id
        });

    public Task TestSourceAsync(string name, string url, string requestId = "") =>
        CommandAsync(new
        {
            type = "testSource",
            name,
            url,
            requestId
        });

    public Task SaveCustomSourcesAsync(DataSourceSet sources) =>
        CommandAsync(new
        {
            type = "saveCustomSources",
            dataSources = new Dictionary<string, string>
            {
                ["announcements"] = sources.Announcements,
                ["servers"] = sources.Servers,
                ["recommendations"] = sources.Recommendations,
                ["quotes"] = sources.Quotes
            }
        });

    public Task UseDataSourcesAsync(string mode) => CommandAsync(new { type = "useDataSources", mode });

    public Task ResetDataSourcesAsync() => application.HandleAsync("""{"type":"resetDataSources"}""");

    public Task VerifyKeyAsync(string role, string key) => CommandAsync(new { type = "verifyKey", role, key });

    public Task RefreshCommunityAsync() => application.HandleAsync("""{"type":"refreshCommunity"}""");

    public Task PublishAsync(string title, string body, string expiresAt, string imageData = "") =>
        CommandAsync(new
        {
            type = "publish",
            title,
            body,
            expiresAt,
            imageData
        });

    public Task SaveAnnouncementsAsync(IReadOnlyList<AnnouncementInfo> items) =>
        CommandAsync(new
        {
            type = "saveAnnouncements",
            items = items.Select(item => ParseRaw(item.Raw)).OfType<JsonElement>().ToArray()
        });

    public Task SaveRecommendationsAsync(IReadOnlyList<RecommendationInfo> items) =>
        CommandAsync(new
        {
            type = "saveRecommendations",
            items = items.Select(item => ParseRaw(item.Raw)).OfType<JsonElement>().ToArray()
        });

    static JsonElement? ParseRaw(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        try
        {
            using var document = JsonDocument.Parse(raw);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public Task ToggleBookmarkAsync(string id, string name, string description, string author, string kind) =>
        CommandAsync(new
        {
            type = "toggleBookmark",
            id,
            name,
            description,
            author,
            kind
        });

    public Task<byte[]> DownloadImageAsync(string url) => application.DownloadImageAsync(url);

    public Task ReadAvatarAsync() => application.HandleAsync("""{"type":"readAvatar"}""");

    public Task SetOfflineSkinAsync(string path) => CommandAsync(new { type = "setOfflineSkin", path });

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

    public Task SaveLaunchOptionsAsync(string token, string version, string name, LaunchOptionsSettings settings, bool inherit) =>
        CommandAsync(new
        {
            type = "saveLaunchOptions",
            token,
            version,
            name,
            inherit,
            settings
        });

    public Task ReadLaunchOptionsAsync(string version, string requestId = "") =>
        CommandAsync(new
        {
            type = "readLaunchOptions",
            version,
            requestId
        });

    public void SendMessage(object payload) => Send(payload);

    Task CommandAsync(object payload) => application.HandleAsync(JsonSerializer.Serialize(payload, Options));

    void Send(object payload)
    {
        var json = JsonSerializer.Serialize(payload, Options);
        if (Dispatcher.UIThread.CheckAccess())
            Handle(json);
        else
            Dispatcher.UIThread.Post(() => Handle(json));
    }

    void Handle(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement.Clone();
        if (!root.TryGetProperty("type", out var typeValue) || typeValue.GetString() is not { } type)
            return;
        switch (type)
        {
            case "state":
                State = LauncherState.From(root);
                StateChanged?.Invoke(State);
                break;
            case "tasks":
                Tasks = ParseItems(root, "items", TaskInfo.From);
                TasksChanged?.Invoke(Tasks);
                break;
            case "contentList":
                ContentItems = ParseItems(root, "items", ContentItem.From);
                ContentChanged?.Invoke(ContentItems);
                break;
            case "games":
                Games = ParseItems(root, "items", GameInfo.From);
                GamesChanged?.Invoke(Games);
                break;
            case "javaList":
                JavaList = ParseItems(root, "items", JavaInfo.From);
                JavaListChanged?.Invoke(JavaList);
                break;
            case "saveBackups":
                SaveBackups = ParseItems(root, "items", SaveBackupInfo.From);
                SaveBackupsChanged?.Invoke(SaveBackups);
                break;
            case "logText":
                LogTextReceived?.Invoke(JsonRead.String(root, "text"));
                break;
            case "systemInfo":
                SystemInfoReceived?.Invoke(SystemInfoInfo.From(root));
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
            case "publishResult":
                PublishResultReceived?.Invoke(PublishResultInfo.From(root));
                break;
            case "launchResult":
                LaunchResultReceived?.Invoke(LaunchResultInfo.From(root));
                break;
            case "uninstallPreview":
                UninstallPreviewReceived?.Invoke(UninstallPreviewInfo.From(root));
                break;
            case "contentUpdates":
                ContentUpdatesReceived?.Invoke(ContentUpdatesInfo.From(root));
                break;
            case "contentUpdatePreview":
                ContentUpdatePreviewReceived?.Invoke(ContentUpdatePreviewInfo.From(root));
                break;
            case "serverPing":
                ServerPingReceived?.Invoke(ServerPingInfo.From(root));
                break;
            case "serverResult":
                ServerResultReceived?.Invoke(ServerResultInfo.From(root));
                break;
            case "testResult":
                TestResultReceived?.Invoke(TestResultInfo.From(root));
                break;
            case "keyResult":
                KeyResultReceived?.Invoke(KeyResultInfo.From(root));
                break;
            case "community":
                var payload = CommunityPayload.From(root);
                Community = CommunityData.From(payload);
                CommunityChanged?.Invoke(Community);
                CommunityReceived?.Invoke(payload);
                break;
            case "avatar":
                AvatarReceived?.Invoke(AvatarInfo.From(root));
                break;
            case "loaderList":
                LoaderListReceived?.Invoke(LoaderListInfo.From(root));
                break;
            case "vanillaList":
                VanillaListReceived?.Invoke(ParseItems(root, "items", VanillaVersionInfo.From).ToArray());
                break;
            case "resourceVersions":
                ResourceVersionsReceived?.Invoke(ResourceVersionsInfo.From(root));
                break;
            case "packPreview":
                PackPreviewReceived?.Invoke(PackPreviewInfo.From(root));
                break;
            case "contentError":
                ContentErrorReceived?.Invoke(ContentErrorInfo.From(root));
                break;
            case "muaProfiles":
                MuaProfilesReceived?.Invoke(MuaProfilesInfo.From(root));
                break;
            case "launchOptions":
                LaunchOptionsReceived?.Invoke(LaunchOptionsInfo.From(root));
                break;
            case "launchOptionsSaved":
                LaunchOptionsSaved?.Invoke();
                break;
            case "saved":
                SavedReceived?.Invoke(SavedInfo.From(root));
                break;
            case "settingsSaved":
                SettingsSaved?.Invoke();
                break;
        }
        MessageReceived?.Invoke(root);
    }

    static IReadOnlyList<T> ParseItems<T>(JsonElement root, string name, Func<JsonElement, T> map) =>
        JsonRead.Items(root, name, map);

    public void Dispose() => application.Dispose();
}

internal sealed record AppSettingsPatch(
    string? Root = null,
    string? Java = null,
    int? Ram = null,
    int? Width = null,
    int? Height = null,
    bool? AutoMemory = null,
    bool? Anonymous = null,
    string? Player = null,
    string? WriteEndpoint = null,
    DataSourceSet? DataSources = null);

internal sealed record LaunchOptionsSettings(
    bool AutoMemory,
    bool FullScreen,
    int RamMb,
    int Width,
    int Height,
    string Java,
    string JvmArguments,
    string WindowTitle,
    string Server);
