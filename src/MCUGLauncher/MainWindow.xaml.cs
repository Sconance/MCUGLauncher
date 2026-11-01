using System.Windows;
using MCUGLauncher.Ui;
using MCUGLauncher.UseCases;
using BlockLaunch;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace MCUGLauncher;

/// <summary>WebView2 窗口宿主，组装应用用例与原生窗口适配器。</summary>
/// <remarks>只处理窗口、对话框和浏览器生命周期；账号、下载和启动由 UseCases 编排。</remarks>
public partial class MainWindow : Window, IDesktopHost
{
    readonly LauncherApplication application;
    readonly WindowChromeController chrome;
    readonly UiBridge bridge;
    bool _ready;
    bool closed;
    string _uiDirectory = "";

    public MainWindow()
    {
        InitializeComponent();
        chrome = new WindowChromeController(this);
        bridge = new UiBridge(Browser, Dispatcher, () => closed);
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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        chrome.Initialize();
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
            _uiDirectory = EmbeddedUiCache.Extract();
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

    public void Window(string action) => chrome.Handle(action);

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
            Filter = target switch
            {
                "java" => "Java|java.exe;javaw.exe|所有文件|*.*",
                "offlineSkin" => "PNG 皮肤|*.png",
                "pack" => "整合包|*.mrpack;*.zip",
                _ => "所有文件|*.*"
            },
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
        chrome.Reset();
        Send(new
        {
            type = "toast",
            message = "窗口已恢复为 1200 × 780。"
        });
    }

    void Send(object message) => bridge.Send(message);

    void IDesktopHost.Drag() => chrome.BeginDrag();
}
