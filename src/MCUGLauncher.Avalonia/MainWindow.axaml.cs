using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia.Controls;
using Microsoft.Web.WebView2.Core;

namespace MCUGLauncher.Avalonia;

public partial class MainWindow : Window
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    bool _ready;
    bool _closed;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closed += (_, _) => { _closed = true; };
        Browser.DOMContentLoaded += OnDomContentLoaded;
    }

    void OnOpened(object? sender, EventArgs e)
    {
        var page = Path.Combine(AppContext.BaseDirectory, "Ui", "index.html");
        Browser.Source = new Uri(page);
    }

    void OnDomContentLoaded(object? sender, CoreWebView2DOMContentLoadedEventArgs e)
    {
        var core = Browser.CoreWebView2;
        if (core is null)
            return;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.WebMessageReceived += OnWebMessage;
        core.NewWindowRequested += (_, args) => args.Handled = true;
        LoadingLayer.IsVisible = false;
        _ready = true;
        SendState();
    }

    async void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var type = document.RootElement.TryGetProperty("type", out var typeValue)
                ? typeValue.GetString() ?? ""
                : "";
            switch (type)
            {
                case "ready":
                case "requestState":
                    SendState();
                    break;
                case "drag":
                    DragWindow();
                    break;
                case "window":
                    HandleWindow(document.RootElement.TryGetProperty("action", out var action)
                        ? action.GetString() ?? ""
                        : "");
                    break;
            }
        }
        catch (JsonException)
        {
            // Ignore malformed messages from the page.
        }
        await Task.CompletedTask;
    }

    void SendState() => Send(new
    {
        type = "state",
        appVersion = "0.22.3-avalonia",
        player = "Steve",
        accountType = "offline",
        accountLabel = "离线账号 · 本地启动",
        root = "",
        java = "",
        width = 1200,
        height = 780,
        selectedVersion = "",
        selectedInstance = "",
        ram = 4096,
        anonymous = false,
        communityKey = false,
        adminKey = false,
        skinStation = "",
        cape = "",
        versionGroups = Array.Empty<object>(),
        instances = Array.Empty<object>(),
        mods = Array.Empty<object>(),
        servers = Array.Empty<object>(),
        dataSources = new { announcements = "", servers = "", recommendations = "", quotes = "" },
        defaultDataSources = new { announcements = "", servers = "", recommendations = "", quotes = "" },
        writeEndpoint = ""
    });

    void Send(object payload)
    {
        if (_closed)
            return;
        Browser.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(payload, JsonOptions));
    }

    void HandleWindow(string action)
    {
        switch (action)
        {
            case "minimize":
                WindowState = WindowState.Minimized;
                break;
            case "maximize":
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                break;
            case "close":
                Close();
                break;
        }
    }

    void DragWindow()
    {
        var handle = TryGetPlatformHandle();
        if (handle is null)
            return;
        ReleaseCapture();
        SendMessage(handle.Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
    }

    [DllImport("user32.dll")]
    static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
}
