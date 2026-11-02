using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using MCUGLauncher.Avalonia.Presentation;

namespace MCUGLauncher.Avalonia.Views;
internal sealed class HomePage : NativePage
{
    readonly StackPanel account = Ui.Stack();
    readonly StackPanel instances = Ui.Stack();
    readonly StackPanel community = Ui.Stack();
    readonly TextBox search = Ui.Input("", "搜索实例名称、版本或加载器");
    bool settingRoots;
    readonly ComboBox roots = Ui.Select(Array.Empty<string>());
    public HomePage(LauncherFacade session) : base(session)
    {
        Title("启动", "选择实例后启动，任务与正在运行的游戏独立管理。");
        roots.ItemsSource = State.GameRoots;
        roots.SelectedItem = State.Root;
        roots.SelectionChanged += async (_, _) =>
        {
            if (!settingRoots && roots.SelectedItem is string path && path != State.Root)
                await Send(new
                {
                    type = "saveSettings",
                    root = path
                });
        };
        var right = Ui.Stack();
        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("280,*"),
            ColumnSpacing = 18
        };
        var left = Ui.Card(account);
        layout.Children.Add(left);
        Grid.SetColumn(right, 1);
        layout.Children.Add(right);
        Body.Children.Add(layout);
        right.Children.Add(Ui.Card(Ui.Stack(
                    Ui.Row(
                        roots,
                        Ui.Button("添加游戏目录", async () =>
                            {
                                var path = await FilePicker.Folder(Owner);
                                if (path != null)
                                    await Send(new
                                    {
                                        type = "saveSettings",
                                        root = path
                                    });
                            }),
                        Ui.Button("导入整合包", () => InstallDialogs.Import(Session, Owner)),
                        Ui.Button("刷新实例", () => Send(new { type = "requestState" }))
                    ),
                    search,
                    instances
        )));
        right.Children.Add(Ui.Card(community));
        search.TextChanged += (_, _) => DrawInstances();
        Draw();
    }

    void Draw()
    {
        settingRoots = true;
        roots.ItemsSource = State.GameRoots.Length > 0 ? State.GameRoots : new[]
        {
            State.Root
        };
        roots.SelectedItem = State.Root;
        settingRoots = false;
        account.Children.Clear();
        account.Children.Add(Ui.Row(
                Ui.Avatar(Session.Avatar, 72),
                Ui.Stack(
                    Ui.Heading(State.Player),
                    Ui.Text(State.AccountLabel),
                    Ui.Action("账号与皮肤", () => Session.Navigate?.Invoke("accounts"))
                )
        ));
        account.Children.Add(Ui.Row(
                LaunchButton(),
                Ui.Action("运行中的游戏（" + Session.Games.Length + "）", () => Session.Navigate?.Invoke("games"))
        ));
        DrawInstances();
        community.Children.Clear();
        community.Children.Add(Ui.Heading("社区动态"));
        foreach (var entry in Session.Community.GetValueOrDefault("announcements")?.Take(3) ?? [])
            community.Children.Add(Ui.Stack(
                    Ui.Text(entry.Title, 16),
                    Ui.Text(entry.Body.Length > 0 ? entry.Body : entry.Text),
                    Ui.Text(entry.PublishedAt)
            ));
        if (community.Children.Count == 1)
            community.Children.Add(Ui.Text("暂无公告，联网内容加载后会显示在这里。"));
        community.Children.Add(Ui.Action("公告、推荐与服务器", () => Session.Navigate?.Invoke("community")));
    }

    Button LaunchButton()
    {
        var button = Ui.Button("启动游戏", () => Send(new { type = "launch", version = State.SelectedVersion }), true);
        button.IsEnabled = State.SelectedVersion.Length > 0;
        return button;
    }
    void DrawInstances()
    {
        instances.Children.Clear();
        var query = search.Text ?? "";
        foreach (var group in State.Instances.Where(i => (i.Title + " " + i.Name + " " + i.Minecraft + " " + i.Loader).Contains(query, StringComparison.OrdinalIgnoreCase)).GroupBy(i => i.Loader))
        {
            var rows = Ui.Stack();
            foreach (var item in group)
            {
                var row = Ui.Row(
                    Ui.Button(item.Title + "\n" + item.Minecraft + " · " + item.Loader, async () =>
                        {
                            await Send(new
                            {
                                type = "selectVersion",
                                version = item.Name
                            });
                            await Send(new
                            {
                                type = "selectInstance",
                                instance = item.Name
                            });
                        }),
                    Ui.Button("启动", () => Send(new { type = "launch", version = item.Name }), true),
                    Ui.Action("管理", () => Session.Navigate?.Invoke("instances"))
                );
                rows.Children.Add(row);
            }

            instances.Children.Add(new Expander { Header = (group.Key.Length > 0 ? group.Key : "原版游戏") + "（" + group.Count() + "）", IsExpanded = true, HorizontalAlignment = HorizontalAlignment.Stretch, Content = rows });
        }

        if (instances.Children.Count == 0)
            instances.Children.Add(Ui.Text("当前目录没有匹配的实例，请添加目录或前往下载页安装。"));
    }

    protected override void Receive(NativeMessage message)
    {
        if (message.Type is "state" or "accountState" or "avatar" or "community" or "games")
            Draw();
    }
}
