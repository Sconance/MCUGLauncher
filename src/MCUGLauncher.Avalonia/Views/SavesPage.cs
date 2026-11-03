using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;

namespace MCUGLauncher.Avalonia.Views;
internal sealed class SavesPage : NativePage
{
    readonly StackPanel list = Ui.Stack();
    public SavesPage(ILauncherService session, UiContext ui) : base(session, ui)
    {
        session.SaveBackupsChanged += OnBackups;
        UnsubscribeWith(() => session.SaveBackupsChanged -= OnBackups);
        Title("存档保护", "当前实例：" + State.SelectedInstance + "。恢复会创建新存档，不覆盖原存档。");
        var retention = Ui.Input(State.Preferences.SaveBackupCount.ToString());
        Body.Children.Add(Ui.SettingsCard("备份保留", Ui.Setting("每个实例保留的备份数", "1–50 份，创建备份时按此数量保留历史记录", retention), Ui.Button("保存备份设置", async () =>
        {
            if (!int.TryParse(retention.Text, out var count))
                throw new ArgumentException("备份数量必须是整数。");
            await Session.SaveSettingsAsync(new AppSettingsPatch { BackupCount = count });
        })));
        Body.Children.Add(Ui.Row(
                Ui.Button("备份当前实例", () => Session.BackupSavesAsync(), true),
                Ui.Button("打开存档文件夹", () => Session.OpenFolderAsync(kind: "saves")),
                Ui.Button("刷新备份", () => Session.ListSaveBackupsAsync())
        ));
        Body.Children.Add(list);
        _ = Session.ListSaveBackupsAsync();
    }

    void OnBackups(SaveBackupInfo[] backups)
    {
        list.Children.Clear();
        foreach (var backup in backups)
        {
            var panel = Ui.Stack(Ui.Text(backup.Created, 16));
            foreach (var world in backup.Worlds)
                panel.Children.Add(Ui.Button("恢复 " + world, () => Session.RestoreSaveAsync(id: backup.Id, world: world)));
            list.Children.Add(Ui.Card(panel));
        }

        if (list.Children.Count == 0)
            list.Children.Add(Ui.Text("还没有备份。"));
    }
}

internal sealed class BookmarksPage : NativePage
{
    public BookmarksPage(ILauncherService session, UiContext ui) : base(session, ui)
    {
        Title("我的收藏");
        Draw();
    }

    void Draw()
    {
        while (Body.Children.Count > 1)
            Body.Children.RemoveAt(1);
        foreach (var item in State.Favorites)
            Body.Children.Add(Ui.Card(Ui.Stack(
                        Ui.Text(item.Title, 17),
                        Ui.Text(item.Description),
                        Ui.Row(
                            Ui.Button("资源详情", () => ResourceDialog.Open(Session, UI, Owner, item, item.Kind)),
                            Ui.Button("取消收藏", () => Session.ToggleBookmarkAsync(id: item.Id, kind: item.Kind))
                        )
            )));
        if (State.Favorites.Length == 0)
            Body.Children.Add(Ui.Text("还没有收藏的资源。"));
    }

    protected override void OnStateChanged()
    {
        Draw();
    }
}
