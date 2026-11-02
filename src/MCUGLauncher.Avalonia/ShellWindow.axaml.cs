using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
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
    readonly Dictionary<string, Button> navigation = new(StringComparer.Ordinal);
    readonly DispatcherTimer toastTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    StackPanel? pageContent;
    IReadOnlyList<TaskInfo> tasks = [];
    string page = "home";

    public ShellWindow()
    {
        InitializeComponent();
        navigation["home"] = HomeButton;
        navigation["instances"] = MaintenanceButton;
        navigation["download"] = DownloadButton;
        navigation["account"] = AccountButton;
        navigation["tasks"] = TasksButton;
        navigation["games"] = GamesButton;
        navigation["bookmarks"] = BookmarksButton;
        navigation["community"] = CommunityButton;
        navigation["settings"] = SettingsButton;
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
        Width = Math.Clamp(facade.Config.WindowWidth, 900, 7680);
        Height = Math.Clamp(facade.Config.WindowHeight, 600, 4320);
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
        facade.TasksChanged += list =>
        {
            tasks = list;
            if (page == "tasks")
                Render();
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
        MaintenanceButton.Click += (_, _) => Show("instances");
        AccountButton.Click += (_, _) => Show("account");
        TasksButton.Click += (_, _) => { page = "tasks"; _ = facade.RequestTasksAsync(); Render(); };
        GamesButton.Click += (_, _) => { gamesView.Activate(); Show("games"); };
        BookmarksButton.Click += (_, _) => Show("bookmarks");
        CommunityButton.Click += (_, _) => Show("community");
        SettingsButton.Click += (_, _) => Show("settings");
        Render();
        Opened += async (_, _) => await facade.StartAsync();
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
        var panel = new StackPanel { Spacing = 12 };
        pageContent = panel;
        PageHost.Content = new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        if (page == "tasks")
            RenderTasks();
    }

    void RenderTasks()
    {
        AddHeading("下载任务");
        if (tasks.Count == 0)
        {
            Add("当前没有排队或运行中的任务。");
            return;
        }
        foreach (var task in tasks)
        {
            Add($"{task.Name} · {task.State} · {task.Percent:0}%");
            if (task.Message.Length > 0)
                Add("  " + task.Message);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            foreach (var (label, action) in new[]
                     {
                         ("暂停", "pause"), ("继续", "resume"), ("取消", "cancel"), ("删除", "delete")
                     })
            {
                var button = new Button { Content = label };
                var taskId = task.Id;
                var taskAction = action;
                button.Click += async (_, _) =>
                {
                    await facade.TaskActionAsync(taskId, taskAction);
                    await facade.RequestTasksAsync();
                };
                row.Children.Add(button);
            }
            Add(row);
        }
        var refresh = new Button { Content = "刷新任务", HorizontalAlignment = HorizontalAlignment.Left };
        refresh.Click += async (_, _) => await facade.RequestTasksAsync();
        Add(refresh);
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

    void AddHeading(string text) => Add(new TextBlock
    {
        Text = text,
        FontSize = 18,
        FontWeight = FontWeight.Bold,
        Margin = new Thickness(0, 12, 0, 0)
    });

    void Add(string text) => Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });

    void Add(Control control) => pageContent?.Children.Add(control);
}
