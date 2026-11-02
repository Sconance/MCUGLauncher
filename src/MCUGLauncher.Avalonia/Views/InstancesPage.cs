using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;

namespace MCUGLauncher.Avalonia.Views;
internal sealed class InstancesPage : NativePage
{
    readonly ComboBox instances;
    bool updating;
    readonly StackPanel mods = Ui.Stack();
    readonly TextBox cloneName = Ui.Input("", "新实例名称");
    public InstancesPage(LauncherFacade session) : base(session)
    {
        Title("实例与内容管理", "设置、模组、克隆、导出和卸载都复用原有后端流程。");
        instances = Ui.Select(State.Instances);
        instances.SelectedItem = State.Instances.FirstOrDefault(i => i.Name == State.SelectedInstance);
        instances.SelectionChanged += async (_, _) =>
        {
            if (!updating && instances.SelectedItem is NativeItem i && i.Name != State.SelectedInstance)
                await Send(new
                {
                    type = "selectInstance",
                    instance = i.Name
                });
        };
        Body.Children.Add(Ui.Card(Ui.Stack(
                    instances,
                    Ui.Row(
                        Ui.Button("实例启动参数", () => LaunchOptionsPage.Open(Session, Owner, Selected)),
                        Ui.Button("启动", () => Send(new { type = "launch", version = Selected }), true),
                        Folder("实例文件夹", "version"),
                        Folder("Mod 文件夹", "mods"),
                        Folder("存档文件夹", "saves"),
                        Folder("截图", "screenshots")
                    ),
                    Ui.Row(
                        Ui.Button("检查内容更新", () => MaintenanceDialogs.Updates(Session, Owner, Selected)),
                        Ui.Action("存档保护", () => Session.Navigate?.Invoke("saves"))
                    )
        )));
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
                        Ui.Button("克隆实例", () => Send(new { type = "cloneInstance", version = Selected, name = cloneName.Text ?? "", saves = saves.IsChecked == true, servers = servers.IsChecked == true })),
                        Ui.Button("导出 ZIP", async () =>
                            {
                                var path = await FilePicker.Save(Owner);
                                if (path != null)
                                    await Send(new
                                    {
                                        type = "exportInstance",
                                        version = Selected,
                                        path,
                                        saves = saves.IsChecked == true,
                                        servers = servers.IsChecked == true
                                    });
                            }),
                        Ui.Button("查看卸载范围", () => MaintenanceDialogs.Uninstall(Session, Owner, Selected))
                    )
        )));
        Body.Children.Add(Ui.Heading("本地模组"));
        Body.Children.Add(mods);
        DrawMods();
    }

    string Selected => (instances.SelectedItem as NativeItem)?.Name ?? State.SelectedInstance;

    Button Folder(string label, string kind) => Ui.Button(label, () => Send(new { type = "openFolder", kind }));
    void DrawMods()
    {
        mods.Children.Clear();
        foreach (var mod in State.Mods)
        {
            mods.Children.Add(Ui.Card(Ui.Stack(
                        Ui.Text(mod.Name, 16),
                        Ui.Text(mod.File + " · " + mod.V),
                        Ui.Row(
                            Ui.Button(mod.Enabled ? "禁用" : "启用", () => Send(new { type = "toggleMod", file = mod.File, enabled = !mod.Enabled })),
                            Ui.Button("删除", async () =>
                                {
                                    if (await Ui.Confirm(Owner, "删除模组", "删除模组 " + mod.File + "？"))
                                        await Send(new
                                        {
                                            type = "deleteMod",
                                            file = mod.File
                                        });
                                })
                        )
            )));
        }

        if (mods.Children.Count == 0)
            mods.Children.Add(Ui.Text("此实例没有本地模组。"));
    }

    protected override void Receive(NativeMessage message)
    {
        if (message.Type == "state")
        {
            updating = true;
            instances.ItemsSource = State.Instances;
            instances.SelectedItem = State.Instances.FirstOrDefault(i => i.Name == State.SelectedInstance);
            updating = false;
            DrawMods();
        }
    }
}
