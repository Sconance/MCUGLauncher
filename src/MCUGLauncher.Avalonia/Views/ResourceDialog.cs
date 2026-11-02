using System.Diagnostics;
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
        Title(project.Title, project.Description);
        Body.Children.Add(new RemoteImage(Session, project.Icon, 80));
        Body.Children.Add(Ui.Text("作者：" + project.Author));
        Body.Children.Add(Ui.Action("在浏览器打开项目", () => Process.Start(new ProcessStartInfo("https://modrinth.com/project/" + Uri.EscapeDataString(project.Id)) { UseShellExecute = true })));
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
        Body.Children.Add(Ui.Card(Ui.Stack(
                    Ui.Field("兼容性依据实例", instances),
                    Ui.Field("游戏版本", game),
                    Ui.Field("加载器", loader),
                    Ui.Button("读取兼容资源版本", Load),
                    Ui.Field("资源版本", versions),
                    Ui.Field("安装方式", mode),
                    Ui.Field("自定义下载目录", destination),
                    Ui.Button("选择目录", async () => destination.Text = await FilePicker.Folder(Owner) ?? destination.Text),
                    status,
                    confirm
        )));
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

    public static Task Open(LauncherFacade session, Window owner, NativeItem project, string category) => Ui.Show(owner, "资源详情与安装选项", new ResourceDialog(session, project, category));
}
