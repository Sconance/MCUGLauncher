using Avalonia.Controls;
using System.Globalization;

namespace MCUGLauncher.Avalonia.Views;

internal sealed class AppearancePage : NativePage
{
    public AppearancePage(ILauncherService session, UiContext ui) : base(session, ui)
    {
        Title("界面与背景", "保留蓝白主题，可设置本地背景；保存后立即应用。");
        var preferences = State.Preferences;
        var path = Ui.Input(preferences.BackgroundPath, "不使用背景图片时留空");
        var opacity = new Slider { Minimum = 0, Maximum = 100, Value = preferences.BackgroundOpacity * 100, TickFrequency = 1 };
        var percentage = Ui.Muted(opacity.Value.ToString("0", CultureInfo.InvariantCulture) + "%");
        opacity.ValueChanged += (_, _) => percentage.Text = opacity.Value.ToString("0", CultureInfo.InvariantCulture) + "%";
        var fit = Ui.Select(new[] { "裁剪填充", "完整显示", "拉伸" });
        fit.SelectedItem = preferences.BackgroundFit;
        Body.Children.Add(Ui.SettingsCard("背景图片", Ui.Field("本地图片", path),
            Ui.Button("选择图片", async () => path.Text = await FilePicker.Open(Owner, "背景图片", "*.png", "*.jpg", "*.jpeg") ?? path.Text),
            Ui.Setting("透明度", "降低透明度可保持内容清晰", Ui.Stack(opacity, percentage)),
            Ui.Setting("显示方式", "裁剪填充保留比例；完整显示可能留边", fit)));
        async Task Save(string value)
        {
            value = value.Trim();
            if (value.Length > 0)
            {
                using var image = new global::Avalonia.Media.Imaging.Bitmap(value);
            }
            await Session.SaveSettingsAsync(new AppSettingsPatch { Appearance = new AppearancePatch { Path = value, Opacity = opacity.Value / 100, Fit = fit.SelectedItem as string ?? "裁剪填充" } });
        }
        Body.Children.Add(Ui.Row(Ui.Button("保存外观设置", () => Save(path.Text ?? ""), true),
            Ui.Button("清除背景", async () => { await Save(""); path.Text = ""; })));
        Body.Children.Add(Ui.SettingsCard("窗口显示", Ui.Text("恢复默认大小不会修改游戏窗口尺寸。"), Ui.Button("恢复默认窗口尺寸", () => Session.ResetWindowAsync())));
    }
}
