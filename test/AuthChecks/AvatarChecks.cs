using System.IO;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BlockLaunch;

/// <summary>
/// 提供 头像Checks 相关功能。
/// </summary>
public static class AvatarChecks
{
    /// <summary>
    /// 执行 Run。
    /// </summary>
    public static void Run(bool live)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Check(live);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null)
            throw new Exception("Avatar checks failed", failure);
    }

    static void Check(bool live)
    {
        var bytes = new byte[64 * 64 * 4];
        for (var y = 8; y < 16; y++)
            for (var x = 8; x < 16; x++)
            {
                var index = (y * 64 + x) * 4;
                bytes[index + 2] = 255;
                bytes[index + 3] = 255;
            }

        var hat = (8 * 64 + 40) * 4;
        bytes[hat] = 255;
        bytes[hat + 3] = 255;
        var image = BitmapSource.Create(64, 64, 96, 96, PixelFormats.Bgra32, null, bytes, 256);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        var result = AvatarRenderer.FromSkin(stream.ToArray());
        var pixels = new byte[8 * 8 * 4];
        result.CopyPixels(pixels, 32, 0);
        if (result.PixelWidth != 8 || !result.IsFrozen || pixels[0] != 255 || pixels[2] != 0 || pixels[4 + 2] != 255)
            throw new Exception("Head/hat composite mismatch");
        Console.WriteLine("HEAD_CROP_AND_TRANSPARENT_HAT_COMPOSITE_PASSED");
        if (AvatarRenderer.SkinUri("http://textures.minecraft.net/texture/test").Scheme != "https")
            throw new Exception("Expected HTTPS");
        try
        {
            AvatarRenderer.SkinUri("https://example.com/texture/test");
            throw new Exception("Unexpected host accepted");
        }
        catch (InvalidDataException)
        {
        }

        if (AvatarRenderer.Default().PixelWidth != 8 || !AvatarRenderer.Default().IsFrozen)
            throw new Exception("Default avatar invalid");
        Console.WriteLine("OFFICIAL_SKIN_SOURCE_AND_DEFAULT_AVATAR_PASSED");
        if (live)
        {
            using var http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BlockLaunch");
            var profile = new MicrosoftAccountService(Path.Combine(directory, "accounts"), http).ReadSavedAccount();
            if (string.IsNullOrWhiteSpace(profile?.SkinUrl))
                throw new Exception("Saved account has no active skin");
            var uri = AvatarRenderer.SkinUri(profile.SkinUrl);
            var skin = AvatarRenderer.Download(http, uri, CancellationToken.None).GetAwaiter().GetResult();
            var actual = AvatarRenderer.FromSkin(skin);
            if (actual.PixelWidth < 8 || actual.PixelHeight != actual.PixelWidth)
                throw new Exception("Invalid live avatar");
            var avatarCache = Path.Combine(directory, "avatars");
            Directory.CreateDirectory(avatarCache);
            File.WriteAllBytes(AvatarRenderer.CachePath(avatarCache, uri), skin);
            Console.WriteLine("LIVE_SAVED_ACCOUNT_SKIN_DOWNLOAD_AND_AVATAR_RENDER_PASSED");
        }

        Console.WriteLine("ALL_AVATAR_CHECKS_PASSED");
    }
}
