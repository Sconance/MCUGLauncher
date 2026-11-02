using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BlockLaunch;
using MCUGLauncher.Avalonia.Views;

namespace MCUGLauncher.Avalonia;

/// <summary>原生 Avalonia 外壳：导航、窗口生命周期与页面宿主；业务由 LauncherFacade 承担。</summary>
public partial class ShellWindow : Window
{
    LauncherFacade facade = null!;
    readonly ShellDesktopHost host;
    readonly SettingsView settingsView;
    readonly InstancesView instancesView;
    readonly HomeView homeView;
    readonly DownloadView downloadView;
    readonly AccountView accountView;
    readonly GamesView gamesView;
    readonly BookmarksView bookmarksView;
    readonly CommunityView communityView;
    readonly TasksView tasksView;
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
            message => facade.SendMessage(message),
            HandleWindow,
            DragWindow,
            ResetWindow);
        facade = new LauncherFacade(host);
        settingsView = new SettingsView(facade);
        instancesView = new InstancesView(facade);
        homeView = new HomeView(facade);
        homeView.AccountRequested += () => Show("account");
        downloadView = new DownloadView(facade);
        accountView = new AccountView(facade);
        gamesView = new GamesView(facade);
        bookmarksView = new BookmarksView(facade);
        communityView = new CommunityView(facade);
        tasksView = new TasksView(facade);
        Width = Math.Clamp(facade.Config.WindowWidth, 960, 7680);
        Height = Math.Clamp(facade.Config.WindowHeight, 640, 4320);
        if (facade.Config.WindowMaximized)
            WindowState = WindowState.Maximized;
        Closing += (_, _) =>
        {
            try
            {
                facade.RememberWindow(Bounds.Width, Bounds.Height, WindowState == WindowState.Maximized);
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
        facade.LaunchResultReceived += info => ShowToast(info.Message);
        facade.AuthResultReceived += info => ShowToast(info.Message);
        facade.ErrorReceived += message => ShowToast("操作失败：" + message);
        facade.ToastReceived += info => ShowToast(info.Message);
        HomeButton.Click += (_, _) => Show("home");
        DownloadButton.Click += (_, _) =>
        {
            downloadView.Activate();
            Show("download");
        };
        SettingsButton.Click += (_, _) => Show("settings");
        Render();
        Opened += async (_, _) =>
        {
            var preference = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(Handle, 33, ref preference, sizeof(int));
            await facade.StartAsync();
        };
        Closed += (_, _) => facade.Dispose();
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
        if (page is not ("home" or "download" or "settings"))
            MoreButton.Classes.Add("active");
    }

    void OnInstancesMenu(object? sender, RoutedEventArgs e) => Show("instances");

    void OnAccountMenu(object? sender, RoutedEventArgs e) => Show("account");

    void OnTasksMenu(object? sender, RoutedEventArgs e)
    {
        tasksView.Activate();
        Show("tasks");
    }

    void OnGamesMenu(object? sender, RoutedEventArgs e)
    {
        gamesView.Activate();
        Show("games");
    }

    void OnBookmarksMenu(object? sender, RoutedEventArgs e) => Show("bookmarks");

    void OnCommunityMenu(object? sender, RoutedEventArgs e) => Show("community");

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
        if (page == "instances")
        {
            PageHost.Content = instancesView;
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
        if (page == "account")
        {
            accountView.Activate();
            PageHost.Content = accountView;
            return;
        }
        if (page == "games")
        {
            PageHost.Content = gamesView;
            return;
        }
        if (page == "bookmarks")
        {
            PageHost.Content = bookmarksView;
            return;
        }
        if (page == "community")
        {
            PageHost.Content = communityView;
            return;
        }
        if (page == "tasks")
        {
            PageHost.Content = tasksView;
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
