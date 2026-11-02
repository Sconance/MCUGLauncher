using Avalonia.Controls;

namespace MCUGLauncher.Avalonia.Views;

// Global and per-instance launch settings share one editor and validation path.
internal sealed class SettingsPage : NativePage
{
    readonly LaunchOptionsPage editor;
    public SettingsPage(LauncherFacade session) : base(session)
    {
        Title("启动设置", "全局设置作为默认值；实例可在版本设置中单独覆盖。");
        editor = new LaunchOptionsPage(session);
        editor.PrepareDialog();
        Body.Children.Add(editor);
        _ = Load();
    }
    async Task Load()
    {
        try
        {
            await editor.Load("", inline: true);
        }
        catch (Exception error) { Body.Children.Add(Ui.Text("读取设置失败：" + error.Message)); }
    }
    public override void Dispose()
    {
        editor.Dispose();
        base.Dispose();
    }
}
