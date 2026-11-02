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
            Ui.Text(result.Notice)
        );
        foreach (var path in result.Paths)
            content.Children.Add(Ui.Text(path));
        var uninstall = Ui.Button("继续卸载", async () =>
                {
                    await session.SendAsync(new
                    {
                        type = "applyUninstall",
                        token = result.Token
                    });
                    (TopLevel.GetTopLevel(content) as Window)?.Close();
                });
        uninstall.Classes.Add("danger-primary");
        await Ui.Show(owner, "确认卸载范围", content, 680, 560, Ui.DialogActions(content, uninstall), "请检查文件列表。继续后还需确认删除。");
    }

    public static async Task Updates(LauncherFacade session, Window owner, string version)
    {
        var result = await session.RequestAsync("contentUpdates", new
        {
            type = "checkContentUpdates",
            version
        });
        var content = Ui.Stack(Ui.Muted("选择需要更新的内容，先查看文件变更，再确认应用。"));
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

        var review = Ui.Button("预览所选更新", async () =>
                {
                    var ids = selected.Where(i => i.Box.IsChecked == true).Select(i => i.Id).ToArray();
                    var preview = await session.RequestAsync("contentUpdatePreview", new
                    {
                        type = "previewContentUpdates",
                        token = result.Token,
                        projects = ids
                    });
                    var plan = Ui.Stack(Ui.Muted("以下文件将按预览结果更新。"));
                    foreach (var change in preview.Changes)
                        plan.Children.Add(Ui.Text(change.Path + " · " + change.Before + " → " + change.After));
                    var apply = Ui.Button("应用更新", () => session.SendAsync(new { type = "applyContentUpdates", token = preview.Token }), true);
                    await Ui.Show((Window)TopLevel.GetTopLevel(content)!, "确认文件更新", plan, actions: Ui.DialogActions(plan, apply));
                }, true);
        await Ui.Show(owner, "内容更新", content, actions: Ui.DialogActions(content, review));
    }
}
