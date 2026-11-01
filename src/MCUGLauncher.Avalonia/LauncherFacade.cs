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

    public Task SaveSettingsAsync(string root, string java, int ram) =>
        application.HandleAsync(JsonSerializer.Serialize(new
        {
            type = "saveSettings",
            root,
            java,
            ram
        }, Options));

    void Send(object payload)
    {
        var json = JsonSerializer.Serialize(payload, Options);
        using var document = JsonDocument.Parse(json);
        MessageReceived?.Invoke(document.RootElement.Clone());
    }

    public void Dispose() => application.Dispose();
}
