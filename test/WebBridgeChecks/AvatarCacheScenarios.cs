using System.Security.Cryptography;
using System.Text.Json;
using MCUGLauncher.Models;
using MCUGLauncher.Services;
using MCUGLauncher.UseCases;

internal static class AvatarCacheScenarios
{
    static void Check(bool condition, string name)
    {
        if (!condition)
            throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
    public static async Task Run(string fixture)
    {
        var folder = Path.Combine(fixture, "avatar-cache");
        var store = new ConfigStore(folder);
        var uuid = Guid.NewGuid().ToString("N");
        store.Save(new AppConfig { Root = fixture, Player = "DiskUser", AccountType = "microsoft" });
        var key = "microsoft:minecraft:" + uuid;
        const string image = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aBb8AAAAASUVORK5CYII=";
        const string source = "https://textures.minecraft.net/texture/cache-fixture";
        var cacheRoot = Path.Combine(folder, "cache", "avatars", "v1");
        var disk = new AvatarDiskCache(cacheRoot);
        await disk.SaveAsync(key, source, image, 1, CancellationToken.None);
        Check((await disk.ReadAsync(key, CancellationToken.None))?.Image == image, "avatar image and identity index survive disk roundtrip");
        Check((await disk.ReadSourceAsync(source, CancellationToken.None))?.Image == image, "skin source index reuses an existing image");
        Check(await disk.ReadAsync("mua:other-server:" + uuid, CancellationToken.None) == null, "same UUID on a different account provider cannot read another identity index");
        var messages = new List<JsonElement>();
        using (var context = new LauncherContext(value => messages.Add(FixtureMessages.Capture(value)), store))
        {
            context.AccountSessions.RememberIdentity("microsoft", new("DiskUser", uuid));
            var encoded = 0;
            await new AvatarCommands(context, _ => { encoded++; return image; }).ReadAsync(false);
            Check(encoded == 0 && messages.Count == 1 && !messages[0].GetProperty("fallback").GetBoolean(), "cached startup portrait is sent without generating Steve or re-encoding skin");
            messages.Clear();
            await new AvatarCommands(context, _ => image).ReadAsync(true);
            Check(messages.Count == 1 && !messages[0].GetProperty("fallback").GetBoolean(), "failed forced refresh retains disk portrait without publishing a fallback");
        }
        messages.Clear();
        using (var restarted = new LauncherContext(value => messages.Add(FixtureMessages.Capture(value)), store))
        {
            await new AccountModeCommands(restarted, readIdentity: mode => Task.FromResult<AccountIdentity?>(
                mode == "microsoft" ? new("DiskUser", uuid) : null)).PreloadIdentitiesAsync();
            await new AvatarCommands(restarted, _ => throw new Exception("Disk portrait should not render again")).ReadAsync(false);
            Check(messages.Single().GetProperty("image").GetString() == image, "new context restores the last portrait without network or renderer");
        }
        await disk.SaveAsync(key, "new-source", image, 9, CancellationToken.None);
        await disk.SaveAsync(key, "old-source", image, 2, CancellationToken.None);
        Check(await disk.ReadSourceAsync("old-source", CancellationToken.None) == null, "late older avatar write cannot replace a newer completed save");
        var indexPath = Directory.GetFiles(Path.Combine(cacheRoot, "accounts")).Single();
        var original = File.ReadAllText(indexPath);
        File.WriteAllText(indexPath, "{\"Format\":1}");
        Check(await disk.ReadAsync(key, CancellationToken.None) == null, "incomplete avatar index is treated as a cache miss");
        File.WriteAllText(indexPath, "{\"Format\":1,\"Image\":\"../../outside\"}");
        Check(await disk.ReadAsync(key, CancellationToken.None) == null, "avatar index cannot supply an arbitrary filesystem path");
        File.WriteAllText(indexPath, original);
        var bytes = Convert.FromBase64String(image[22..]);
        var file = Path.Combine(cacheRoot, "images", Convert.ToHexString(SHA256.HashData(bytes)) + ".png");
        File.WriteAllText(file, "corrupt");
        Check(await disk.ReadAsync(key, CancellationToken.None) == null, "corrupted avatar image fails its content digest check");
    }
}
