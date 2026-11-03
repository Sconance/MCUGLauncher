using Avalonia.Controls;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.Avalonia.Views;

/// <summary>“更多”整合页：左侧分区导航，右侧承载原先放在标题栏下拉菜单里的六个页面。</summary>
internal partial class ExtrasView : UserControl
{
    readonly Dictionary<string, Control> pages = new(StringComparer.Ordinal);

    public ExtrasView()
    {
        InitializeComponent();
    }

    public ExtrasView(ILauncherService service) : this()
    {
        pages["instances"] = new InstancesView(service);
        pages["account"] = new AccountView(service);
        pages["tasks"] = new TasksView(service);
        pages["games"] = new GamesView(service);
        pages["bookmarks"] = new BookmarksView(service);
        pages["community"] = new CommunityView(service);
        Nav.SelectedIndex = 0;
        ShowPage("instances");
    }

    /// <summary>供外壳跳转：切到指定分区，键名与 XAML 中 ListBoxItem 的 Tag 对应。</summary>
    public void Open(string key)
    {
        if (!pages.ContainsKey(key))
            return;
        foreach (var item in Nav.Items.OfType<ListBoxItem>())
            item.IsSelected = (string?)item.Tag == key;
        ShowPage(key);
    }

    void OnNavSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Nav.SelectedItem is ListBoxItem { Tag: string key })
            ShowPage(key);
    }

    void ShowPage(string key)
    {
        if (!pages.TryGetValue(key, out var target))
            return;
        PageHost.Content = target;
        switch (target)
        {
            case AccountView account:
                account.Activate();
                break;
            case TasksView tasks:
                tasks.Activate();
                break;
            case GamesView games:
                games.Activate();
                break;
        }
    }
}
