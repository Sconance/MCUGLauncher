using System.Collections.Concurrent;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.Tui.Pages;

/// <summary>离线及已保存账号切换、MUA 登录和角色选择；不自行实现授权。</summary>
internal sealed class AccountsPage(ILauncherService service, Terminal terminal) : TerminalPage(service, terminal)
{
    public override string Title => "账号";

    public override async Task ShowAsync()
    {
        IO.Write($"当前账号：{State.Player} ({State.AccountType})");
        IO.Write("微软首次登录或重新授权需使用 Avalonia；TUI 不提供图形授权窗口。");
        var action = IO.Select(Title, ["切换账号模式", "修改离线玩家名", "登录 MUA", "移除已保存账号"]);
        switch (action)
        {
            case 0:
                var modes = new[] { "offline", "microsoft", "mua" };
                var mode = IO.Select("账号模式（在线模式使用已有登录缓存）", ["离线", "微软", "MUA"]);
                if (mode < 0)
                    return;
                var switched = await Service.SwitchAccountModeAsync(modes[mode]);
                IO.Write(switched.Message);
                if (switched.NeedsLogin)
                    IO.Write("需要先登录；微软请使用 Avalonia，MUA 可在本页登录。");
                break;
            case 1:
                IO.Write((await Service.SaveSettingsAsync(new AppSettingsPatch(Player: IO.Required("离线玩家名：")))).Message);
                break;
            case 2:
                await LoginMua();
                break;
            case 3:
                var selected = IO.Select("移除登录缓存", ["微软", "MUA"]);
                if (selected >= 0 && IO.Confirm("确定移除此账号的登录缓存？"))
                    IO.Write((await Service.ForgetAccountAsync(selected == 0 ? "microsoft" : "mua")).Message);
                break;
            default:
                return;
        }
        IO.Pause();
    }

    async Task LoginMua()
    {
        var account = IO.Required("MUA 账号：");
        var password = IO.Password();
        if (password.Length == 0)
            return;
        var pendingProfiles = new ConcurrentQueue<MuaProfilesInfo>();
        void OnProfiles(MuaProfilesInfo info) => pendingProfiles.Enqueue(info);
        Service.MuaProfilesReceived += OnProfiles;
        try
        {
            var login = Service.LoginMuaAsync(account, password);
            password = "";
            while (!login.IsCompleted)
            {
                if (pendingProfiles.TryDequeue(out var profiles))
                {
                    var selected = IO.Select("选择 MUA 角色", profiles.Items.Select(item => item.Name).ToArray());
                    await Service.SelectMuaProfileAsync(profiles.Token, selected < 0 ? "" : profiles.Items[selected].Id);
                }
                await Task.WhenAny(login, Task.Delay(100));
            }
            var result = await login;
            IO.Write((result.Ok == true ? "成功：" : "未完成：") + result.Message);
        }
        finally
        {
            Service.MuaProfilesReceived -= OnProfiles;
        }
    }
}
