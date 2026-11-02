using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;

namespace MCUGLauncher.Avalonia.Views;

internal sealed class DirectoriesPage : NativePage
{
    readonly TextBox path;
    readonly StackPanel directories = Ui.Stack();
    string previousRoot;
    public DirectoriesPage(ILauncherService session, UiContext ui) : base(session, ui)
    {
        Title("游戏目录", "切换目录只改变启动器的选择，不移动或删除游戏文件。");
        previousRoot = State.Root;
        path = Ui.Input(State.Root);
        Body.Children.Add(Ui.SettingsCard("添加或切换目录", path, Ui.Row(
            Ui.Button("选择文件夹", async () => path.Text = await FilePicker.Folder(Owner) ?? path.Text),
            Ui.Button("使用此目录", () => Session.SaveSettingsAsync(new AppSettingsPatch { Root = path.Text ?? "" }), true))));
        Body.Children.Add(directories);
        Draw();
    }
    void Draw()
    {
        directories.Children.Clear();
        foreach (var directory in State.GameRoots.Append(State.Root).Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var active = directory.Equals(State.Root, StringComparison.OrdinalIgnoreCase);
            var use = Ui.Button(active ? "正在使用" : "切换", () => Session.SaveSettingsAsync(new AppSettingsPatch { Root = directory }));
            use.IsEnabled = !active;
            var remove = Ui.Button("移除记录", async () =>
            {
                if (await Ui.Confirm(Owner, "移除目录记录", "只从列表移除记录，不删除目录中的任何文件。", "移除记录"))
                    await Session.SaveSettingsAsync(new AppSettingsPatch { GameRoots = State.GameRoots.Where(p => !p.Equals(directory, StringComparison.OrdinalIgnoreCase)).ToArray() });
            });
            remove.IsEnabled = !active;
            directories.Children.Add(Ui.Card(Ui.Stack(Ui.Text(directory), Ui.Row(use, remove))));
        }
    }
    protected override void OnStateChanged()
    {
        if (path.Text == previousRoot)
            path.Text = State.Root;
        previousRoot = State.Root;
        Draw();
    }
}
