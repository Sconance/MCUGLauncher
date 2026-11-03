using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Input;
using MCUGLauncher.Avalonia.Presentation;
using MCUGLauncher.Avalonia.Views;
namespace MCUGLauncher.Avalonia;

// Composition and native window lifetime only. Page views own presentation, use cases own work.
public partial class ShellWindow : Window
{
    readonly ILauncherService session;
    readonly UiContext ui;
    readonly NotificationView notice = new();
    readonly BackgroundView background = new();
    readonly FileDropView drops;
    NativePage? page;
    string group = "";
    public ShellWindow() : this(null) { }
    internal ShellWindow(ILauncherService? injected, UiContext? injectedUi = null)
    {
        InitializeComponent();
        Title = MCUGLauncher.ProductInfo.Name + " · " + MCUGLauncher.ProductInfo.Version + " · AI 迁移框架尝试";
        session = injected ?? App.CreateSession(new ShellDesktopHost(this));
        ui = injectedUi ?? new UiContext(session);
        var size = session.WindowPlacement;
        Width = Math.Clamp(size.Width, 960, 3840);
        Height = Math.Clamp(size.Height, 640, 2160);
        if (size.Maximized)
            WindowState = WindowState.Maximized;
        ui.Navigate = Navigate;
        ui.PropertyChanged += Changed;
        session.AccountSwitchResultReceived += OnAccountSwitch;
        session.MuaProfilesReceived += OnProfiles;
        BrandPanel.Children.Add(Ui.Logo(30));
        var brandName = Ui.Text("MCUG Launcher", 16);
        brandName.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center;
        BrandPanel.Children.Add(brandName);
        foreach (var (label, id) in new[] { ("▷ 启动", "home"), ("↓ 下载", "community"), ("⚙ 设置", "settings") })
        {
            var button = Ui.Action(label, () => Navigate(id));
            button.Tag = id;
            button.Classes.Add("top-nav");
            TopNavigation.Children.Add(button);
        }
        drops = new FileDropView(session, ui, this);
        DropHost.Content = drops;
        NoticeHost.Content = notice;
        BackgroundHost.Content = background;
        PageHost.SizeChanged += (_, _) => { if (page != null) page.MaxWidth = PageHost.Bounds.Width; };
        MinimizeButton.Click += (_, _) => WindowState = WindowState.Minimized;
        MaximizeButton.Click += (_, _) => ToggleMaximized();
        CloseButton.Click += (_, _) => Close();
        TitleBar.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || e.Source is Button
                || (e.Source as Visual)?.GetVisualAncestors().OfType<Button>().Any() == true)
                return;
            if (e.ClickCount == 2)
                ToggleMaximized();
            else
                BeginMoveDrag(e);
        };
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty)
            {
                MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
                ToolTip.SetTip(MaximizeButton, WindowState == WindowState.Maximized ? "还原" : "最大化");
            }
        };
        TaskButton.Click += (_, _) => Navigate("tasks");
        TaskButton.Classes.Add("soft");
        Opened += async (_, _) => { if (injected == null) await session.StartAsync(); };
        Closed += (_, _) => { session.RememberWindow(Width, Height, WindowState == WindowState.Maximized); notice.Dispose(); background.Dispose(); drops.Dispose(); page?.Dispose(); ui.PropertyChanged -= Changed; session.AccountSwitchResultReceived -= OnAccountSwitch; session.MuaProfilesReceived -= OnProfiles; ui.Dispose(); session.Dispose(); };
        Navigate("home");
        Changed(this, new(null));
    }
    void ToggleMaximized() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    internal void Navigate(string id)
    {
        var entry = NavigationCatalog.All.FirstOrDefault(i => i.Id == id) ?? NavigationCatalog.All[0];
        if (entry.Group != group)
        {
            group = entry.Group;
            Sidebar.Children.Clear();
            var section = "";
            foreach (var item in NavigationCatalog.All.Where(i => i.Group == group && i.Visible))
            {
                if (item.Section != section)
                {
                    section = item.Section;
                    Sidebar.Children.Add(Ui.Text(section, 11));
                }
                var button = Ui.Action(item.Label, () => Navigate(item.Id));
                button.Tag = item.Id;
                if (item.IconPath is { Length: > 0 } image)
                    button.Content = Ui.Row(new Image { Source = new global::Avalonia.Media.Imaging.Bitmap(global::Avalonia.Platform.AssetLoader.Open(new Uri(image))), Width = 20, Height = 20 }, Ui.Text(item.Label));
                else
                    button.Content = item.Symbol + "  " + item.Label;
                button.Classes.Add("nav");
                Sidebar.Children.Add(button);
            }
        }
        PageGrid.ColumnDefinitions[0].Width = new GridLength(group == "home" ? 0 : 190);
        foreach (var button in TopNavigation.Children.OfType<Button>())
            button.Classes.Set("selected", button.Tag as string == (group == "home" ? "home" : group == "download" ? "community" : "settings"));
        page?.Dispose();
        page = entry.Create(session, ui);
        if (PageHost.Bounds.Width > 0)
            page.MaxWidth = PageHost.Bounds.Width;
        PageHost.Content = page;
        PageScroll.Offset = default;
        foreach (var button in Sidebar.Children.OfType<Button>())
            button.Classes.Set("selected", button.Tag as string == entry.Id);
    }
    void Changed(object? sender, PropertyChangedEventArgs args)
    {
        background.Update(session.State.Preferences);
        if (args.PropertyName == nameof(UiContext.Status))
            notice.Show(ui.Status);
        var active = session.Tasks.Where(t => t.State is "Running" or "Queued" or "Paused").ToArray();
        TaskButton.Content = "↓ 下载任务（" + active.Length + "）";
        TaskProgress.IsVisible = active.Length > 0;
        TaskProgress.Value = active.Length > 0 ? active.Average(t => t.Percent ?? 0) : 0;
    }
    async void OnAccountSwitch(AccountSwitchResultInfo message)
    {
        if (!message.NeedsLogin)
            return;
        try
        {
            await AccountDialogs.Open(session, ui, this, message.Account);
        }
        catch (Exception error) { ui.Report(error.Message); }
    }
    async void OnProfiles(MuaProfilesInfo message)
    {
        try
        {
            await AccountDialogs.SelectProfile(session, ui, this, message);
        }
        catch (Exception error) { ui.Report(error.Message); }
    }
}
