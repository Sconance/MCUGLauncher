using Avalonia;

namespace MCUGLauncher.Avalonia;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var directory = Array.IndexOf(args, "--data-directory");
        if (directory >= 0)
        {
            if (directory + 1 >= args.Length)
                throw new ArgumentException("--data-directory requires a directory.");
            App.DataDirectory = Path.GetFullPath(args[directory + 1]);
        }
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
