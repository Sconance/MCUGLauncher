using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;

namespace MCUGLauncher.Avalonia.Views;
internal sealed class InstancesPage : NativePage
{
    readonly ComboBox instances;
    bool updating;
    readonly StackPanel mods = Ui.Stack();
    readonly TextBox cloneName = Ui.Input("", "新实例名称");
    public InstancesPage(ILauncherService session, UiContext ui, bool contentManagement = false) : base(session, ui)
    {
        var heading = Ui.Stack(Ui.Heading(contentManagement ? "版本内容管理" : "实例维护"), Ui.Muted("选择实例后管理启动、模组、加载器与本地文件。"));
        Body.Children.Add(contentManagement ? Ui.Beside(heading, Ui.Action("返回", () => UI.Navigate?.Invoke("home"))) : heading);
        instances = Ui.Select(State.Instances);
        instances.SelectedItem = State.Instances.FirstOrDefault(i => i.Name == State.SelectedInstance);
        instances.SelectionChanged += async (_, _) =>
        {
            if (!updating && instances.SelectedItem is InstanceInfo i && i.Name != State.SelectedInstance)
                await Session.SelectInstanceAsync(instance: i.Name);
        };
        Body.Children.Add(Ui.Card(Ui.Stack(
            Ui.Text("当前实例", 14), instances,
            Ui.Row(Ui.Button("启动", () => Session.LaunchAsync(version: Selected), true),
                Ui.Action("版本设置", () => UI.Navigate?.Invoke("version")),
                Ui.Button("实例启动参数", () => LaunchOptionsPage.Open(Session, UI, Owner, Selected)),
                Ui.Button("安装模组加载器", InstallLoader))
        )));
        Body.Children.Add(Ui.Card(Ui.Stack(
            Ui.Text("内容管理", 14),
            Ui.Row(Ui.Action("下载模组", () => UI.Navigate?.Invoke("mod")),
                Ui.Button("导入本地文件", () => LocalImportDialog.Pick(Session, UI, Owner)),
                Ui.Button("检查内容更新", () => MaintenanceDialogs.Updates(Session, UI, Owner, Selected)),
                Ui.Action("存档保护", () => UI.Navigate?.Invoke("saves"))),
            Ui.Text("本地文件", 14),
            Ui.Row(Folder("实例文件夹", "version"), Folder("Mod 文件夹", "mods"),
                Folder("存档文件夹", "saves"), Folder("截图", "screenshots"))
        )));
        if (!contentManagement)
        {
            var saves = Ui.Check("包含存档");
            var servers = Ui.Check("包含服务器列表");
            Body.Children.Add(Ui.Card(Ui.Stack(
                        Ui.Heading("维护"),
                        cloneName,
                        Ui.Row(
                            saves,
                            servers
                        ),
                        Ui.Row(
                            Ui.Button("克隆实例", () => Session.CloneInstanceAsync(version: Selected, name: cloneName.Text ?? "", saves: saves.IsChecked == true, servers: servers.IsChecked == true)),
                            Ui.Button("导出 ZIP", async () =>
                                {
                                    var path = await FilePicker.Save(Owner);
                                    if (path != null)
                                        await Session.ExportInstanceAsync(version: Selected, path: path, saves: saves.IsChecked == true, servers: servers.IsChecked == true);
                                }),
                            Ui.Button("查看卸载范围", () => MaintenanceDialogs.Uninstall(Session, UI, Owner, Selected))
                        )
            )));
        }
        Body.Children.Add(Ui.Heading("本地模组"));
        Body.Children.Add(mods);
        DrawMods();
    }

    string Selected => (instances.SelectedItem as InstanceInfo)?.Name ?? State.SelectedInstance;

    Task InstallLoader()
    {
        var instance = instances.SelectedItem as InstanceInfo;
        if (instance == null || instance.Minecraft.Length == 0)
            throw new ArgumentException("请先选择已读取游戏版本的实例。");
        return InstallDialogs.Loader(Session, UI, Owner, instance.Minecraft);
    }

    Button Folder(string label, string kind) => Ui.Button(label, () => Session.OpenFolderAsync(kind: kind));
    void DrawMods()
    {
        mods.Children.Clear();
        foreach (var mod in State.Mods)
        {
            mods.Children.Add(Ui.Card(Ui.Stack(
                        Ui.Text(mod.Name, 16),
                        Ui.Text(mod.File + " · " + Ui.FileSize(mod.Size) + " · " + (mod.Version.Length > 0 ? mod.Version : "—")),
                        Ui.Text(mod.Enabled ? "已启用" : "已禁用"),
                        Ui.Row(
                            Ui.Button(mod.Enabled ? "禁用" : "启用", () => Session.ToggleModAsync(file: mod.File, enabled: !mod.Enabled)),
                            Ui.Button("删除", async () =>
                                {
                                    if (await Ui.Confirm(Owner, "删除模组", "将删除模组文件「" + mod.File + "」。该操作不能撤销。", "删除模组", true))
                                        await Session.DeleteModAsync(file: mod.File);
                                })
                        )
            )));
        }

        if (mods.Children.Count == 0)
            mods.Children.Add(Ui.Text("此实例没有本地模组。"));
    }

    protected override void OnStateChanged()
    {
        {
            updating = true;
            instances.ItemsSource = State.Instances;
            instances.SelectedItem = State.Instances.FirstOrDefault(i => i.Name == State.SelectedInstance);
            updating = false;
            DrawMods();
        }
    }
}
