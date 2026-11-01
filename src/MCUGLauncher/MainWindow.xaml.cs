using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using MCUGLauncher.UseCases;
using BlockLaunch;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace MCUGLauncher;

public partial class MainWindow : Window, IDesktopHost
{
    readonly LauncherApplication application;
    bool _ready;
    bool closed;
    string _uiDirectory = "";

    public MainWindow()
    {
        InitializeComponent();
        application = new LauncherApplication(Send, this, dispatch: action =>
        {
            if (!closed)
                Dispatcher.BeginInvoke(action);
        }, microsoftBrowser: directory => new MicrosoftLoginWebUi(directory));
        Width = Math.Clamp(application.Config.WindowWidth, 960, 7680);
        Height = Math.Clamp(application.Config.WindowHeight, 640, 4320);
        if (application.Config.WindowMaximized)
            WindowState = WindowState.Maximized;
        Closing += (_, _) =>
        {
            var bounds = WindowState == WindowState.Normal ? new Rect(0, 0, Width, Height) : RestoreBounds;
            try
            {
                application.RememberWindow(bounds.Width, bounds.Height, WindowState == WindowState.Maximized);
            }
            catch (Exception) { /* A locked settings file must not prevent closing. */ }
        };
        Loaded += OnLoaded;
        Closed += (_, _) => { closed = true; application.Dispose(); Browser.Dispose(); };
    }

    [DllImport("user32.dll")]
    static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var preference = 2;
        DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 33, ref preference, sizeof(int));
    }

    void BeginWindowDrag()
    {
        // Delegate moving, snapping and monitor boundaries to the Windows move loop.
        ReleaseCapture();
        SendMessage(new WindowInteropHelper(this).Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
    }

    void FitUiToWindow()
    {
        // The UI is laid out for a 1200x780 DIP client; WebView2 already maps CSS pixels to DIPs.
        Browser.ZoomFactor = 1.0;
    }

    async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _uiDirectory = PrepareUi();
            var dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MCUGLauncher", "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(null, dataFolder);
            await Browser.EnsureCoreWebView2Async(environment);
            if (closed)
                return;
            FitUiToWindow();
            Browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "app.local", _uiDirectory, CoreWebView2HostResourceAccessKind.Allow);
            Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Browser.CoreWebView2.NavigationStarting += (_, args) => args.Cancel = !IsLocalPage(args.Uri);
            Browser.CoreWebView2.NewWindowRequested += (_, args) => args.Handled = true;
            Browser.CoreWebView2.WebMessageReceived += OnWebMessage;
            Browser.NavigationCompleted += OnNavigationCompleted;
            Browser.Source = new Uri("https://app.local/index.html");
        }
        catch (Exception ex)
        {
            if (closed)
                return;
            MessageBox.Show("界面初始化失败。\n\n" + ex.Message, "MCUG Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    static string PrepareUi()
    {
        var target = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MCUGLauncher",
            "ui-" + ProductInfo.Version);
        Directory.CreateDirectory(target);
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var name in assembly.GetManifestResourceNames().Where(name => name.StartsWith("MCUGLauncher.Ui.")))
        {
            var file = Path.Combine(target, name["MCUGLauncher.Ui.".Length..]);
            // Embedded resource names flatten folder separators to dots.
            if (file.EndsWith("assets.logo.png", StringComparison.OrdinalIgnoreCase))
                file = Path.Combine(target, "assets", "logo.png");
            var folder = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);
            using var source = assembly.GetManifestResourceStream(name);
            if (source == null)
                continue;
            using var destination = File.Create(file);
            source.CopyTo(destination);
        }
        return target;
    }

    void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            LoadingLayer.Visibility = Visibility.Collapsed;
            if (_ready)
                application.SendState();
        }
        else
        {
            LoadingLayer.Visibility = Visibility.Visible;
        }
    }

    async void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!IsLocalPage(e.Source))
            return;
        _ready = true;
        await application.HandleAsync(e.WebMessageAsJson);
    }

    internal static bool IsLocalPage(string source) =>
        Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.Host == "app.local" && uri.IsDefaultPort;

    public bool ConfirmStopGame() => MessageBox.Show(this, "确认关闭这个游戏？未保存的内容可能丢失。", "关闭游戏",
        MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;

    public void Window(string action)
    {
        switch (action)
        {
            case "minimize":
                WindowState = WindowState.Minimized;
                break;
            case "close":
                Close();
                break;
            case "maximize":
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                break;
        }
    }

    public void PickFolder(string target)
    {
        var dialog = new OpenFolderDialog { Title = "选择文件夹" };
        if (!string.IsNullOrWhiteSpace(application.Config.Root))
            dialog.InitialDirectory = application.Config.Root;
        if (dialog.ShowDialog(this) == true)
            Send(new
            {
                type = "pickedPath",
                target,
                path = dialog.FolderName
            });
    }

    public bool ConfirmUninstall(string name) => MessageBox.Show(this,
        "确认执行「" + name + "」的卸载计划？列出的文件将永久删除，请先确认备份。", "确认卸载",
        MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;

    public void PickSaveFile(string target)
    {
        var dialog = new SaveFileDialog { Title = "导出实例", Filter = "整合包 ZIP|*.zip", DefaultExt = ".zip", FileName = "instance-export.zip" };
        if (dialog.ShowDialog(this) == true)
            Send(new
            {
                type = "pickedPath",
                target,
                path = dialog.FileName
            });
    }

    public void PickFile(string target)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择文件",
            Filter = target == "java" ? "Java|java.exe;javaw.exe|所有文件|*.*" : "所有文件|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
            Send(new
            {
                type = "pickedPath",
                target,
                path = dialog.FileName
            });
    }

    public void ResetWindow()
    {
        Width = 1200;
        Height = 780;
        WindowState = WindowState.Normal;
        Send(new
        {
            type = "toast",
            message = "窗口已恢复为 1200 × 780。"
        });
    }

    void Send(object payload)
    {
        if (closed)
            return;
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => Send(payload));
            return;
        }
        Browser.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }

    void IDesktopHost.Drag() => BeginWindowDrag();
}
