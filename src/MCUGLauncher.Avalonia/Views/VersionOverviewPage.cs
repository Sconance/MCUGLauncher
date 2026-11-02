using Avalonia.Controls;

namespace MCUGLauncher.Avalonia.Views;

internal sealed class VersionOverviewPage : NativePage
{
    public VersionOverviewPage(ILauncherService session, UiContext ui) : base(session, ui)
    {
        var item = State.Instances.FirstOrDefault(i => i.Name == State.SelectedInstance);
        Title("版本设置 · " + (item?.Title ?? "未选择实例"));
        Body.Children.Add(Ui.Row(Ui.Action("返回", () => UI.Navigate?.Invoke("home")), Ui.Action("版本内容管理", () => UI.Navigate?.Invoke("manage"))));
        if (item == null)
        {
            Body.Children.Add(Ui.Text("请先选择实例。"));
            return;
        }
        Body.Children.Add(Ui.Card(Ui.Stack(Ui.Text(item.Title, 20), Ui.Text(item.Minecraft + " · " + item.Loader), Ui.Button("启动设置", () => LaunchOptionsPage.Open(Session, UI, Owner, item.Name)), Ui.Button("打开版本文件夹", () => Session.OpenFolderAsync(kind: "version")), Ui.Button("启动此版本", () => Session.LaunchAsync(version: item.Name), true))));
    }
}
