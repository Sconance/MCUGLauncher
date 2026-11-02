using Avalonia.Controls;

namespace MCUGLauncher.Avalonia.Views;

internal sealed class CommunityOptionsPage : NativePage
{
    public CommunityOptionsPage(LauncherFacade session) : base(session)
    {
        Title("账号与社区设置", "本地账号与社区密钥；保存密钥不等于服务器身份认证。");
        Body.Children.Add(Ui.Card(Ui.Stack(Ui.Heading("当前账号"), Ui.Text(State.Player + " · " + State.AccountLabel), Ui.Action("返回主页切换账号", () => Session.Navigate?.Invoke("home")), Ui.Text("切换到离线不会删除在线授权。"), Forget("移除保存的正版账号", "microsoft"), Forget("移除保存的 MUA 账号", "mua"))));
        Body.Children.Add(Ui.Card(Ui.Stack(Ui.Heading("社区请求"), AnonymousSetting())));
        var key = Ui.Input("", "粘贴社区密钥");
        key.PasswordChar = '●';
        var status = Ui.Text(State.CommunityKey ? "已保存在本机" : "尚未保存");
        Body.Children.Add(Ui.Card(Ui.Stack(Ui.Heading("社区密钥"), status, key, Ui.Button("保存密钥", async () => { var result = await Session.RequestAsync("keyResult", new { type = "verifyKey", role = "community", key = key.Text ?? "" }); key.Text = ""; status.Text = result.Message; }))));
    }
    Control AnonymousSetting()
    {
        var toggle = Ui.Check("社区匿名模式", State.Anonymous);
        return Ui.Stack(toggle, Ui.Muted("只影响社区请求，不改变游戏账号身份。"), Ui.Button("保存社区偏好", () => Send(new { type = "saveSettings", anonymous = toggle.IsChecked == true })));
    }
    Button Forget(string label, string account) => Ui.Button(label, async () => { if (await Ui.Confirm(Owner, "移除账号", "仅移除本机登录缓存。下次使用此账号时需要重新登录。", "移除账号", true)) await Send(new { type = "forgetAccount", account }); });
}
