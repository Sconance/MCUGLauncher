using System.Text.Json;
using Avalonia.Threading;
using MCUGLauncher.UseCases;

namespace MCUGLauncher.Avalonia;

/// <summary>类型化 UI 门面：替代 WebView JSON 协议，向原生 View 暴露业务消息。</summary>
internal sealed class LauncherFacade : IDisposable
{
    static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    readonly LauncherApplication application;

    public event Action<JsonElement>? MessageReceived;
    public event Action<LauncherState>? StateChanged;
    public LauncherState? State
    {
        get; private set;
    }

    public event Action<IReadOnlyList<TaskInfo>>? TasksChanged;
    public event Action<IReadOnlyList<ContentItem>>? ContentChanged;
    public IReadOnlyList<TaskInfo> Tasks { get; private set; } = Array.Empty<TaskInfo>();
    public IReadOnlyList<ContentItem> ContentItems { get; private set; } = Array.Empty<ContentItem>();

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

    public Task LaunchAsync(string version) =>
        application.HandleAsync(JsonSerializer.Serialize(new
        {
            type = "launch",
            version
        }, Options));

    public Task RequestStateAsync() => application.HandleAsync("""{"type":"requestState"}""");

    public Task ScanJavaAsync() => application.HandleAsync("""{"type":"scanJava"}""");

    public Task SaveSettingsAsync(string root, string java, int ram, string? player = null)
    {
        var payload = new Dictionary<string, object>
        {
            ["type"] = "saveSettings",
            ["root"] = root,
            ["java"] = java,
            ["ram"] = ram
        };
        if (!string.IsNullOrWhiteSpace(player))
            payload["player"] = player;
        return application.HandleAsync(JsonSerializer.Serialize(payload, Options));
    }

    public Task RequestTasksAsync() => application.HandleAsync("""{"type":"requestTasks"}""");

    public Task TaskActionAsync(string id, string action) =>
        application.HandleAsync(JsonSerializer.Serialize(new
        {
            type = "taskAction",
            id,
            action
        }, Options));

    public Task LoginMicrosoftAsync() => application.HandleAsync("""{"type":"loginMicrosoft"}""");

    public Task LoginMuaAsync(string account, string password) =>
        application.HandleAsync(JsonSerializer.Serialize(new
        {
            type = "loginMua",
            account,
            password
        }, Options));

    public Task SwitchAccountModeAsync(string mode) =>
        application.HandleAsync(JsonSerializer.Serialize(new
        {
            type = "switchAccountMode",
            account = mode
        }, Options));

    public Task ForgetAccountAsync(string mode) =>
        application.HandleAsync(JsonSerializer.Serialize(new
        {
            type = "forgetAccount",
            account = mode
        }, Options));

    public Task SearchContentAsync(string category, string query) =>
        application.HandleAsync(JsonSerializer.Serialize(new
        {
            type = "searchContent",
            category,
            query
        }, Options));

    public Task InstallContentAsync(string id, string category) =>
        application.HandleAsync(JsonSerializer.Serialize(new
        {
            type = "installContent",
            id,
            category
        }, Options));

    public void SendMessage(object payload) => Send(payload);

    public Task CloneInstanceAsync(string version, string name) =>
        application.HandleAsync(JsonSerializer.Serialize(new
        {
            type = "cloneInstance",
            version,
            name
        }, Options));

    public Task PreviewUninstallAsync(string version) =>
        application.HandleAsync(JsonSerializer.Serialize(new
        {
            type = "previewUninstall",
            version
        }, Options));

    public Task ToggleModAsync(string file, bool enabled) =>
        application.HandleAsync(JsonSerializer.Serialize(new
        {
            type = "toggleMod",
            file,
            enabled
        }, Options));

    public Task CheckUpdatesAsync(string version) =>
        application.HandleAsync(JsonSerializer.Serialize(new
        {
            type = "checkContentUpdates",
            version
        }, Options));

    public Task PreviewUpdatesAsync(string token, IReadOnlyList<string> projects) =>
        application.HandleAsync(JsonSerializer.Serialize(new
        {
            type = "previewContentUpdates",
            token,
            projects
        }, Options));

    public Task ApplyUpdatesAsync(string token) =>
        application.HandleAsync(JsonSerializer.Serialize(new
        {
            type = "applyContentUpdates",
            token
        }, Options));

    public Task ExportInstanceAsync(string version, string path, bool servers, bool saves) =>
        application.HandleAsync(JsonSerializer.Serialize(new
        {
            type = "exportInstance",
            version,
            path,
            servers,
            saves
        }, Options));

    public Task ApplyUninstallAsync(string token) =>
        application.HandleAsync(JsonSerializer.Serialize(new
        {
            type = "applyUninstall",
            token
        }, Options));

    void Send(object payload)
    {
        var json = JsonSerializer.Serialize(payload, Options);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement.Clone();
        if (root.TryGetProperty("type", out var type) && type.GetString() == "state")
        {
            State = LauncherState.From(root);
            StateChanged?.Invoke(State);
        }
        if (root.TryGetProperty("type", out var contentType) && contentType.GetString() == "contentList"
            && root.TryGetProperty("items", out var contentItems) && contentItems.ValueKind == JsonValueKind.Array)
        {
            ContentItems = contentItems.EnumerateArray().Select(ContentItem.From).ToArray();
            ContentChanged?.Invoke(ContentItems);
        }
        if (root.TryGetProperty("type", out var messageType) && messageType.GetString() == "tasks"
            && root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            Tasks = items.EnumerateArray().Select(TaskInfo.From).ToArray();
            TasksChanged?.Invoke(Tasks);
        }
        MessageReceived?.Invoke(root);
    }

    public void Dispose() => application.Dispose();
}
