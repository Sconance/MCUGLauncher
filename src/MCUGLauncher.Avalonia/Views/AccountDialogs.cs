using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;
namespace MCUGLauncher.Avalonia.Views;

// These dialogs retain the entry points and overlay semantics of the codex account card.
internal static class AccountDialogs
{
    public static Task Open(ILauncherService session, UiContext ui, Window owner, string mode) => mode switch
    {
        "offline" => Offline(session, ui, owner),
        "mua" => Mua(session, ui, owner),
        _ => Microsoft(session, ui, owner)
    };
    static Task Microsoft(ILauncherService session, UiContext ui, Window owner)
    {
        var content = Ui.Stack(Ui.Text("登录 Microsoft 账号后，可使用已购买的 Minecraft Java 版。"),
            Ui.Card(Ui.Text("启动器不保存密码，只保留授权会话。切换到离线模式不会移除已保存的账号。")));
        var login = Ui.Button("打开登录窗口", async () =>
        {
            var response = await session.LoginMicrosoftAsync();
            if (!response.Ok)
                throw new IOException(response.Message);
            (TopLevel.GetTopLevel(content) as Window)?.Close();
        }, true);
        return Ui.Show(owner, "正版账号登录", content, 500, 350, Ui.DialogActions(content, login), "请在 Microsoft 官方页面完成授权。");
    }
    static Task Mua(ILauncherService session, UiContext ui, Window owner)
    {
        var user = Ui.Input("", "请输入 MUA 账号");
        var password = Ui.Input("", "请输入密码");
        password.PasswordChar = '●';
        var content = Ui.Stack(Ui.Field("账号", user), Ui.Field("密码", password));
        var login = Ui.Button("登录 MUA", async () =>
        {
            var secret = password.Text ?? "";
            password.Text = "";
            var response = await session.LoginMuaAsync(account: user.Text ?? "", password: secret);
            if (!response.Ok)
                throw new IOException(response.Message);
            (TopLevel.GetTopLevel(content) as Window)?.Close();
        }, true);
        return Ui.Show(owner, "MUA 登录", content, 500, 380, Ui.DialogActions(content, login), "使用 MUA 账号验证身份并选择角色。");
    }
    static Task Offline(ILauncherService session, UiContext ui, Window owner)
    {
        var name = Ui.Input(session.State.Player, "请输入离线角色名");
        var content = Ui.Stack(Ui.Field("角色名", name), Ui.Muted("仅用于离线游戏，不修改正版或 MUA 角色。"));
        var save = Ui.Button("保存并切换", async () =>
        {
            await session.SaveSettingsAsync(new AppSettingsPatch { Player = name.Text ?? "" });
            (TopLevel.GetTopLevel(content) as Window)?.Close();
        }, true);
        return Ui.Show(owner, "切换本地账号", content, 460, 300, Ui.DialogActions(content, save));
    }
    public static Task Skin(ILauncherService session, UiContext ui, Window owner)
    {
        var preview = Ui.Avatar(ui.Avatar, 96);
        preview.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center;
        var content = Ui.Stack(preview, Ui.Text(session.State.Player + " · " + Ui.AccountLabel(session.State.AccountType)));
        var actions = new List<Control> { Ui.Button("重新读取皮肤", () => session.ReadAvatarAsync()) };
        if (session.State.AccountType == "offline")
        {
            actions.Add(Ui.Button("选择本地 PNG 皮肤", async () =>
            {
                var path = await FilePicker.Open((Window)TopLevel.GetTopLevel(content)!, "PNG 皮肤", "*.png");
                if (path != null)
                    await session.SetOfflineSkinAsync(path: path);
            }, true));
            actions.Insert(0, Ui.Button("恢复默认头像", () => session.SetOfflineSkinAsync(path: "")));
        }
        content.Children.Add(Ui.Text("本地皮肤仅用于启动器头像；正版/MUA 皮肤来自认证站。暂不支持上传披风。"));
        void Update(AvatarInfo message)
        {
            var previous = preview.Source as IDisposable;
            preview.Source = Ui.Bitmap(ui.Avatar);
            previous?.Dispose();
        }
        return Show();
        async Task Show()
        {
            session.AvatarReceived += Update;
            try
            {
                await Ui.Show(owner, "皮肤与披风", content, 600, 450, Ui.DialogActions(content, actions.ToArray()), "管理启动器头像和角色皮肤预览。");
            }
            finally { session.AvatarReceived -= Update; (preview.Source as IDisposable)?.Dispose(); }
        }
    }
    public static async Task SelectProfile(ILauncherService session, UiContext ui, Window owner, MuaProfilesInfo message)
    {
        var list = Ui.Select(message.Items);
        var selected = false;
        var button = Ui.Button("使用选中角色", async () =>
            {
                if (list.SelectedItem is ProfileInfo item)
                {
                    selected = true;
                    await session.SelectMuaProfileAsync(token: message.Token, profileId: item.Id);
                    (TopLevel.GetTopLevel(list) as Window)?.Close();
                }
            }, true);
        var content = Ui.Stack(Ui.Field("本次登录的角色", list), Ui.Muted("取消或关闭弹窗将结束本次登录。"));
        await Ui.Show(owner, "选择 MUA 角色", content, 480, 310, Ui.DialogActions(content, button));
        if (!selected)
            await session.SelectMuaProfileAsync(token: message.Token, profileId: "");
    }
}
