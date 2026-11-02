using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;
namespace MCUGLauncher.Avalonia.Views;

// These dialogs retain the entry points and overlay semantics of the codex account card.
internal static class AccountDialogs
{
    public static Task Open(LauncherFacade session, Window owner, string mode) => mode switch
    {
        "offline" => Offline(session, owner),
        "mua" => Mua(session, owner),
        _ => Microsoft(session, owner)
    };
    static Task Microsoft(LauncherFacade session, Window owner)
    {
        return Ui.Show(owner, "正版账号登录", Ui.Stack(
            Ui.Heading("正版账号登录"),
            Ui.Text("使用 Microsoft 账号完成正版验证。启动器不保存密码，只保留授权会话。"),
            Ui.Button("打开登录窗口", () => session.SendAsync(new { type = "loginMicrosoft" }), true)), 480, 320);
    }
    static Task Mua(LauncherFacade session, Window owner)
    {
        var user = Ui.Input("", "MUA 账号");
        var password = Ui.Input("", "密码");
        password.PasswordChar = '●';
        var content = Ui.Stack(Ui.Heading("MUA 登录"), user, password);
        content.Children.Add(Ui.Button("登录 MUA", async () =>
        {
            var secret = password.Text ?? "";
            password.Text = "";
            await session.SendAsync(new
            {
                type = "loginMua",
                account = user.Text ?? "",
                password = secret
            });
        }, true));
        return Ui.Show(owner, "MUA 登录", content, 480, 430);
    }
    static Task Offline(LauncherFacade session, Window owner)
    {
        var name = Ui.Input(session.State.Player, "离线角色名");
        var content = Ui.Stack(Ui.Heading("切换本地账号"), Ui.Text("仅用于离线游戏，不影响正版角色。"), name);
        content.Children.Add(Ui.Button("保存离线角色名", async () =>
        {
            await session.SendAsync(new
            {
                type = "saveSettings",
                player = name.Text ?? ""
            });
        }, true));
        return Ui.Show(owner, "切换本地账号", content, 440, 300);
    }
    public static Task Skin(LauncherFacade session, Window owner)
    {
        var preview = Ui.Avatar(session.Avatar, 96);
        var content = Ui.Stack(Ui.Heading("皮肤与披风"), preview, Ui.Text(session.State.Player + " · " + session.State.AccountLabel));
        content.Children.Add(Ui.Button("重新读取皮肤", () => session.SendAsync(new { type = "readAvatar" })));
        if (session.State.AccountType == "offline")
        {
            content.Children.Add(Ui.Button("选择本地 PNG 皮肤", async () =>
            {
                var path = await FilePicker.Open((Window)TopLevel.GetTopLevel(content)!, "PNG 皮肤", "*.png");
                if (path != null)
                    await session.SendAsync(new
                    {
                        type = "setOfflineSkin",
                        path
                    });
            }, true));
            content.Children.Add(Ui.Button("恢复默认头像", () => session.SendAsync(new { type = "setOfflineSkin", path = "" })));
        }
        content.Children.Add(Ui.Text("本地皮肤仅用于启动器头像；正版/MUA 皮肤来自认证站。披风上传原后端尚未接入。"));
        void Update(NativeMessage message)
        {
            if (message.Type != "avatar")
                return;
            var previous = preview.Source as IDisposable;
            preview.Source = Ui.Bitmap(session.Avatar);
            previous?.Dispose();
        }
        return Show();
        async Task Show()
        {
            session.MessageReceived += Update;
            try
            {
                await Ui.Show(owner, "皮肤与披风", content, 580, 560);
            }
            finally { session.MessageReceived -= Update; (preview.Source as IDisposable)?.Dispose(); }
        }
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
