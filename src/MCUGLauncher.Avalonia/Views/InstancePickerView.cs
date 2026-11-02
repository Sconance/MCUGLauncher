using Avalonia;
using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;
namespace MCUGLauncher.Avalonia.Views;

// Right-hand instance panel; the dashboard remains the default home view.
internal sealed class InstancePickerView : NativePage
{
    readonly ComboBox roots = Ui.Select(Array.Empty<string>());
    readonly TextBox search = Ui.Input("", "点此处搜索实例");
    readonly StackPanel list = Ui.Stack();
    bool updating;
    public InstancePickerView(LauncherFacade session, Action close) : base(session)
    {
        Body.Children.Add(Ui.Row(Ui.Heading("游戏目录"), Ui.Button("导入整合包", () => InstallDialogs.Import(Session, Owner)), Ui.Button("刷新版本", () => Send(new { type = "requestState" })), Ui.Action("收起", close)));
        Body.Children.Add(Ui.Row(roots, Ui.Button("+", async () =>
        {
            var path = await FilePicker.Folder(Owner);
            if (path != null)
                await Send(new
                {
                    type = "saveSettings",
                    root = path
                });
        })));
        Body.Children.Add(search);
        Body.Children.Add(list);
        roots.SelectionChanged += async (_, _) =>
        {
            if (!updating && roots.SelectedItem is string root && root != State.Root)
                await Send(new
                {
                    type = "saveSettings",
                    root
                });
        };
        search.TextChanged += (_, _) => DrawList();
        Draw();
    }
    void Draw()
    {
        updating = true;
        roots.ItemsSource = State.GameRoots.Length > 0 ? State.GameRoots : new[] { State.Root };
        roots.SelectedItem = State.Root;
        updating = false;
        DrawList();
    }
    void DrawList()
    {
        list.Children.Clear();
        foreach (var group in State.Instances.Where(i => (i.Title + " " + i.Name + " " + i.Minecraft + " " + i.Loader).Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase)).GroupBy(i => i.Loader))
        {
            var rows = Ui.Stack();
            foreach (var item in group)
            {
                var selection = Ui.Button(item.Title + "\n" + item.Minecraft + " · " + item.Loader, () => Select(item));
                selection.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
                selection.HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Left;
                selection.Classes.Set("instance-selected", item.Name == State.SelectedVersion);
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,40,40"), ColumnSpacing = 5 };
                row.Children.Add(selection);
                var start = Ui.Button("▷", () => Send(new { type = "launch", version = item.Name }));
                Grid.SetColumn(start, 1);
                row.Children.Add(start);
                var settings = Ui.Button("⚙", async () => { await Select(item); Session.Navigate?.Invoke("version"); });
                Grid.SetColumn(settings, 2);
                row.Children.Add(settings);
                rows.Children.Add(row);
            }
            list.Children.Add(new Expander { Header = (group.Key is "" or "原版" ? "原版游戏" : group.Key + " 版本") + "（" + group.Count() + "）", IsExpanded = true, Content = rows, HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch });
        }
        if (list.Children.Count == 0)
            list.Children.Add(Ui.Text("当前目录没有匹配的实例。"));
    }
    async Task Select(NativeItem item)
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
    }
    protected override void Receive(NativeMessage message)
    {
        if (message.Type == "state")
            Draw();
    }
}
