using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace MCUGLauncher.Avalonia.Views;
// Only displays validated raster bytes; network access is delegated to the existing application policy.
internal sealed class RemoteImage : UserControl
{
    readonly ILauncherService session;
    readonly string url;
    readonly Image image = new()
    {
        MaxWidth = 640,
        MaxHeight = 300
    };
    readonly TextBlock status = Ui.Text("图片加载中…", 11);
    bool loaded;
    bool detached;
    public RemoteImage(ILauncherService session, UiContext ui, string url, double size = 0)
    {
        this.session = session;
        this.url = url;
        if (size > 0)
        {
            Width = size;
            Height = size;
            image.Width = size;
            image.Height = size;
        }

        Content = Ui.Stack(
            image,
            status
        );
        IsVisible = url.Length > 0;
    }

    protected override async void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        detached = false;
        if (loaded || url.Length == 0)
            return;
        loaded = true;
        try
        {
            byte[] bytes;
            if (url.StartsWith("data:image/png;base64,", StringComparison.Ordinal) || url.StartsWith("data:image/jpeg;base64,", StringComparison.Ordinal))
                bytes = Convert.FromBase64String(url[(url.IndexOf(',') + 1)..]);
            else
            {
                bytes = await session.DownloadImageAsync(url);
            }

            if (detached)
                return;
            if (bytes.Length > 2 * 1024 * 1024 || bytes.Length < 8)
                throw new IOException("图片大小不合适");
            // Require PNG/JPEG magic before decoding; SVG and document payloads are never rendered.
            if (!(bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71) && !(bytes[0] == 255 && bytes[1] == 216))
                throw new IOException("不是 PNG/JPEG");
            using var stream = new MemoryStream(bytes);
            using (var codec = SkiaSharp.SKCodec.Create(stream))
            {
                if (codec == null || codec.Info.Width > 8192 || codec.Info.Height > 8192)
                    throw new IOException("图片尺寸过大");
            }

            using var decoded = new MemoryStream(bytes);
            image.Source = Bitmap.DecodeToWidth(decoded, 640);
            status.IsVisible = false;
        }
        catch (Exception)
        {
            if (!detached)
                status.Text = "图片不可用";
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        detached = true;
        (image.Source as IDisposable)?.Dispose();
        image.Source = null;
        loaded = false;
        base.OnDetachedFromVisualTree(e);
    }
}
