using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace MCUGLauncher.Avalonia;

public partial class App : Application
{
    internal static string? DataDirectory
    {
        get; set;
    }
    internal static LauncherFacade CreateSession(MCUGLauncher.UseCases.IDesktopHost host)
        => new(host, DataDirectory is null ? null : new MCUGLauncher.Services.ConfigStore(DataDirectory));
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new ShellWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
