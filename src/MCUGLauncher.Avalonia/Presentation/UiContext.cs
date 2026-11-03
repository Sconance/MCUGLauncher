using System.ComponentModel;

namespace MCUGLauncher.Avalonia.Presentation;

/// <summary>Visual navigation, notices and avatar rendering state; contains no business command protocol.</summary>
internal sealed class UiContext : IDisposable, INotifyPropertyChanged
{
    readonly ILauncherService service;
    readonly Dictionary<string, string> avatars = [];
    string identity = "";
    static readonly Lazy<string> defaultAvatar = new(() => AvaloniaAvatarImage.Encode(null));
    public string Avatar { get; private set; } = defaultAvatar.Value;
    public string Status { get; private set; } = "";
    public Action<string>? Navigate
    {
        get; set;
    }
    public string PendingSearch { get; set; } = "";
    public bool HomeInstancePanelOpen
    {
        get; set;
    }
    public bool AdminUiUnlocked
    {
        get; set;
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? AvatarChanged;
    public UiContext(ILauncherService service)
    {
        this.service = service;
        service.StateChanged += StateChanged;
        service.TasksChanged += TasksChanged;
        service.GamesChanged += GamesChanged;
        service.AvatarReceived += ApplyAvatar;
        service.FeedbackReceived += Report;
    }
    void StateChanged(LauncherState state)
    {
        var key = state.AccountType + ":" + state.Player;
        if (key != identity)
        {
            identity = key;
            Avatar = avatars.GetValueOrDefault(key) ?? defaultAvatar.Value;
            AvatarChanged?.Invoke();
        }
        PropertyChanged?.Invoke(this, new(null));
    }
    void TasksChanged(TaskInfo[] value) => PropertyChanged?.Invoke(this, new(null));
    void GamesChanged(GameInfo[] value) => PropertyChanged?.Invoke(this, new(null));
    void ApplyAvatar(AvatarInfo info)
    {
        if (info.Account != service.State.AccountType || info.Player != service.State.Player || !info.Image.StartsWith("data:image/png;base64,"))
            return;
        var key = info.Account + ":" + info.Player;
        if (info.Fallback && avatars.ContainsKey(key) && info.Account != "offline")
            return;
        var changed = Avatar != info.Image;
        Avatar = info.Image;
        avatars[key] = Avatar;
        if (avatars.Count > 24)
            avatars.Remove(avatars.Keys.First());
        if (changed)
            AvatarChanged?.Invoke();
    }
    public void Report(string text)
    {
        Status = text;
        PropertyChanged?.Invoke(this, new(nameof(Status)));
    }
    public void Dispose()
    {
        service.StateChanged -= StateChanged;
        service.TasksChanged -= TasksChanged;
        service.GamesChanged -= GamesChanged;
        service.AvatarReceived -= ApplyAvatar;
        service.FeedbackReceived -= Report;
    }
}
