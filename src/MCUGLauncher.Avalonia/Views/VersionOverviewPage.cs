using Avalonia.Controls;

namespace MCUGLauncher.Avalonia.Views;

internal sealed class VersionOverviewPage : NativePage
{
    public VersionOverviewPage(LauncherFacade session) : base(session)
    {
        var item = State.Instances.FirstOrDefault(i => i.Name == State.SelectedInstance);
        Title("版本设置 · " + (item?.Title ?? "未选择实例"));
        Body.Children.Add(Ui.Row(Ui.Action("返回", () => Session.Navigate?.Invoke("home")), Ui.Action("版本内容管理", () => Session.Navigate?.Invoke("manage"))));
        if (item == null)
        {
            Body.Children.Add(Ui.Text("请先选择实例。"));
            return;
        }
        Body.Children.Add(Ui.Card(Ui.Stack(Ui.Text(item.Title, 20), Ui.Text(item.Minecraft + " · " + item.Loader), Ui.Button("启动设置", () => LaunchOptionsPage.Open(Session, Owner, item.Name)), Ui.Button("打开版本文件夹", () => Send(new { type = "openFolder", kind = "version" })), Ui.Button("启动此版本", () => Send(new { type = "launch", version = item.Name }), true))));
    }
}
