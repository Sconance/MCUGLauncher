using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using MCUGLauncher.UseCases;
using Microsoft.Web.WebView2.Core;

namespace MCUGLauncher.Avalonia;

public partial class MainWindow : Window, IDesktopHost
{
    readonly LauncherApplication application;
    bool _ready;
    bool _closed;

    public MainWindow()
    {
        InitializeComponent();
        application = new LauncherApplication(Send, this, dispatch: action =>
        {
            if (!_closed)
                Dispatcher.UIThread.Post(action);
        }, microsoftBrowser: _ => new MicrosoftLoginWebUi(), avatarEncoder: AvaloniaAvatarImage.Encode);
        Width = Math.Clamp(application.Config.WindowWidth, 960, 7680);
        Height = Math.Clamp(application.Config.WindowHeight, 640, 4320);
        if (application.Config.WindowMaximized)
            WindowState = WindowState.Maximized;
        Closing += (_, _) =>
        {
            try
            {
                application.RememberWindow(Bounds.Width, Bounds.Height, WindowState == WindowState.Maximized);
            }
            catch
            {
                // A locked settings file must not prevent closing.
            }
        };
        Closed += (_, _) =>
        {
            _closed = true;
            application.Dispose();
        };
        Browser.DOMContentLoaded += OnDomContentLoaded;
    }

    IntPtr Handle => TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

    async void OnDomContentLoaded(object? sender, CoreWebView2DOMContentLoadedEventArgs e)
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
        await application.HandleAsync("""{"type":"ready"}""");
    }

    async void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!_ready)
            return;
        await application.HandleAsync(e.WebMessageAsJson);
    }

    void Send(object payload)
    {
        if (_closed)
            return;
        Browser.CoreWebView2?.PostWebMessageAsJson(
            JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
    }

    public bool ConfirmStopGame() =>
        NativeDialogs.Confirm(Handle, "确认关闭这个游戏？未保存的内容可能丢失。", "关闭游戏");

    public bool ConfirmUninstall(string name) =>
        NativeDialogs.Confirm(Handle, "确认执行「" + name + "」的卸载计划？列出的文件将永久删除，请先确认备份。", "确认卸载");

    public void PickFolder(string target)
    {
        var path = NativeDialogs.PickFolder(Handle, "选择文件夹");
        if (path is not null)
            Send(new { type = "pickedPath", target, path });
    }

    public void PickFile(string target)
    {
        var filter = target switch
        {
            "java" => "Java|java.exe;javaw.exe|所有文件|*.*",
            "offlineSkin" => "PNG 皮肤|*.png",
            "pack" => "整合包|*.mrpack;*.zip",
            _ => "所有文件|*.*"
        };
        var path = NativeDialogs.OpenFile(Handle, filter, application.Config.Root);
        if (path is not null)
            Send(new { type = "pickedPath", target, path });
    }

    public void PickSaveFile(string target)
    {
        var path = NativeDialogs.SaveFile(Handle, "整合包 ZIP|*.zip", "instance-export.zip", null);
        if (path is not null)
            Send(new { type = "pickedPath", target, path });
    }

    public void Window(string action)
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

    public void Drag()
    {
        if (Handle == IntPtr.Zero)
            return;
        ReleaseCapture();
        SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
    }

    public void ResetWindow()
    {
        Width = 1200;
        Height = 780;
        WindowState = WindowState.Normal;
        Send(new { type = "toast", message = "窗口已恢复为 1200 × 780。" });
    }

    [DllImport("user32.dll")]
    static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
}
