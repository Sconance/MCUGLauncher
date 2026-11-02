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
    readonly DispatcherTimer toastTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    StackPanel? pageContent;
    LauncherState? typed;
    IReadOnlyList<TaskInfo> tasks = [];
    string page = "home";

    public ShellWindow()
    {
        InitializeComponent();
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
        facade.StateChanged += snapshot =>
        {
            typed = snapshot;
            if (page == "account")
                Render();
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
        SettingsButton.Click += (_, _) => Show("settings");
        ClassicButton.Click += (_, _) => new MainWindow().Show();
        Render();
        Opened += async (_, _) => await facade.StartAsync();
        Closed += (_, _) => facade.Dispose();
    }

    void Show(string next)
    {
        page = next;
        Render();
    }

    void Render()
    {
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
        var panel = new StackPanel { Spacing = 12 };
        pageContent = panel;
        PageHost.Content = new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        if (page == "account")
            RenderAccount();
        else if (page == "tasks")
            RenderTasks();
    }

    void RenderAccount()
    {
        var snapshot = typed;
        AddHeading("账号");
        Add("当前：" + (snapshot?.AccountLabel is { Length: > 0 } label ? label : "离线账号"));
        Add("玩家：" + (snapshot?.Player ?? ""));
        var microsoft = new Button { Content = "登录微软账号", HorizontalAlignment = HorizontalAlignment.Left };
        microsoft.Click += async (_, _) => { await facade.LoginMicrosoftAsync(); await facade.RequestStateAsync(); };
        Add(microsoft);
        AddHeading("MUA 账号");
        var account = new TextBox { Watermark = "MUA 账号" };
        var password = new TextBox { Watermark = "密码", PasswordChar = '●' };
        Add(account);
        Add(password);
        var muaLogin = new Button { Content = "登录 MUA", HorizontalAlignment = HorizontalAlignment.Left };
        muaLogin.Click += async (_, _) =>
        {
            await facade.LoginMuaAsync(account.Text?.Trim() ?? "", password.Text ?? "");
            await facade.RequestStateAsync();
        };
        Add(muaLogin);
        var offline = new Button { Content = "切换到离线账号", HorizontalAlignment = HorizontalAlignment.Left };
        offline.Click += async (_, _) => { await facade.SwitchAccountModeAsync("offline"); await facade.RequestStateAsync(); };
        Add(offline);
        var forget = new Button { Content = "忘记已保存账号", HorizontalAlignment = HorizontalAlignment.Left };
        forget.Click += async (_, _) =>
        {
            await facade.ForgetAccountAsync(snapshot?.AccountType is { Length: > 0 } mode ? mode : "offline");
            await facade.RequestStateAsync();
        };
        Add(forget);
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
