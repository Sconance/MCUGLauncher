using System.Windows.Media.Imaging;
using BlockLaunch;

namespace MCUGLauncher.Services;

// Media adapter: application commands exchange PNG data, never WPF bitmap objects.
internal static class AvatarImage
{
    public static string Encode(byte[]? skin)
    {
        var face = skin == null ? AvatarRenderer.Default() : AvatarRenderer.FromSkin(skin);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(face));
        using var output = new MemoryStream();
        png.Save(output);
        return "data:image/png;base64," + Convert.ToBase64String(output.ToArray());
    }
}
