using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia.Threading;
using MCUGLauncher.Services;
using MCUGLauncher.UseCases;
using MCUGLauncher.Avalonia.Presentation;

namespace MCUGLauncher.Avalonia;

/// <summary>Native presentation state and compatibility adapter to existing use cases.</summary>
internal sealed class LauncherFacade : IDisposable, INotifyPropertyChanged
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    readonly LauncherApplication? application;
    readonly Func<object, Task>? testSend;
    readonly CancellationTokenSource lifetime = new();
    readonly Dictionary<string, string> avatarCache = [];
    string avatar = AvaloniaAvatarImage.Encode(null);
    public LauncherState State { get; private set; } = new();
    public NativeItem[] Tasks { get; private set; } = [];
    public NativeItem[] Games { get; private set; } = [];
    public Dictionary<string, CommunityEntry[]> Community { get; } = [];
    public string Avatar => avatar;
    public (double Width, double Height, bool Maximized) WindowPreferences => application is null ? (1200, 800, false) : (application.Config.WindowWidth, application.Config.WindowHeight, application.Config.WindowMaximized);
    public string Status { get; private set; } = "AI 迁移框架尝试 · 尚未完成真实账号与游戏验收";
    public bool AdminUiUnlocked
    {
        get; set;
    }
    public bool HomeInstancePanelOpen
    {
        get; set;
    }
    public string PendingSearch { get; set; } = "";
    public Action<string>? Navigate
    {
        get; set;
    }
    public event Action<NativeMessage>? MessageReceived;
    public event PropertyChangedEventHandler? PropertyChanged;
    public LauncherFacade(IDesktopHost desktop, ConfigStore? store = null)
    {
        application = new LauncherApplication(SendMessage, desktop, store,
            action => Dispatcher.UIThread.Post(action), _ => new MicrosoftLoginWebUi(), AvaloniaAvatarImage.Encode);
    }
    internal LauncherFacade(Func<object, Task> send) => testSend = send;
    public Task SendAsync(object payload) => testSend != null ? testSend(payload)
        : application!.HandleAsync(JsonSerializer.Serialize(payload, Json));
    public async Task<NativeMessage> RequestAsync(string response, object payload, Func<NativeMessage, bool>? matches = null)
    {
        var command = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(payload, Json))!.AsObject();
        var requestId = command["requestId"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N");
        command["requestId"] = requestId;
        var completion = new TaskCompletionSource<NativeMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Receive(NativeMessage message)
        {
            if (message.Type == response && (matches == null || matches(message)))
                completion.TrySetResult(message);
            else if (message.Type is "error" or "contentError" && message.RequestId == requestId)
                completion.TrySetException(new IOException(message.Message));
        }
        MessageReceived += Receive;
        try
        {
            await SendAsync(command);
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(70), lifetime.Token);
        }
        finally { MessageReceived -= Receive; }
    }
    public void Report(string text)
    {
        Status = text;
        Notify(nameof(Status));
    }
    public void SendMessage(object value)
    {
        var json = JsonSerializer.Serialize(value, Json);
        if (Dispatcher.UIThread.CheckAccess())
            Receive(json);
        else
            Dispatcher.UIThread.Post(() => Receive(json));
    }
    internal void Receive(string json)
    {
        var message = JsonSerializer.Deserialize<NativeMessage>(json, Json)!;
        var oldKey = State.AccountType + ":" + State.Player;
        if (message.Type == "state")
            State = JsonSerializer.Deserialize<LauncherState>(json, Json)!;
        if (message.Type == "accountState")
            State = State with
            {
                Player = message.Player,
                AccountType = message.AccountType,
                AccountLabel = message.AccountLabel
            };
        var key = State.AccountType + ":" + State.Player;
        if (oldKey != key)
        {
            avatar = avatarCache.GetValueOrDefault(key) ?? AvaloniaAvatarImage.Encode(null);
            Notify(nameof(Avatar));
            _ = SendAsync(new
            {
                type = "readAvatar"
            });
        }
        if (message.Type == "avatar" && message.Account == State.AccountType && message.Player == State.Player && message.Image.StartsWith("data:image/png;base64,", StringComparison.Ordinal))
        {
            if (!message.Fallback || !avatarCache.ContainsKey(key) || message.Account == "offline")
            {
                avatar = message.Image;
                avatarCache[key] = avatar;
                if (avatarCache.Count > 24)
                    avatarCache.Remove(avatarCache.Keys.First());
                Notify(nameof(Avatar));
            }
        }
        if (message.Type == "tasks")
            Tasks = message.Items;
        if (message.Type == "games")
            Games = message.Items;
        if (message.Type == "community")
            foreach (var (name, text) in message.Payload)
            {
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    var entries = doc.RootElement;
                    if (entries.ValueKind == JsonValueKind.Object && (entries.TryGetProperty("items", out var nested) || entries.TryGetProperty(name, out nested)))
                        entries = nested;
                    if (entries.ValueKind == JsonValueKind.Array)
                        Community[name] = entries.Deserialize<CommunityEntry[]>(Json) ?? [];
                }
                catch (JsonException) { Report("社区内容格式无效：" + name); }
            }
        if (message.Type is "error" or "toast" or "saved" or "launchResult" or "publishResult" or "authResult" or "accountSwitchResult" or "serverResult")
            Report(message.Message);
        if (message.Type == "communityRefreshDone")
            Report(message.Ok ? "社区内容已刷新。" : "部分数据源读取失败，请检查数据源设置。");
        if (message.Type == "settingsSaved")
            Report("已保存设置。");
        if (message.Type is "state" or "accountState")
            Notify(nameof(State));
        if (message.Type is "tasks" or "games")
        {
            Notify(nameof(Tasks));
            Notify(nameof(Games));
        }
        MessageReceived?.Invoke(message);
    }
    public void RememberWindow(double width, double height, bool maximized) => application?.RememberWindow(width, height, maximized);
    void Notify([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new(property));
    public void Dispose()
    {
        lifetime.Cancel();
        application?.Dispose();
        lifetime.Dispose();
    }
}
