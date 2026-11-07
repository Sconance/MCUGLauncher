using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MCUGLauncher.Services;
using MCUGLauncher.UseCases;

internal static class AvatarScenarios
{
    const string ImagePrefix = "data:image/png;base64,";
    static readonly byte[] Png =
        Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADUlEQVQImWNgYGD4DwABBAEAHnOcQAAAAABJRU5ErkJggg==");

    public static async Task Run(string fixture)
    {
        var directory = Path.Combine(fixture, "avatar-cache");
        var cache = new AvatarDiskCache(directory);
        var image = Image(Png);
        await cache.SaveAsync("account-a", "source-a", image, 1, CancellationToken.None);
        var reopened = new AvatarDiskCache(directory);
        Check((await reopened.ReadAsync("account-a", CancellationToken.None))?.Image == image,
            "avatar cache persists across instances");
        Check(await reopened.ReadAsync("account-b", CancellationToken.None) is null,
            "avatar cache isolates account identities");
        Check((await reopened.ReadSourceAsync("source-a", CancellationToken.None))?.Image == image,
            "avatar cache shares image by source");

        var old = DateTimeOffset.UtcNow.AddMinutes(-6);
        await reopened.SaveAsync("account-old", "source-old", image, 1, CancellationToken.None, old);
        var sourceEntry = await reopened.ReadSourceAsync("source-old", CancellationToken.None);
        Check(sourceEntry is not null && sourceEntry.Checked == old && !AvatarDiskCache.IsFresh(sourceEntry),
            "reusing a source does not extend its freshness");

        var alternate = Png.ToArray();
        alternate[^1] ^= 1;
        var alternateImage = Image(alternate);
        await reopened.SaveAsync("account-generation", "source-generation", alternateImage, 2, CancellationToken.None);
        await reopened.SaveAsync("account-generation", "source-generation", image, 1, CancellationToken.None);
        Check((await reopened.ReadAsync("account-generation", CancellationToken.None))?.Image == alternateImage,
            "older avatar generation cannot overwrite a newer result");

        var corrupted = Path.Combine(directory, "accounts", Hash("corrupt") + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(corrupted)!);
        await File.WriteAllTextAsync(corrupted, "{");
        Check(await reopened.ReadAsync("corrupt", CancellationToken.None) is null,
            "malformed avatar cache index is ignored");

        var skin = Path.Combine(fixture, "avatar-source.png");
        await File.WriteAllBytesAsync(skin, Png);
        using var context = new LauncherContext(
            _ => { },
            new ConfigStore(Path.Combine(fixture, "avatar-command")),
            avatarEncoder: bytes => bytes is null ? "default-avatar" : Image(bytes));
        context.Config.AccountType = "offline";
        context.Config.Player = "AvatarUser";
        context.Config.SkinFile = skin;
        var first = await context.Avatars.ReadAsync(force: true);
        File.Delete(skin);
        var failedRefresh = await context.Avatars.ReadAsync(force: true);
        Check(first is not null && failedRefresh is not null
            && failedRefresh.Image == first.Image && failedRefresh.Label == first.Label && !failedRefresh.Fallback,
            "failed avatar refresh retains the last cached image");
    }

    static string Image(byte[] bytes) => ImagePrefix + Convert.ToBase64String(bytes);

    static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    static void Check(bool condition, string name)
    {
        if (!condition)
            throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
}
