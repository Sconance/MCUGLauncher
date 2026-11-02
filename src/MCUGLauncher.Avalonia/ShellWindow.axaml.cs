using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MCUGLauncher.Avalonia.Presentation;
using MCUGLauncher.Avalonia.Views;
namespace MCUGLauncher.Avalonia;

// Composition and native window lifetime only. Page views own presentation, use cases own work.
public partial class ShellWindow : Window
{
    readonly LauncherFacade session;
    NativePage? page;
    string group = "";
    public ShellWindow() : this(null) { }
    internal ShellWindow(LauncherFacade? injected)
    {
        InitializeComponent();
        Title = BlockLaunch.ProductInfo.Name + " · " + BlockLaunch.ProductInfo.Version + " · AI 迁移框架尝试";
        session = injected ?? App.CreateSession(new ShellDesktopHost(this, value => session!.SendMessage(value)));
        var size = session.WindowPreferences;
        Width = Math.Clamp(size.Width, 960, 3840);
        Height = Math.Clamp(size.Height, 640, 2160);
        if (size.Maximized)
            WindowState = WindowState.Maximized;
        session.Navigate = Navigate;
        session.PropertyChanged += Changed;
        session.MessageReceived += OnMessage;
        BrandPanel.Children.Add(Ui.Logo());
        BrandPanel.Children.Add(Ui.Text("MCUG Launcher", 20));
        foreach (var (label, id) in new[] { ("▷ 启动", "home"), ("↓ 下载", "vanilla"), ("⚙ 设置", "settings") })
            TopNavigation.Children.Add(Ui.Action(label, () => Navigate(id)));
        TaskButton.Click += (_, _) => Navigate("tasks");
        Opened += async (_, _) => { if (injected == null) await session.SendAsync(new { type = "ready" }); };
        Closed += (_, _) => { session.RememberWindow(Width, Height, WindowState == WindowState.Maximized); page?.Dispose(); session.PropertyChanged -= Changed; session.MessageReceived -= OnMessage; session.Dispose(); };
        Navigate("home");
        Changed(this, new(null));
    }
    internal void Navigate(string id)
    {
        var entry = NavigationCatalog.All.FirstOrDefault(i => i.Id == id) ?? NavigationCatalog.All[0];
        if (entry.Group != group)
        {
            group = entry.Group;
            Sidebar.Children.Clear();
            foreach (var item in NavigationCatalog.All.Where(i => i.Group == group))
            {
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
        page?.Dispose();
        page = entry.Create(session);
        PageHost.Content = page;
        PageScroll.Offset = default;
        foreach (var button in Sidebar.Children.OfType<Button>())
            button.Classes.Set("selected", button.Tag as string == entry.Id);
    }
    void Changed(object? sender, PropertyChangedEventArgs args)
    {
        StatusText.Text = session.Status;
        var active = session.Tasks.Where(t => t.State is "Running" or "Queued" or "Paused").ToArray();
        TaskButton.Content = "↓ 下载任务（" + active.Length + "）";
        TaskProgress.IsVisible = active.Length > 0;
        TaskProgress.Value = active.Length > 0 ? active.Average(t => t.Percent ?? 0) : 0;
    }
    async void OnMessage(NativeMessage message)
    {
        if (message.Type == "muaProfiles")
            try
            {
                await AccountsPage.SelectProfile(session, this, message);
            }
            catch (Exception error) { session.Report(error.Message); }
    }
}
