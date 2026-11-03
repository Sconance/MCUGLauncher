using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Runtime.InteropServices;
using System.Buffers.Binary;

namespace MCUGLauncher.Avalonia;

/// <summary>Avalonia 皮肤头像编码：输出 data:image/png;base64。</summary>
internal static class AvaloniaAvatarImage
{
    public static string Encode(byte[]? skin)
    {
        using var output = new MemoryStream();
        if (skin is null)
            RenderDefault().Save(output);
        else
            RenderSkin(skin).Save(output);
        return "data:image/png;base64," + Convert.ToBase64String(output.ToArray());
    }

    static Bitmap RenderSkin(byte[] bytes)
    {
        if (bytes.Length < 33
            || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            || !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8))
            throw new InvalidDataException("皮肤必须是 PNG 图像。");
        var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4));
        if (width < 64 || width > 1024 || width % 64 != 0 || (height != width && height * 2 != width))
            throw new InvalidDataException("不支持的皮肤尺寸。");

        using var input = new MemoryStream(bytes);
        using var source = new Bitmap(input);
        var scale = source.PixelSize.Width / 64;
        var size = 8 * scale;
        var target = new RenderTargetBitmap(new PixelSize(size * 7, size * 7), new Vector(96, 96));
        var options = new RenderOptions
        {
            BitmapInterpolationMode = BitmapInterpolationMode.None
        };
        using (var context = target.CreateDrawingContext())
        using (context.PushRenderOptions(options))
        {
            context.DrawImage(source, new Rect(8 * scale, 8 * scale, size, size), new Rect(4, 4, size * 6, size * 6));
            context.DrawImage(source, new Rect(40 * scale, 8 * scale, size, size), new Rect(0, 0, size * 7, size * 7));
        }

        return target;
    }

    static Bitmap RenderDefault()
    {
        var bitmap = new WriteableBitmap(new PixelSize(8, 8), new Vector(96, 96), PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);
        using (var buffer = bitmap.Lock())
        {
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    var color = y < 2 || (y == 2 && (x == 0 || x == 7))
                        ? Color.FromRgb(71, 47, 34)
                        : Color.FromRgb(187, 135, 97);
                    if (y == 4 && x is 1 or 2 or 5 or 6)
                        color = x is 2 or 5 ? Color.FromRgb(56, 91, 159) : Colors.White;
                    if (y == 6 && x is >= 2 and <= 5)
                        color = Color.FromRgb(103, 64, 46);
                    var offset = y * buffer.RowBytes + x * 4;
                    Marshal.WriteByte(buffer.Address, offset, color.B);
                    Marshal.WriteByte(buffer.Address, offset + 1, color.G);
                    Marshal.WriteByte(buffer.Address, offset + 2, color.R);
                    Marshal.WriteByte(buffer.Address, offset + 3, 255);
                }
            }
        }

        return bitmap;
    }
}
