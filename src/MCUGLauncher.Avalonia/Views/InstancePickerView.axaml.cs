using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.Avalonia.Views;

/// <summary>
/// 首页「当前实例」的二级页面：按加载器分组列出实例，可搜索，点条目切换，
/// 每行右侧可以直接用该实例启动或进入版本设置。
/// </summary>
internal partial class InstancePickerView : UserControl
{
    // 行高与行距的微调入口（单位是像素）：
    // 行高由 RowMinHeight 直接决定（两行文字内容约 34），行间距由 RowSpacing 决定。
    const double RowPaddingX = 6;
    const double RowMinHeight = 37;
    const double RowSpacing = 4;

    readonly HashSet<string> collapsed = new(StringComparer.OrdinalIgnoreCase);
    LauncherState? state;
    string selected = "";
    string query = "";

    public InstancePickerView()
    {
        InitializeComponent();
        RefreshVersionsButton.Click += (_, _) => RefreshRequested?.Invoke();
        ClosePickerButton.Click += (_, _) => CloseRequested?.Invoke();
        PickerSearch.TextChanged += (_, _) =>
        {
            query = PickerSearch.Text?.Trim() ?? "";
            Render();
        };
    }

    /// <summary>请求收起二级页面，回到首页信息流。</summary>
    public event Action? CloseRequested;

    /// <summary>按「刷新版本」重新扫描本机。</summary>
    public event Action? RefreshRequested;

    /// <summary>点中某个实例（切换当前实例）。</summary>
    public event Action<string>? InstancePicked;

    /// <summary>行尾「▷」：用该实例启动。</summary>
    public event Action<string>? LaunchRequested;

    /// <summary>行尾「⚙」：进入该实例的版本设置。</summary>
    public event Action<string>? SettingsRequested;

    public void SetState(LauncherState? snapshot, string current)
    {
        state = snapshot;
        selected = current;
        Render();
    }

    public void SetStatus(string message)
    {
        PickerStatus.Text = message;
        PickerStatus.IsVisible = message.Length > 0;
    }

    void Render()
    {
        GroupPanel.Children.Clear();
        var instances = Instances();
        PickerSummary.Text = instances.Count == 0
            ? "当前目录没有可用实例，先在下载页安装一个。"
            : $"共 {instances.Count} 个实例。点条目切换当前实例，▷ 直接启动，⚙ 进入版本设置。";

        var filtered = instances.Where(Matches).ToArray();
        foreach (var group in filtered
                     .GroupBy(item => Group(item.Loader))
                     .OrderBy(group => group.Key == "原版" ? 0 : 1)
                     .ThenBy(group => group.Key, StringComparer.Ordinal))
        {
            GroupPanel.Children.Add(GroupCard(group.Key, group.ToArray()));
        }

        if (filtered.Length == 0 && instances.Count > 0)
            GroupPanel.Children.Add(Muted("没有匹配的实例。"));
    }

    Control GroupCard(string loader, IReadOnlyList<InstanceInfo> items)
    {
        var open = !collapsed.Contains(loader);
        var label = (loader == "原版" ? "原版游戏" : loader + " 版本") + "（" + items.Count + "）";
        // 箭头用同族的 ▾/▸ 且占固定宽度，展开前后标题文字的左边缘与基线都不会跳。
        var arrow = new TextBlock
        {
            Text = open ? "▾" : "▸",
            Width = 12,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Classes = { "muted" }
        };
        var title = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        var header = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { arrow, title }
            },
            Classes = { "link", "groupHeader" },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left
        };
        header.Click += (_, _) =>
        {
            if (collapsed.Contains(loader))
                collapsed.Remove(loader);
            else
                collapsed.Add(loader);
            Render();
        };

        var rows = new StackPanel { Spacing = RowSpacing, IsVisible = open };
        foreach (var item in items)
            rows.Children.Add(Row(item));

        var card = new Border
        {
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.Parse("#FBFDFC")),
            BorderBrush = new SolidColorBrush(Color.Parse("#E4EBF1")),
            BorderThickness = new Thickness(1),
            Child = new StackPanel { Spacing = 4, Children = { header, rows } }
        };
        return card;
    }

    Control Row(InstanceInfo item)
    {
        var row = InstanceListItem.From(item);
        var lines = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        lines.Children.Add(new TextBlock
        {
            Text = row.Title,
            FontSize = 12.5,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        if (row.HasSubtitle)
            lines.Children.Add(new TextBlock
            {
                Text = row.Subtitle,
                FontSize = 10.5,
                Classes = { "muted" },
                TextTrimming = TextTrimming.CharacterEllipsis
            });

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        head.Children.Add(new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(7),
            Background = row.Accent,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = row.Glyph,
                Foreground = Brushes.White,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        });
        lines.Margin = new Thickness(9, 0, 0, 0);
        Grid.SetColumn(lines, 1);
        head.Children.Add(lines);

        var pick = new Button
        {
            Content = head,
            Classes = { "instanceRow" },
            Padding = new Thickness(RowPaddingX, 0),
            MinHeight = RowMinHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        pick.Classes.Set("selected", item.Name == selected);
        pick.Click += (_, _) => InstancePicked?.Invoke(item.Name);

        var launch = new Button
        {
            Content = "▷",
            Classes = { "link" },
            Width = 30,
            Padding = new Thickness(0)
        };
        ToolTip.SetTip(launch, "用这个实例启动");
        launch.Click += (_, _) => LaunchRequested?.Invoke(item.Name);

        var settings = new Button
        {
            Content = "⚙",
            Classes = { "link" },
            Width = 30,
            Padding = new Thickness(0)
        };
        ToolTip.SetTip(settings, "进入版本设置");
        settings.Click += (_, _) => SettingsRequested?.Invoke(item.Name);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,34,34") };
        grid.Children.Add(pick);
        Grid.SetColumn(launch, 1);
        launch.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(launch);
        Grid.SetColumn(settings, 2);
        settings.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(settings);
        return grid;
    }

    bool Matches(InstanceInfo item)
    {
        if (query.Length == 0)
            return true;
        var row = InstanceListItem.From(item);
        var haystack = row.Title + " " + row.Subtitle + " " + item.Name + " " + item.Loader;
        return haystack.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    static string Group(string loader) =>
        loader.Length == 0 || loader == "原版" ? "原版" : loader;

    IReadOnlyList<InstanceInfo> Instances() => state is null
        ? Array.Empty<InstanceInfo>()
        : state.Instances.Where(item => item.Name.Length > 0 && !item.IsPlaceholder).ToArray();

    static TextBlock Muted(string text) => new() { Text = text, Classes = { "muted" } };
}
