using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MCUGLauncher.Avalonia.Views;

internal partial class GamesView : UserControl
{
    LauncherFacade? facade;

    public GamesView()
    {
        InitializeComponent();
        RefreshButton.Click += async (_, _) => await facade!.RequestGamesAsync();
    }

    public GamesView(LauncherFacade facade) : this()
    {
        Attach(facade);
    }

    public void Attach(LauncherFacade value)
    {
        facade = value;
        facade.GamesChanged += OnGames;
        facade.ToastReceived += info => Status(info.Message);
        facade.ErrorReceived += message => Status("操作失败：" + message);
        OnGames(facade.Games);
    }

    public void Activate() => _ = facade?.RequestGamesAsync();

    void OnGames(IReadOnlyList<GameInfo> items)
    {
        GamesPanel.Children.Clear();
        if (items.Count == 0)
        {
            GamesPanel.Children.Add(Muted("没有由本次启动器启动的游戏。"));
            return;
        }
        foreach (var game in items)
        {
            var copy = new StackPanel { Spacing = 2 };
            copy.Children.Add(new TextBlock { Text = game.Version, FontWeight = FontWeight.SemiBold });
            copy.Children.Add(new TextBlock
            {
                Text = $"玩家 {game.Player} · PID {game.Pid}",
                Classes = { "muted" }
            });
            var stop = new Button { Content = "关闭游戏", Classes = { "ghost" } };
            var id = game.Id;
            stop.Click += async (_, _) =>
            {
                Status("正在请求关闭 " + game.Version + "…");
                await facade!.StopGameAsync(id);
            };
            GamesPanel.Children.Add(new Border
            {
                Classes = { "card" },
                Padding = new Thickness(14),
                Child = Row(copy, stop)
            });
        }
    }

    void Status(string message) => StatusText.Text = message;

    static Control Row(Control copy, Control action)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        row.Children.Add(copy);
        Grid.SetColumn(action, 1);
        action.Margin = new Thickness(12, 0, 0, 0);
        action.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(action);
        return row;
    }

    static TextBlock Muted(string text) => new() { Text = text, Classes = { "muted" } };
}
