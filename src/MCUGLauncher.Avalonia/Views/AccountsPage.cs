using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;

namespace MCUGLauncher.Avalonia.Views;
internal sealed class AccountsPage : NativePage
{
    readonly TextBlock identity = Ui.Text("");
    readonly Image avatar = Ui.Avatar("", 96);
    public AccountsPage(LauncherFacade session) : base(session)
    {
        Title("账号与皮肤", "正版与 MUA 使用缓存会话；本地皮肤只影响启动器头像，不修改游戏内皮肤。");
        Body.Children.Add(Ui.Card(Ui.Stack(
                    avatar,
                    identity,
                    Ui.Row(
                        Switch("正版", "microsoft"),
                        Switch("离线", "offline"),
                        Switch("MUA", "mua")
                    ),
                    Ui.Button("登录 Microsoft 账号", () => Send(new { type = "loginMicrosoft" }), true)
        )));
        var offline = Ui.Input(State.Player, "离线玩家名");
        Body.Children.Add(Ui.Card(Ui.Stack(
                    Ui.Heading("离线账号"),
                    offline,
                    Ui.Button("保存离线 ID", () => Send(new { type = "saveSettings", player = offline.Text ?? "" }))
        )));
        var user = Ui.Input("", "MUA 账号");
        var password = Ui.Input("", "密码");
        password.PasswordChar = '●';
        Body.Children.Add(Ui.Card(Ui.Stack(
                    Ui.Heading("MUA 登录"),
                    user,
                    password,
                    Ui.Button("登录 MUA", async () =>
                        {
                            var secret = password.Text ?? "";
                            password.Text = "";
                            await Send(new
                            {
                                type = "loginMua",
                                account = user.Text ?? "",
                                password = secret
                            });
                        })
        )));
        Body.Children.Add(Ui.Card(Ui.Stack(
                    Ui.Heading("头像"),
                    Ui.Row(
                        Ui.Button("读取账号皮肤", () => Send(new { type = "readAvatar" })),
                        Ui.Button("选择离线 PNG", async () =>
                            {
                                var path = await FilePicker.Open(Owner, "PNG 皮肤", "*.png");
                                if (path != null)
                                    await Send(new
                                    {
                                        type = "setOfflineSkin",
                                        path
                                    });
                            }),
                        Ui.Button("恢复史蒂夫", () => Send(new { type = "setOfflineSkin", path = "" }))
                    ),
                    Ui.Text("披风上传在当前后端尚未接入；在线皮肤由认证站提供。"),
                    Ui.Button("移除当前模式的已保存账号", async () =>
                        {
                            if (await Ui.Confirm(Owner, "移除账号", "移除本机保存的当前账号？"))
                                await Send(new
                                {
                                    type = "forgetAccount",
                                    account = State.AccountType
                                });
                        })
        )));
        Draw();
    }

    Button Switch(string label, string mode) => Ui.Button(label, () => Send(new { type = "switchAccountMode", account = mode }));
    void Draw()
    {
        identity.Text = State.Player + " · " + State.AccountLabel;
        var old = avatar.Source as IDisposable;
        avatar.Source = Ui.Bitmap(Session.Avatar);
        old?.Dispose();
    }

    protected override void Receive(NativeMessage message)
    {
        if (message.Type is "state" or "accountState" or "avatar")
            Draw();
    }

    public static async Task SelectProfile(LauncherFacade session, Window owner, NativeMessage message)
    {
        var list = Ui.Select(message.Items);
        var selected = false;
        var button = Ui.Button("使用选中角色", async () =>
            {
                if (list.SelectedItem is NativeItem item)
                {
                    selected = true;
                    await session.SendAsync(new
                    {
                        type = "selectMuaProfile",
                        token = message.Token,
                        profileId = item.Id
                    });
                    (TopLevel.GetTopLevel(list) as Window)?.Close();
                }
            }, true);
        await Ui.Show(owner, "选择 MUA 角色", Ui.Stack(
                Ui.Text("选择本次登录使用的角色，关闭窗口将取消登录。"),
                list,
                button
        ));
        if (!selected)
            await session.SendAsync(new
            {
                type = "selectMuaProfile",
                token = message.Token,
                profileId = ""
            });
    }
}
