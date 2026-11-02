using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using MCUGLauncher.Avalonia.Presentation;
namespace MCUGLauncher.Avalonia.Views;

internal sealed class HomePage : NativePage
{
    readonly Grid account = new() { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
    readonly StackPanel launch = Ui.Stack();
    readonly ContentControl right = new();
    readonly InstancePickerView picker;
    readonly HomeFeedPanel servers;
    readonly HomeFeedPanel announcements;
    readonly HomeFeedPanel recommendations;
    readonly HomeFeedPanel quotes;
    readonly Grid feeds;
    readonly Grid layout;
    readonly Border accountCard;
    bool expanded;
    public HomePage(LauncherFacade session) : base(session)
    {
        picker = new InstancePickerView(session, () => Toggle(false));
        servers = new HomeFeedPanel(session, "servers");
        announcements = new HomeFeedPanel(session, "announcements");
        recommendations = new HomeFeedPanel(session, "recommendations");
        quotes = new HomeFeedPanel(session, "quotes");
        feeds = new Grid { RowDefinitions = new RowDefinitions("*,112,*"), RowSpacing = 14 };
        feeds.Children.Add(servers);
        Grid.SetRow(announcements, 1);
        feeds.Children.Add(announcements);
        Grid.SetRow(recommendations, 2);
        feeds.Children.Add(recommendations);
        accountCard = Ui.Card(account);
        accountCard.Padding = new Thickness(16, 15, 16, 12);
        accountCard.Height = 426;
        accountCard.Name = "HomeAccountCard";
        accountCard.Margin = default;
        var left = Ui.Stack(accountCard, launch);
        left.Spacing = 14;
        layout = new Grid { ColumnDefinitions = new ColumnDefinitions("320,*"), ColumnSpacing = 18, MinHeight = 560 };
        layout.Children.Add(left);
        Grid.SetColumn(right, 1);
        layout.Children.Add(right);
        Body.Children.Add(layout);
        Body.Children.Add(quotes);
        Body.Spacing = 14;
        SizeChanged += (_, _) => UpdateDimensions();
        AttachedToVisualTree += (_, _) => UpdateDimensions();
        Toggle(session.HomeInstancePanelOpen);
        DrawAccount();
    }
    void Toggle(bool value)
    {
        expanded = value;
        Session.HomeInstancePanelOpen = value;
        right.Content = expanded ? picker : feeds;
        quotes.IsVisible = !expanded;
        DrawLaunch();
    }
    void UpdateDimensions()
    {
        var compact = Bounds.Width < 1032;
        layout.ColumnDefinitions[0].Width = new GridLength(compact ? 280 : 320);
        layout.ColumnSpacing = compact ? 14 : 18;
        // The original dashboard grows with the viewport, while its account card stays compact.
        layout.Height = Math.Max(560, (TopLevel.GetTopLevel(this)?.ClientSize.Height ?? 780) - 220);
    }
    void DrawAccount()
    {
        foreach (var image in account.GetLogicalDescendants().OfType<Image>())
            (image.Source as IDisposable)?.Dispose();
        account.Children.Clear();
        var details = Ui.Stack();
        details.Spacing = 0;
        var tabs = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), Height = 29 };
        var column = 0;
        foreach (var (label, mode) in new[] { ("正版", "microsoft"), ("离线", "offline"), ("MUA 登录", "mua") })
        {
            var tab = Ui.Button(label, () => Send(new { type = "switchAccountMode", account = mode }));
            tab.Classes.Add("account-tab");
            tab.Height = 25;
            tab.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center;
            tab.Classes.Set("active", State.AccountType == mode);
            Grid.SetColumn(tab, column++);
            tabs.Children.Add(tab);
        }
        details.Children.Add(new Border { Child = tabs, Background = global::Avalonia.Media.Brush.Parse("#E8F0F7"), CornerRadius = new CornerRadius(7), Padding = new Thickness(2) });
        var avatar = Ui.Button("", () => AccountDialogs.Skin(Session, Owner));
        avatar.Content = Ui.Avatar(Session.Avatar, 94);
        avatar.Padding = new Thickness(3);
        avatar.Width = avatar.Height = 100;
        avatar.Margin = new Thickness(0, 13, 0, 0);
        avatar.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center;
        details.Children.Add(avatar);
        var name = Ui.Text(State.Player, 18);
        name.FontWeight = global::Avalonia.Media.FontWeight.Bold;
        name.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center;
        name.Margin = new Thickness(0, 10, 0, 0);
        details.Children.Add(name);
        var identity = Ui.Muted(State.AccountLabel, 10.5);
        identity.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center;
        identity.Margin = new Thickness(0, 3, 0, 0);
        details.Children.Add(identity);
        var change = Ui.Button(State.AccountType == "offline" ? "切换本地账号" : "切换账号", () => AccountDialogs.Open(Session, Owner, State.AccountType));
        change.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
        change.HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Center;
        change.Height = 36;
        change.Margin = new Thickness(0, 14, 0, 0);
        details.Children.Add(change);
        var settings = Ui.Action("账号设置", () => Session.Navigate?.Invoke("options"));
        settings.Classes.Add("link");
        settings.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center;
        settings.Margin = new Thickness(0, 7, 0, 0);
        details.Children.Add(settings);
        account.Children.Add(details);
        var footer = new Border
        {
            Name = "HomeAccountFooter",
            BorderBrush = global::Avalonia.Media.Brush.Parse("#E4ECF3"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 9, 0, 0),
            Child = Ui.Beside(Ui.Muted("账号保存在本机", 9.5), Ui.Muted("已保存", 9.5))
        };
        Grid.SetRow(footer, 2);
        account.Children.Add(footer);
    }
    void DrawLaunch()
    {
        launch.Children.Clear();
        var button = Ui.Button("启动游戏", () => Send(new { type = "launch", version = State.SelectedVersion }), true);
        var title = Ui.Text("启动游戏", 17);
        title.FontWeight = global::Avalonia.Media.FontWeight.Bold;
        var detail = Ui.Text((State.SelectedVersion.Length > 0 ? State.SelectedVersion : "未选择实例") + " · " + (State.AutoMemory ? "自动内存" : State.Ram + " MB"), 9.5);
        detail.TextWrapping = global::Avalonia.Media.TextWrapping.NoWrap;
        detail.TextTrimming = global::Avalonia.Media.TextTrimming.CharacterEllipsis;
        detail.MaxWidth = 160;
        var text = Ui.Stack(title, detail);
        text.Spacing = 4;
        var content = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            Spacing = 12,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };
        content.Children.Add(new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(15),
            Background = global::Avalonia.Media.Brush.Parse("#30FFFFFF"),
            Child = new TextBlock
            {
                Text = "▶",
                Foreground = global::Avalonia.Media.Brushes.White,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
            }
        });
        content.Children.Add(text);
        button.Content = content;
        button.HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Center;
        button.IsEnabled = State.SelectedVersion.Length > 0;
        button.Height = 78;
        button.Name = "HomeLaunchButton";
        button.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,40"), ColumnSpacing = 8 };
        row.Children.Add(button);
        var actions = Ui.Stack(Ui.Action(expanded ? "‹" : "›", () => Toggle(!expanded)), Ui.Action("⚙", () => Session.Navigate?.Invoke("manage")));
        actions.Spacing = 6;
        foreach (var action in actions.Children.OfType<Button>())
        {
            action.Height = 36;
            action.Width = 38;
            action.Padding = default;
            action.HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Center;
        }
        Grid.SetColumn(actions, 1);
        row.Children.Add(actions);
        launch.Children.Add(row);
        var games = Ui.Action("运行中的游戏（" + Session.Games.Length + "）", () => Session.Navigate?.Invoke("games"));
        games.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
        games.HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Center;
        games.Height = 32;
        launch.Children.Add(games);
    }
    protected override void Receive(NativeMessage message)
    {
        if (message.Type is "state" or "accountState" or "avatar")
            DrawAccount();
        if (message.Type is "state" or "games")
            DrawLaunch();
    }
    public override void Dispose()
    {
        picker.Dispose();
        servers.Dispose();
        announcements.Dispose();
        recommendations.Dispose();
        quotes.Dispose();
        base.Dispose();
    }
}
