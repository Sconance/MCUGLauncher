using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using MCUGLauncher.Avalonia.Presentation;
namespace MCUGLauncher.Avalonia.Views;

internal sealed class HomePage : NativePage
{
    readonly StackPanel account = Ui.Stack();
    readonly StackPanel launch = Ui.Stack();
    readonly ContentControl right = new();
    readonly InstancePickerView picker;
    readonly HomeFeedPanel servers;
    readonly HomeFeedPanel announcements;
    readonly HomeFeedPanel recommendations;
    readonly HomeFeedPanel quotes;
    readonly StackPanel feeds;
    bool expanded;
    public HomePage(LauncherFacade session) : base(session)
    {
        picker = new InstancePickerView(session, () => Toggle(false));
        servers = new HomeFeedPanel(session, "servers");
        announcements = new HomeFeedPanel(session, "announcements");
        recommendations = new HomeFeedPanel(session, "recommendations");
        quotes = new HomeFeedPanel(session, "quotes");
        feeds = Ui.Stack(servers, announcements, recommendations);
        var left = Ui.Stack(Ui.Card(account), launch);
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("300,*"), ColumnSpacing = 18 };
        layout.Children.Add(left);
        Grid.SetColumn(right, 1);
        layout.Children.Add(right);
        Body.Children.Add(layout);
        Body.Children.Add(quotes);
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
    void DrawAccount()
    {
        foreach (var image in account.GetLogicalDescendants().OfType<Image>())
            (image.Source as IDisposable)?.Dispose();
        account.Children.Clear();
        var tabs = Ui.Row();
        foreach (var (label, mode) in new[] { ("正版", "microsoft"), ("离线", "offline"), ("MUA 登录", "mua") })
            tabs.Children.Add(Ui.Button(label, () => Send(new { type = "switchAccountMode", account = mode }), State.AccountType == mode));
        account.Children.Add(tabs);
        var avatar = Ui.Button("", () => AccountDialogs.Skin(Session, Owner));
        avatar.Content = Ui.Avatar(Session.Avatar, 90);
        avatar.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center;
        account.Children.Add(avatar);
        var name = Ui.Heading(State.Player);
        name.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center;
        account.Children.Add(name);
        var identity = Ui.Text(State.AccountLabel);
        identity.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center;
        account.Children.Add(identity);
        account.Children.Add(Ui.Button(State.AccountType == "offline" ? "切换本地账号" : "切换账号", () => AccountDialogs.Open(Session, Owner, State.AccountType)));
        account.Children.Add(Ui.Action("账号设置", () => Session.Navigate?.Invoke("options")));
        account.Children.Add(Ui.Text("账号保存在本机"));
    }
    void DrawLaunch()
    {
        launch.Children.Clear();
        var button = Ui.Button("启动游戏", () => Send(new { type = "launch", version = State.SelectedVersion }), true);
        button.Content = Ui.Stack(Ui.Text("▷ 启动游戏", 22), Ui.Text((State.SelectedVersion.Length > 0 ? State.SelectedVersion : "未选择实例") + " · " + (State.AutoMemory ? "自动内存" : State.Ram + " MB")));
        button.IsEnabled = State.SelectedVersion.Length > 0;
        button.MinHeight = 84;
        button.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,40"), ColumnSpacing = 8 };
        row.Children.Add(button);
        var actions = Ui.Stack(Ui.Action(expanded ? "‹" : "›", () => Toggle(!expanded)), Ui.Action("⚙", () => Session.Navigate?.Invoke("manage")));
        Grid.SetColumn(actions, 1);
        row.Children.Add(actions);
        launch.Children.Add(row);
        launch.Children.Add(Ui.Action("运行中的游戏（" + Session.Games.Length + "）", () => Session.Navigate?.Invoke("games")));
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
