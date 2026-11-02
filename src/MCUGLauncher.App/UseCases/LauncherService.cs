using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MCUGLauncher.Contracts;
using MCUGLauncher.Services;

namespace MCUGLauncher.UseCases;

/// <summary>Typed application boundary. JSON is an internal compatibility detail, never a View API.</summary>
/// <remarks>C layer: dispatches state/events and adapts typed requests to existing commands. No UI algorithms.</remarks>
public sealed class LauncherService : ILauncherService
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    readonly LauncherApplication? application;
    readonly Func<object, Task>? testSend;
    readonly Action<Action> dispatch;
    readonly CancellationTokenSource lifetime = new();
    event Action<JsonElement>? replyReceived;
    public LauncherState State { get; private set; } = new();
    public TaskInfo[] Tasks { get; private set; } = [];
    public GameInfo[] Games { get; private set; } = [];
    readonly Dictionary<string, CommunityEntry[]> community = [];
    public IReadOnlyDictionary<string, CommunityEntry[]> Community => community;
    public WindowPlacement WindowPlacement => application is null ? new(1200, 800, false) :
        new(application.Config.WindowWidth, application.Config.WindowHeight, application.Config.WindowMaximized);
    public event Action<LauncherState>? StateChanged;
    public event Action<TaskInfo[]>? TasksChanged;
    public event Action<GameInfo[]>? GamesChanged;
    public event Action<IReadOnlyDictionary<string, CommunityEntry[]>>? CommunityChanged;
    public event Action<AvatarInfo>? AvatarReceived;
    public event Action<AccountSwitchResultInfo>? AccountSwitchResultReceived;
    public event Action<MuaProfilesInfo>? MuaProfilesReceived;
    public event Action<ServerPingInfo>? ServerPingReceived;
    public event Action<TestResultInfo>? TestResultReceived;
    public event Action<SaveBackupInfo[]>? SaveBackupsChanged;
    public event Action<string>? FeedbackReceived;

    public LauncherService(IDesktopHost desktop, Action<Action> dispatch,
        Func<string, XboxAuthNet.OAuth.CodeFlow.IWebUI> browser, Func<byte[]?, string> avatarEncoder, ConfigStore? store = null)
    {
        this.dispatch = dispatch;
        application = new LauncherApplication(ReceivePayload, desktop, store, dispatch, browser, avatarEncoder);
    }
    internal LauncherService(Func<object, Task> send, Action<Action>? dispatch = null)
    {
        testSend = send;
        this.dispatch = dispatch ?? (action => action());
    }
    public void RememberWindow(double width, double height, bool maximized) => application?.RememberWindow(width, height, maximized);
    public Task StartAsync() => Command(new { type = "ready" });
    public Task ReadAvatarAsync() => Command(new { type = "readAvatar" });
    public Task<AuthResultInfo> LoginMicrosoftAsync() => Request<AuthResultInfo>("authResult", new { type = "loginMicrosoft" });
    public Task<AuthResultInfo> LoginMuaAsync(string account = "", string password = "") => Request<AuthResultInfo>("authResult", new { type = "loginMua", account, password });
    public Task<OperationReceipt> SaveSettingsAsync(AppSettingsPatch patch) => Request<OperationReceipt>("settingsSaved", Patch(patch));
    public Task SetOfflineSkinAsync(string path = "") => Command(new { type = "setOfflineSkin", path });
    public Task SelectMuaProfileAsync(string token = "", string profileId = "") => Command(new { type = "selectMuaProfile", token, profileId });
    public Task ResetWindowAsync() => Command(new { type = "resetWindow" });
    public Task<KeyResultInfo> VerifyKeyAsync(string role = "", string key = "") => Request<KeyResultInfo>("keyResult", new { type = "verifyKey", role, key });
    public Task ForgetAccountAsync(string account = "") => Command(new { type = "forgetAccount", account });
    public Task RefreshCommunityAsync() => Command(new { type = "refreshCommunity" });
    public Task PingServerAsync(string address = "", string id = "") => Command(new { type = "pingServer", address, id });
    public Task DeleteServerAsync(string address = "") => Command(new { type = "deleteServer", address });
    public Task<ContentListInfo> SearchContentAsync(string category = "", string query = "", string game = "", string loader = "", string requestId = "") => Request<ContentListInfo>("contentList", new { type = "searchContent", category, query, game, loader, requestId });
    public Task ToggleBookmarkAsync(string id = "", string kind = "", string name = "", string description = "", string author = "") => Command(new { type = "toggleBookmark", id, kind, name, description, author });
    public Task<OperationReceipt> PublishAsync(string title = "", string body = "", string expiresAt = "", string imageData = "") => Request<OperationReceipt>("publishResult", new { type = "publish", title, body, expiresAt, imageData });
    public Task<OperationReceipt> SaveRecommendationsAsync(CommunityEntry[] items) => Request<OperationReceipt>("saved", new { type = "saveRecommendations", items = CommunityRecords(items) });
    public Task<OperationReceipt> SaveAnnouncementsAsync(CommunityEntry[] items) => Request<OperationReceipt>("saved", new { type = "saveAnnouncements", items = CommunityRecords(items) });
    public Task<LocalImportPreviewInfo> PreviewLocalImportAsync(string path = "", string instance = "", string resourceKind = "") => Request<LocalImportPreviewInfo>("localImportPreview", new { type = "previewLocalImport", path, instance, resourceKind });
    public Task<OperationReceipt> ConfirmLocalImportAsync(string token = "") => Request<OperationReceipt>("localImportQueued", new { type = "confirmLocalImport", token });
    public Task<ServerResultInfo> SaveServerAsync(string name = "", string address = "", string oldAddress = "") => Request<ServerResultInfo>("serverResult", new { type = "saveServer", name, address, oldAddress });
    public Task SwitchAccountModeAsync(string account = "") => Command(new { type = "switchAccountMode", account });
    public Task LaunchAsync(string version = "") => Command(new { type = "launch", version });
    public Task<PackPreviewInfo> PreviewPackAsync(string path = "") => Request<PackPreviewInfo>("packPreview", new { type = "previewPack", path });
    public Task<OperationReceipt> ConfirmPackAsync(string token = "", string name = "", string directory = "") => Request<OperationReceipt>("packQueued", new { type = "confirmPack", token, name, directory });
    public Task<OperationReceipt> InstallLoaderAsync(string version = "", string loader = "", string loaderVersion = "") => Request<OperationReceipt>("loaderQueued", new { type = "installLoader", version, loader, loaderVersion });
    public Task<LoaderListInfo> ListLoadersAsync(string version = "", string loader = "", string requestId = "") => Request<LoaderListInfo>("loaderList", new { type = "listLoaders", version, loader, requestId });
    public Task RequestStateAsync() => Command(new { type = "requestState" });
    public Task SelectVersionAsync(string version = "") => Command(new { type = "selectVersion", version });
    public Task SelectInstanceAsync(string instance = "") => Command(new { type = "selectInstance", instance });
    public Task CloneInstanceAsync(string version = "", string name = "", bool saves = false, bool servers = false) => Command(new { type = "cloneInstance", version, name, saves, servers });
    public Task ExportInstanceAsync(string version = "", string path = "", bool saves = false, bool servers = false) => Command(new { type = "exportInstance", version, path, saves, servers });
    public Task OpenFolderAsync(string kind = "") => Command(new { type = "openFolder", kind });
    public Task ToggleModAsync(string file = "", bool enabled = false) => Command(new { type = "toggleMod", file, enabled });
    public Task DeleteModAsync(string file = "") => Command(new { type = "deleteMod", file });
    public Task<JavaListInfo> ScanJavaAsync() => Request<JavaListInfo>("javaList", new { type = "scanJava" });
    public Task<LaunchOptionsInfo> ReadLaunchOptionsAsync(string version = "", string requestId = "") => Request<LaunchOptionsInfo>("launchOptions", new { type = "readLaunchOptions", version, requestId });
    public Task<OperationReceipt> SaveLaunchOptionsAsync(string token, string name, bool inherit, LaunchOptionsSettings settings) => Request<OperationReceipt>("launchOptionsSaved", new { type = "saveLaunchOptions", token, name, inherit, settings });
    public Task<UninstallPreviewInfo> PreviewUninstallAsync(string version = "") => Request<UninstallPreviewInfo>("uninstallPreview", new { type = "previewUninstall", version });
    public Task<OperationReceipt> ApplyUninstallAsync(string token = "") => Request<OperationReceipt>("uninstallQueued", new { type = "applyUninstall", token });
    public Task<ContentUpdatesInfo> CheckUpdatesAsync(string version = "") => Request<ContentUpdatesInfo>("contentUpdates", new { type = "checkContentUpdates", version });
    public Task<ContentUpdatePreviewInfo> PreviewUpdatesAsync(string token, string[] projects) => Request<ContentUpdatePreviewInfo>("contentUpdatePreview", new { type = "previewContentUpdates", token, projects });
    public Task<OperationReceipt> ApplyUpdatesAsync(string token = "") => Request<OperationReceipt>("contentUpdateQueued", new { type = "applyContentUpdates", token });
    public Task<OperationReceipt> QueueContentVersionAsync(string token = "", string versionId = "", string mode = "", string path = "") => Request<OperationReceipt>("resourceQueued", new { type = "queueContentVersion", token, versionId, mode, path });
    public Task<ResourceVersionsInfo> ListContentVersionsAsync(string id = "", string category = "", string instance = "", string game = "", string loader = "", string requestId = "") => Request<ResourceVersionsInfo>("resourceVersions", new { type = "listContentVersions", id, category, instance, game, loader, requestId });
    public Task BackupSavesAsync() => Command(new { type = "backupSaves" });
    public Task ListSaveBackupsAsync() => Command(new { type = "listSaveBackups" });
    public Task RestoreSaveAsync(string id = "", string world = "") => Command(new { type = "restoreSave", id, world });
    public Task UseDataSourcesAsync(string mode = "") => Command(new { type = "useDataSources", mode });
    public Task SaveCustomSourcesAsync(DataSourceSet dataSources) => Command(new { type = "saveCustomSources", dataSources });
    public Task TestSourceAsync(string name = "", string url = "", string requestId = "") => Command(new { type = "testSource", name, url, requestId });
    public Task RequestTasksAsync() => Command(new { type = "requestTasks" });
    public Task TaskActionAsync(string id = "", string action = "") => Command(new { type = "taskAction", id, action });
    public Task RequestGamesAsync() => Command(new { type = "requestGames" });
    public Task StopGameAsync(string id = "") => Command(new { type = "stopGame", id });
    public Task<LogTextInfo> ReadLogAsync() => Request<LogTextInfo>("logText", new { type = "readLog" });
    public Task<SystemInfoInfo> SystemInfoAsync() => Request<SystemInfoInfo>("systemInfo", new { type = "systemInfo" });
    public Task<VanillaListInfo> ListVanillaVersionsAsync() => Request<VanillaListInfo>("vanillaList", new { type = "vanillaVersions" });
    public Task InstallVanillaAsync(string version = "") => Command(new { type = "installVanilla", version });

    public Task<OperationReceipt> SaveCommunityAsync(string feed, CommunityEntry[] items) => feed switch
    {
        "announcements" => SaveAnnouncementsAsync(items),
        "recommendations" => SaveRecommendationsAsync(items),
        _ => throw new ArgumentException("未知社区内容类型")
    };
    public async Task<byte[]> DownloadImageAsync(string url)
    {
        var result = await Request<ImageReply>("imageData", new
        {
            type = "readImage",
            url
        });
        if (!result.Ok)
            throw new IOException("图片不可用");
        return Convert.FromBase64String(result.Image);
    }
    sealed record ImageReply(bool Ok, string Image);
    Task Command(object payload) => testSend != null ? testSend(payload) : application!.HandleAsync(JsonSerializer.Serialize(payload, Json));
    async Task<T> Request<T>(string response, object payload)
    {
        var command = JsonSerializer.SerializeToNode(payload, Json)!.AsObject();
        var id = command["requestId"]?.GetValue<string>();
        if (string.IsNullOrEmpty(id))
            id = Guid.NewGuid().ToString("N");
        command["requestId"] = id;
        var token = command["token"]?.GetValue<string>() ?? "";
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Receive(JsonElement message)
        {
            var type = String(message, "type");
            var requestId = String(message, "requestId");
            if (type is "error" or "contentError" && requestId == id)
                completion.TrySetException(new IOException(String(message, "message")));
            if (type != response || requestId.Length > 0 && requestId != id)
                return;
            if (token.Length > 0 && String(message, "token") is { Length: > 0 } replyToken && replyToken != token)
                return;
            if (response == "authResult" && message.TryGetProperty("pending", out var pending) && pending.GetBoolean())
                return;
            object? result = message.Deserialize<T>(Json);
            if (result is OperationReceipt receipt && !message.TryGetProperty("ok", out _))
                result = receipt with
                {
                    Ok = true
                };
            completion.TrySetResult((T)result!);
        }
        replyReceived += Receive;
        try
        {
            await Command(command);
            return await completion.Task.WaitAsync(response == "authResult" ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(70), lifetime.Token);
        }
        finally { replyReceived -= Receive; }
    }
    static object Patch(AppSettingsPatch patch)
    {
        var payload = JsonSerializer.SerializeToNode(patch, Json)!.AsObject();
        foreach (var name in payload.Where(p => p.Value == null).Select(p => p.Key).ToArray())
            payload.Remove(name);
        payload["type"] = "saveSettings";
        return payload;
    }
    internal void ReceivePayload(object value)
    {
        if (value is LauncherState state)
        {
            dispatch(() => ApplyState(state));
            return;
        }
        var json = JsonSerializer.Serialize(value, Json);
        dispatch(() => Receive(json));
    }
    internal void Receive(string json)
    {
        using var document = JsonDocument.Parse(json);
        var message = document.RootElement.Clone();
        var type = String(message, "type");
        switch (type)
        {
            case "state":
                ApplyState(message.Deserialize<LauncherState>(Json)!);
                break;
            case "accountState":
                var account = message.Deserialize<AccountStateInfo>(Json)!;
                ApplyState(State with
                {
                    AccountType = account.AccountType,
                    Player = account.Player
                });
                break;
            case "tasks":
                Tasks = Items<TaskInfo>(message);
                TasksChanged?.Invoke(Tasks);
                break;
            case "games":
                Games = Items<GameInfo>(message);
                GamesChanged?.Invoke(Games);
                break;
            case "community":
                foreach (var property in message.GetProperty("payload").EnumerateObject())
                {
                    try
                    {
                        using var feed = JsonDocument.Parse(property.Value.GetString() ?? "[]");
                        var entries = feed.RootElement;
                        if (entries.ValueKind == JsonValueKind.Object && (entries.TryGetProperty("items", out var nested) || entries.TryGetProperty(property.Name, out nested)))
                            entries = nested;
                        if (entries.ValueKind == JsonValueKind.Array)
                            community[property.Name] = entries.EnumerateArray().Select(ReadEntry).ToArray();
                    }
                    catch (JsonException) { FeedbackReceived?.Invoke("社区内容格式无效：" + property.Name); }
                }
                CommunityChanged?.Invoke(community);
                break;
            case "avatar":
                AvatarReceived?.Invoke(message.Deserialize<AvatarInfo>(Json)!);
                break;
            case "accountSwitchResult":
                AccountSwitchResultReceived?.Invoke(message.Deserialize<AccountSwitchResultInfo>(Json)!);
                break;
            case "muaProfiles":
                MuaProfilesReceived?.Invoke(message.Deserialize<MuaProfilesInfo>(Json)!);
                break;
            case "serverPing":
                ServerPingReceived?.Invoke(message.Deserialize<ServerPingInfo>(Json)!);
                break;
            case "testResult":
                TestResultReceived?.Invoke(message.Deserialize<TestResultInfo>(Json)!);
                break;
            case "saveBackups":
                SaveBackupsChanged?.Invoke(Items<SaveBackupInfo>(message));
                break;
            case "communityRefreshDone":
                FeedbackReceived?.Invoke(message.GetProperty("ok").GetBoolean() ? "社区内容已刷新" : "部分数据源读取失败，请检查数据源设置");
                break;
            case "settingsSaved":
                FeedbackReceived?.Invoke("已保存设置");
                break;
        }
        if (type is "error" or "toast" or "saved" or "launchResult" or "publishResult" or "authResult" or "accountSwitchResult" or "serverResult")
        {
            var text = String(message, "message");
            if (text.Length > 0)
                FeedbackReceived?.Invoke(text);
        }
        replyReceived?.Invoke(message);
    }
    void ApplyState(LauncherState state)
    {
        var oldKey = State.AccountType + ":" + State.Player;
        State = state;
        StateChanged?.Invoke(state);
        if (oldKey != State.AccountType + ":" + State.Player)
            _ = ReadAvatarAsync();
    }
    static string String(JsonElement message, string name) => message.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    static T[] Items<T>(JsonElement message) => message.GetProperty("items").Deserialize<T[]>(Json) ?? [];
    static CommunityEntry ReadEntry(JsonElement value)
    {
        var entry = value.Deserialize<CommunityEntry>(Json)!;
        var known = JsonSerializer.SerializeToElement(entry, Json);
        return entry with
        {
            Metadata = value.EnumerateObject().Where(p => !known.TryGetProperty(p.Name, out _)).ToDictionary(p => p.Name, p => p.Value.GetRawText())
        };
    }
    static object[] CommunityRecords(CommunityEntry[] items) => items.Select(entry =>
    {
        var record = JsonSerializer.SerializeToNode(entry, Json)!.AsObject();
        record.Remove("metadata");
        foreach (var (name, raw) in entry.Metadata)
            if (!record.ContainsKey(name))
                record[name] = JsonNode.Parse(raw);
        return (object)record;
    }).ToArray();
    public void Dispose()
    {
        lifetime.Cancel();
        application?.Dispose();
        lifetime.Dispose();
    }
}
