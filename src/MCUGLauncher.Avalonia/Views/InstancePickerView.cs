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
    LauncherState? renderedState;
    public InstancePickerView(ILauncherService session, UiContext ui, Action close) : base(session, ui)
    {
        Body.Children.Add(Ui.Beside(Ui.Stack(Ui.Heading("游戏目录"), Ui.Muted("选择实例后可直接启动，或进入版本设置。")), Ui.Row(Ui.Button("导入整合包", () => InstallDialogs.Import(Session, UI, Owner)), Ui.Button("刷新版本", () => Session.RequestStateAsync()), Ui.Action("收起", close))));
        Body.Children.Add(Ui.Beside(roots, Ui.Button("+", async () =>
        {
            var path = await FilePicker.Folder(Owner);
            if (path != null)
                await Session.SaveSettingsAsync(new AppSettingsPatch { Root = path });
        })));
        Body.Children.Add(search);
        Body.Children.Add(list);
        Content = null;
        Content = Ui.Card(Body);
        ((Border)Content).Margin = default;
        roots.MinWidth = 0;
        roots.SelectionChanged += async (_, _) =>
        {
            if (!updating && roots.SelectedItem is string root && root != State.Root)
                await Session.SaveSettingsAsync(new AppSettingsPatch { Root = root });
        };
        search.TextChanged += (_, _) => DrawList();
        Draw();
    }
    void Draw()
    {
        renderedState = State;
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
                selection.Classes.Add("instance-row");
                selection.Classes.Set("instance-selected", item.Name == State.SelectedVersion);
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,40,40"), ColumnSpacing = 5 };
                row.Children.Add(selection);
                var start = Ui.Button("▷", () => Session.LaunchAsync(version: item.Name));
                Grid.SetColumn(start, 1);
                row.Children.Add(start);
                var settings = Ui.Button("⚙", async () => { await Select(item); UI.Navigate?.Invoke("version"); });
                Grid.SetColumn(settings, 2);
                row.Children.Add(settings);
                rows.Children.Add(row);
            }
            var label = (group.Key is "" or "原版" ? "原版游戏" : group.Key + " 版本") + "（" + group.Count() + "）";
            Button? header = null;
            header = Ui.Action("⌄  " + label, () =>
            {
                rows.IsVisible = !rows.IsVisible;
                header!.Content = (rows.IsVisible ? "⌄  " : "›  ") + label;
            });
            header.Classes.Add("nav");
            header.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
            var groupPanel = Ui.Stack(header, rows);
            groupPanel.Spacing = 8;
            var groupCard = Ui.Card(groupPanel);
            groupCard.Padding = new Thickness(8);
            list.Children.Add(groupCard);
        }
        if (list.Children.Count == 0)
            list.Children.Add(Ui.Text("当前目录没有匹配的实例。"));
    }
    async Task Select(InstanceInfo item)
    {
        await Session.SelectVersionAsync(version: item.Name);
        await Session.SelectInstanceAsync(instance: item.Name);
    }
    protected override void OnStateChanged()
    {
        if (renderedState is not { } previous || previous.Root != State.Root || previous.SelectedInstance != State.SelectedInstance ||
            !ReferenceEquals(previous.Instances, State.Instances) || !ReferenceEquals(previous.GameRoots, State.GameRoots))
            Draw();
    }
}
