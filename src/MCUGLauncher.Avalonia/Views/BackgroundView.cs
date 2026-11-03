using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace MCUGLauncher.Avalonia.Views;

// Presentation-only image lifetime. Configuration is saved by settings use cases.
internal sealed class BackgroundView : Image, IDisposable
{
    string loaded = "";
    PreferencesInfo? displayed;
    public void Update(PreferencesInfo preferences)
    {
        if (ReferenceEquals(displayed, preferences))
            return;
        displayed = preferences;
        Opacity = Math.Clamp(preferences.BackgroundOpacity, 0, 1);
        Stretch = preferences.BackgroundFit switch
        {
            "完整显示" => Stretch.Uniform,
            "拉伸" => Stretch.Fill,
            _ => Stretch.UniformToFill
        };
        var path = preferences.BackgroundPath;
        string key;
        try
        {
            key = File.Exists(path) ? path + File.GetLastWriteTimeUtc(path).Ticks : "";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { key = ""; }
        if (key == loaded)
            return;
        (Source as IDisposable)?.Dispose();
        Source = null;
        loaded = key;
        if (key.Length == 0)
            return;
        try
        {
            Source = new Bitmap(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { loaded = ""; }
    }
    public void Dispose()
    {
        (Source as IDisposable)?.Dispose();
        Source = null;
    }
}
