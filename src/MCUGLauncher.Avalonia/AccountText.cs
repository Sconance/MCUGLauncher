namespace MCUGLauncher.Avalonia;

/// <summary>账号类型到界面文案的映射；业务层只返回 accountType。</summary>
internal static class AccountText
{
    public static string Label(string accountType) => accountType switch
    {
        "microsoft" => "正版账号 · 在线验证",
        "mua" => "MUA 账号 · 外置登录",
        _ => "离线账号 · 本地启动"
    };
}
