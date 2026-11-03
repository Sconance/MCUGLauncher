using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BlockLaunch;
using MCUGLauncher.Avalonia.Views;
using MCUGLauncher.Contracts;
using MCUGLauncher.UseCases;

namespace MCUGLauncher.Avalonia;

/// <summary>原生 Avalonia 外壳：导航、窗口生命周期与页面宿主；业务由 ILauncherService 承担。</summary>
public partial class ShellWindow : Window
{
    ILauncherService service = null!;
    readonly ShellDesktopHost host;
    readonly SettingsView settingsView;
    readonly ExtrasView extrasView;
    readonly HomeView homeView;
    readonly DownloadView downloadView;
    readonly Dictionary<string, Button> navigation = new(StringComparer.Ordinal);
    readonly DispatcherTimer toastTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    string page = "home";

    public ShellWindow()
    {
        InitializeComponent();
        navigation["home"] = HomeButton;
        navigation["download"] = DownloadButton;
        navigation["settings"] = SettingsButton;
        BrandVersion.Text = ProductInfo.Version;
        host = new ShellDesktopHost(
            () => Handle,
            HandleWindow,
            DragWindow,
            ResetWindow);
        service = new LauncherService(
            host,
            action => Dispatcher.UIThread.Post(action),
            _ => new MicrosoftLoginWebUi(),
            AvaloniaAvatarImage.Encode);
        settingsView = new SettingsView(service);
        extrasView = new ExtrasView(service);
        homeView = new HomeView(service);
        homeView.AccountRequested += () => OpenExtras("account");
        downloadView = new DownloadView(service);
        var placement = service.WindowPlacement;
        Width = Math.Clamp(placement.Width, LaunchLimits.ShellWidthMin, LaunchLimits.ShellWidthMax);
        Height = Math.Clamp(placement.Height, LaunchLimits.ShellHeightMin, LaunchLimits.ShellHeightMax);
        if (placement.Maximized)
            WindowState = WindowState.Maximized;
        Closing += (_, _) =>
        {
            try
            {
                service.RememberWindow(Bounds.Width, Bounds.Height, WindowState == WindowState.Maximized);
            }
            catch
            {
                // A locked settings file must not prevent closing.
            }
        };
        toastTimer.Tick += (_, _) =>
        {
            ToastLayer.IsVisible = false;
            toastTimer.Stop();
        };
        service.ErrorReceived += message => ShowToast("操作失败：" + message);
        service.ToastReceived += info => ShowToast(info.Message);
        HomeButton.Click += (_, _) => Show("home");
        DownloadButton.Click += (_, _) =>
        {
            downloadView.Activate();
            Show("download");
        };
        SettingsButton.Click += (_, _) => Show("settings");
        MoreButton.Click += (_, _) => OpenExtras();
        Render();
        Opened += async (_, _) =>
        {
            var preference = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(Handle, 33, ref preference, sizeof(int));
            await service.StartAsync();
            homeView.Activate();
        };
        Closed += (_, _) => service.Dispose();
    }

    void Show(string next)
    {
        page = next;
        Render();
    }

    void UpdateNavigation()
    {
        foreach (var button in navigation.Values)
            button.Classes.Remove("active");
        if (navigation.TryGetValue(page, out var active))
            active.Classes.Add("active");
        MoreButton.Classes.Remove("active");
        if (page == "extras")
            MoreButton.Classes.Add("active");
    }

    void OpenExtras(string? section = null)
    {
        if (section is not null)
            extrasView.Open(section);
        Show("extras");
    }

    void OnMinimizeWindow(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    void OnMaximizeWindow(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    void OnCloseWindow(object? sender, RoutedEventArgs e) => Close();

    void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (e.Source is Visual source && source.FindAncestorOfType<Button>(includeSelf: true) is not null)
            return;
        BeginMoveDrag(e);
    }

    void OnTitleBarDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Visual source && source.FindAncestorOfType<Button>(includeSelf: true) is not null)
            return;
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    void Render()
    {
        UpdateNavigation();
        if (page == "settings")
        {
            PageHost.Content = settingsView;
            return;
        }

        if (page == "extras")
        {
            PageHost.Content = extrasView;
            return;
        }

        if (page == "home")
        {
            PageHost.Content = homeView;
            return;
        }

        if (page == "download")
        {
            PageHost.Content = downloadView;
            return;
        }
    }

    void ShowToast(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
        ToastText.Text = message;
        ToastLayer.IsVisible = true;
        toastTimer.Stop();
        toastTimer.Start();
    }

    IntPtr Handle => TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

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
        if (Handle == IntPtr.Zero)
            return;
        ReleaseCapture();
        SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
    }

    void ResetWindow()
    {
        Width = 1200;
        Height = 780;
        WindowState = WindowState.Normal;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool ReleaseCapture();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
