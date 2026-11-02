using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media.Imaging;

namespace MCUGLauncher.Avalonia.Views;

internal partial class AccountView : UserControl
{
    LauncherFacade? facade;
    LauncherState? state;
    Bitmap? avatar;

    public AccountView()
    {
        InitializeComponent();
        WireButtons();
    }

    public AccountView(LauncherFacade facade) : this()
    {
        Attach(facade);
    }

    public void Attach(LauncherFacade value)
    {
        facade = value;
        facade.StateChanged += ApplyState;
        facade.AvatarReceived += OnAvatar;
        facade.AuthResultReceived += OnAuth;
        facade.AccountSwitchResultReceived += OnSwitchResult;
        facade.MuaProfilesReceived += OnProfiles;
        facade.PathPicked += OnPathPicked;
        facade.ToastReceived += info => Status(info.Message);
        facade.ErrorReceived += message => Status("操作失败：" + message);
        ApplyState(facade.State);
    }

    public void Activate()
    {
        if (facade is null)
            return;
        Status("正在读取头像…");
        _ = facade.ReadAvatarAsync();
    }

    void WireButtons()
    {
        MicrosoftLoginButton.Click += async (_, _) =>
        {
            AuthStatus.Text = "正在打开微软登录页…";
            await facade!.LoginMicrosoftAsync();
        };
        MuaLoginButton.Click += async (_, _) =>
        {
            var account = MuaAccountBox.Text?.Trim() ?? "";
            if (account.Length == 0 || string.IsNullOrEmpty(MuaPasswordBox.Text))
            {
                AuthStatus.Text = "请填写 MUA 账号和密码。";
                return;
            }
            AuthStatus.Text = "正在登录 MUA…";
            await facade!.LoginMuaAsync(account, MuaPasswordBox.Text ?? "");
        };
        OfflineSwitchButton.Click += async (_, _) =>
        {
            AuthStatus.Text = "正在切换到离线账号…";
            await facade!.SwitchAccountModeAsync("offline");
        };
        ForgetMicrosoftButton.Click += async (_, _) => await Forget("microsoft");
        ForgetMuaButton.Click += async (_, _) => await Forget("mua");
        ChooseSkinButton.Click += async (_, _) => await facade!.PickFileAsync("offlineSkin");
        ClearSkinButton.Click += async (_, _) =>
        {
            await facade!.SetOfflineSkinAsync("");
            Status("已清除本地皮肤。");
        };
    }

    async Task Forget(string mode)
    {
        if (facade is null)
            return;
        AuthStatus.Text = "正在移除本机保存的账号…";
        await facade.ForgetAccountAsync(mode);
    }

    void ApplyState(LauncherState? snapshot)
    {
        state = snapshot;
        if (snapshot is null)
            return;
        CurrentPlayer.Text = snapshot.Player.Length > 0 ? snapshot.Player : "未登录";
        CurrentMode.Text = snapshot.AccountLabel.Length > 0 ? snapshot.AccountLabel : "离线账号 · 本地启动";
        var offline = snapshot.AccountType == "offline";
        ChooseSkinButton.IsEnabled = offline;
        ClearSkinButton.IsEnabled = offline;
        SkinHint.Text = offline
            ? "本地皮肤仅对离线账号生效；选择 PNG 后立即生效。"
            : "在线账号的皮肤来自认证站，不能在此选择本地皮肤。";
    }

    void OnAvatar(AvatarInfo info)
    {
        avatar?.Dispose();
        avatar = Decode(info.Image);
        AvatarImage.Source = avatar;
        AvatarLabel.Text = info.Label;
        if (AuthStatus.Text == "正在读取头像…")
            AuthStatus.Text = "";
        SkinHint.Text = info.Fallback
            ? "正在使用默认头像；离线模式可选择本地皮肤。"
            : info.Account == "offline"
                ? "本地皮肤仅对离线账号生效。"
                : "在线账号皮肤来自认证站。";
    }

    void OnAuth(AuthResultInfo info)
    {
        AuthStatus.Text = info.Message;
        if (!info.Pending)
            ProfilesCard.IsVisible = false;
        if (info.Ok == true)
        {
            _ = facade!.RequestStateAsync();
            _ = facade.ReadAvatarAsync();
        }
    }

    void OnSwitchResult(AccountSwitchResultInfo info)
    {
        AuthStatus.Text = info.Message;
        if (info.NeedsLogin)
            AuthStatus.Text += " 请使用上方登录入口重新登录。";
    }

    void OnProfiles(MuaProfilesInfo info)
    {
        ProfilesPanel.Children.Clear();
        foreach (var profile in info.Items)
        {
            var button = new Button
            {
                Content = profile.Name,
                Classes = { "ghost" }
            };
            button.HorizontalAlignment = HorizontalAlignment.Left;
            var id = profile.Id;
            var token = info.Token;
            button.Click += async (_, _) =>
            {
                ProfilesCard.IsVisible = false;
                AuthStatus.Text = "已选择角色 " + profile.Name + "，正在完成登录…";
                await facade!.SelectMuaProfileAsync(token, id);
            };
            ProfilesPanel.Children.Add(button);
        }
        ProfilesCard.IsVisible = info.Items.Count > 0;
        if (info.Items.Count == 0)
            AuthStatus.Text = "MUA 登录未返回可选角色。";
    }

    void OnPathPicked(PickedPathInfo info)
    {
        if (info.Target != "offlineSkin")
            return;
        AuthStatus.Text = "正在应用本地皮肤…";
        _ = facade!.SetOfflineSkinAsync(info.Path);
    }

    void Status(string message) => AuthStatus.Text = message;

    static Bitmap? Decode(string dataUrl)
    {
        var comma = dataUrl.IndexOf(',');
        if (comma < 0 || comma + 1 >= dataUrl.Length)
            return null;
        try
        {
            var bytes = Convert.FromBase64String(dataUrl[(comma + 1)..]);
            using var input = new MemoryStream(bytes);
            return new Bitmap(input);
        }
        catch
        {
            return null;
        }
    }
}
