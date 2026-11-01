using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BlockLaunch;

/// <summary>
/// 提供 头像Renderer 相关功能。
/// </summary>
public static class AvatarRenderer
{
    /// <summary>
    /// 执行 From皮肤。
    /// </summary>
    public static BitmapSource FromSkin(byte[] bytes)
    {
        if (bytes.Length < 33 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) || !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8))
            throw new InvalidDataException("皮肤必须是 PNG 图像。");
        var width = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4));
        var height = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4));
        if (width < 64 || width > 1024 || width % 64 != 0 || height != width && height != width / 2)
            throw new InvalidDataException("不支持的皮肤尺寸。");
        using var input = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = input;
        image.EndInit();
        image.Freeze();
        if (image.PixelWidth < 64 || image.PixelWidth > 1024 || image.PixelWidth % 64 != 0 || image.PixelHeight != image.PixelWidth && image.PixelHeight * 2 != image.PixelWidth)
            throw new InvalidDataException("不支持的皮肤尺寸。");
        var scale = image.PixelWidth / 64;
        var size = 8 * scale;
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawImage(new CroppedBitmap(image, new Int32Rect(8 * scale, 8 * scale, size, size)), new Rect(0, 0, size, size));
            drawing.DrawImage(new CroppedBitmap(image, new Int32Rect(40 * scale, 8 * scale, size, size)), new Rect(0, 0, size, size));
        }

        var result = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        result.Render(visual);
        result.Freeze();
        return result;
    }

    /// <summary>
    /// 执行 Default。
    /// </summary>
    public static BitmapSource Default()
    {
        // Original pixel artwork used when no skin is selected or the network is unavailable.
        var pixels = new byte[8 * 8 * 4];
        for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                var color = y < 2 || y == 2 && (x == 0 || x == 7) ? Color.FromRgb(71, 47, 34) : Color.FromRgb(187, 135, 97);
                if (y == 4 && x is 1 or 2 or 5 or 6)
                    color = x is 2 or 5 ? Color.FromRgb(56, 91, 159) : Colors.White;
                if (y == 6 && x is >= 2 and <= 5)
                    color = Color.FromRgb(103, 64, 46);
                var i = (y * 8 + x) * 4;
                pixels[i] = color.B;
                pixels[i + 1] = color.G;
                pixels[i + 2] = color.R;
                pixels[i + 3] = 255;
            }

        var image = BitmapSource.Create(8, 8, 96, 96, PixelFormats.Bgra32, null, pixels, 32);
        image.Freeze();
        return image;
    }

    /// <summary>
    /// 执行 皮肤Uri。
    /// </summary>
    public static Uri SkinUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Host != "textures.minecraft.net" || uri.Scheme is not ("https" or "http") || !uri.AbsolutePath.StartsWith("/texture/", StringComparison.Ordinal) || uri.UserInfo.Length > 0)
            throw new InvalidDataException("皮肤来源无效。");
        return new UriBuilder(Uri.UriSchemeHttps, "textures.minecraft.net")
        {
            Path = uri.AbsolutePath
        }.Uri;
    }

    /// <summary>
    /// 执行 缓存路径。
    /// </summary>
    public static string CachePath(string directory, Uri uri) => Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri))) + ".png");

    /// <summary>
    /// 执行 下载。
    /// </summary>
    public static async Task<byte[]> Download(HttpClient http, Uri uri, CancellationToken token)
    {
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 2 * 1024 * 1024)
            throw new InvalidDataException("皮肤文件过大。");
        await using var source = await response.Content.ReadAsStreamAsync(token);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await source.ReadAsync(buffer, token)) > 0)
        {
            if (output.Length + read > 2 * 1024 * 1024)
                throw new InvalidDataException("皮肤文件过大。");
            await output.WriteAsync(buffer.AsMemory(0, read), token);
        }

        return output.ToArray();
    }
}
