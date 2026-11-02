using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;

namespace MCUGLauncher.Avalonia.Views;
internal sealed class ResourceDialog : NativePage
{
    readonly NativeItem project;
    readonly string category;
    readonly ComboBox instances;
    readonly TextBox game = Ui.Input("", "游戏版本（可留空）");
    readonly ComboBox loader = Ui.Select(new[] { "", "vanilla", "fabric", "forge", "neoforge", "quilt" });
    readonly ComboBox versions = Ui.Select(Array.Empty<NativeItem>());
    readonly ComboBox mode = Ui.Select(new[] { "安装到实例", "下载到自定义目录" });
    readonly TextBox destination = Ui.Input("", "下载目录");
    readonly TextBlock status = Ui.Text("选择目标并读取兼容版本后，再确认下载。");
    readonly Button confirm;
    readonly Control manualConditions;
    string token = "";
    int revision;
    public ResourceDialog(LauncherFacade session, NativeItem project, string category) : base(session)
    {
        this.project = project;
        this.category = category;
        var choices = new[]
        {
            new NativeItem
            {
                Name = "不选择实例",
                Id = ""
            }
        }.Concat(State.Instances.Select(i => i with { Id = i.Name })).ToArray();
        instances = Ui.Select(choices);
        instances.SelectedItem = choices.FirstOrDefault(i => i.Id == State.SelectedInstance) ?? choices[0];
        var description = Ui.Stack(Ui.Heading(project.Title), Ui.Muted("作者：" + project.Author));
        var hero = new Grid { ColumnDefinitions = new ColumnDefinitions("80,*"), ColumnSpacing = 16 };
        hero.Children.Add(new RemoteImage(Session, project.Icon, 80));
        Grid.SetColumn(description, 1);
        hero.Children.Add(description);
        var details = Ui.Stack(hero, Ui.Text(project.Description), Ui.Action("在浏览器打开项目", () => Process.Start(new ProcessStartInfo("https://modrinth.com/project/" + Uri.EscapeDataString(project.Id)) { UseShellExecute = true })));
        details.Name = "ResourceDescription";
        confirm = Ui.Button("确认下载", async () =>
            {
                if (versions.SelectedItem is not NativeItem version || token.Length == 0)
                    throw new ArgumentException("请读取并选择资源版本。");
                await Send(new
                {
                    type = "queueContentVersion",
                    token,
                    versionId = version.Id,
                    mode = mode.SelectedIndex == 1 ? "local" : "instance",
                    path = destination.Text ?? ""
                });
            }, true);
        var directory = Ui.Field("自定义下载目录", Ui.Beside(destination, Ui.Button("选择目录", async () => destination.Text = await FilePicker.Folder(Owner) ?? destination.Text)));
        destination.MinWidth = 0;
        manualConditions = Ui.Stack(Ui.Field("游戏版本", game), Ui.Field("加载器", loader));
        var options = Ui.Stack(Ui.Text("下载选项", 16),
                    Ui.Field("目标实例", instances),
                    manualConditions,
                    Ui.Button("读取兼容资源版本", Load),
                    Ui.Field("资源版本", versions),
                    Ui.Field("安装方式", mode),
                    directory,
                    status
        );
        options.Name = "ResourceInstallOptions";
        DialogActions = Ui.DialogActions(this, confirm);
        confirm.HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Center;
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("*,310"), ColumnSpacing = 22 };
        layout.Children.Add(details);
        var card = Ui.Card(options);
        Grid.SetColumn(card, 1);
        layout.Children.Add(card);
        layout.SizeChanged += (_, _) =>
        {
            var narrow = layout.Bounds.Width < 680;
            layout.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "*,310");
            layout.RowDefinitions = new RowDefinitions(narrow ? "Auto,Auto" : "Auto");
            layout.RowSpacing = narrow ? 18 : 0;
            Grid.SetColumn(card, narrow ? 0 : 1);
            Grid.SetRow(card, narrow ? 1 : 0);
        };
        Body.Children.Add(layout);
        void UpdateMode() => directory.IsVisible = mode.SelectedIndex == 1;
        mode.SelectionChanged += (_, _) => UpdateMode();
        UpdateMode();
        if (category is "modpack" or "datapack")
        {
            mode.SelectedIndex = 1;
            mode.IsEnabled = false;
        }

        instances.SelectionChanged += (_, _) => Invalidate();
        game.TextChanged += (_, _) => Invalidate();
        loader.SelectionChanged += (_, _) => Invalidate();
        Invalidate();
    }

    void Invalidate()
    {
        revision++;
        token = "";
        confirm.IsEnabled = false;
        var selected = instances.SelectedItem as NativeItem;
        manualConditions.IsVisible = selected?.Id.Length == 0;
        game.IsEnabled = loader.IsEnabled = selected?.Id.Length == 0;
    }

    async Task Load()
    {
        Invalidate();
        var generation = revision;
        status.Text = "正在读取版本…";
        var request = Guid.NewGuid().ToString("N");
        var result = await Session.RequestAsync("resourceVersions", new
        {
            type = "listContentVersions",
            id = project.Id,
            category,
            instance = (instances.SelectedItem as NativeItem)?.Id ?? "",
            game = game.Text ?? "",
            loader = loader.SelectedItem as string ?? "",
            requestId = request
        }, m => m.RequestId == request);
        if (generation != revision)
            return;
        versions.ItemsSource = result.Items;
        versions.SelectedIndex = result.Items.Length > 0 ? 0 : -1;
        token = result.Token;
        confirm.IsEnabled = result.Items.Length > 0;
        status.Text = result.Items.Length > 0 ? $"找到 {result.Items.Length} 个版本 · {result.Game} / {result.Loader}" : "没有兼容版本，请更换实例或条件。";
    }

    protected override void Receive(NativeMessage message)
    {
        if (message.Type == "resourceQueued" && message.Token == token)
        {
            status.Text = "已加入下载任务。";
            confirm.IsEnabled = false;
            token = "";
        }
    }

    public static Task Open(LauncherFacade session, Window owner, NativeItem project, string category) => Ui.Show(owner, "资源详情与安装选项", new ResourceDialog(session, project, category), 1000, 740);
}
