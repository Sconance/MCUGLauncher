using System.Windows;
using System.Windows.Threading;

namespace MCUGLauncher;

/// <summary>
/// 提供 App 相关功能。
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 执行 OnStartup。
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnUnhandledException;
        base.OnStartup(e);
    }

    static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "mcug-launcher-error.txt"),
                e.Exception.ToString());
        }
        catch
        {
            // 日志失败不应掩盖原始错误。
        }
        MessageBox.Show(e.Exception.ToString(), "MCUG Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
