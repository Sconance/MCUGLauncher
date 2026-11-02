using Avalonia.Controls;

namespace MCUGLauncher.Avalonia.Views;
internal static class MaintenanceDialogs
{
    public static async Task Uninstall(LauncherFacade session, Window owner, string version)
    {
        var result = await session.RequestAsync("uninstallPreview", new
        {
            type = "previewUninstall",
            version
        });
        var content = Ui.Stack(
            Ui.Heading("卸载范围"),
            Ui.Text(result.Notice)
        );
        foreach (var path in result.Paths)
            content.Children.Add(Ui.Text(path));
        content.Children.Add(Ui.Button("确认执行卸载计划", async () =>
                {
                    await session.SendAsync(new
                    {
                        type = "applyUninstall",
                        token = result.Token
                    });
                    (TopLevel.GetTopLevel(content) as Window)?.Close();
                }));
        await Ui.Show(owner, "卸载预览", content);
    }

    public static async Task Updates(LauncherFacade session, Window owner, string version)
    {
        var result = await session.RequestAsync("contentUpdates", new
        {
            type = "checkContentUpdates",
            version
        });
        var content = Ui.Stack(Ui.Heading("可更新内容"));
        var selected = new List<(string Id, CheckBox Box)>();
        foreach (var item in result.Items)
        {
            var box = Ui.Check(item.Name + " · " + item.Before + " → " + item.After);
            box.IsEnabled = item.Available;
            content.Children.Add(box);
            if (item.Error.Length > 0)
                content.Children.Add(Ui.Text(item.Error));
            selected.Add((item.Id, box));
        }

        content.Children.Add(Ui.Button("预览所选更新", async () =>
                {
                    var ids = selected.Where(i => i.Box.IsChecked == true).Select(i => i.Id).ToArray();
                    var preview = await session.RequestAsync("contentUpdatePreview", new
                    {
                        type = "previewContentUpdates",
                        token = result.Token,
                        projects = ids
                    });
                    var plan = Ui.Stack(Ui.Heading("确认更新文件"));
                    foreach (var change in preview.Changes)
                        plan.Children.Add(Ui.Text(change.Path + " · " + change.Before + " → " + change.After));
                    plan.Children.Add(Ui.Button("应用更新", () => session.SendAsync(new { type = "applyContentUpdates", token = preview.Token }), true));
                    await Ui.Show((Window)TopLevel.GetTopLevel(content)!, "内容更新预览", plan);
                }));
        await Ui.Show(owner, "内容更新", content);
    }
}
