using System.Text.Json;
using BlockLaunch;

namespace MCUGLauncher.Services;

// Reads installed metadata, never guesses loader versions from the display name.
internal static class InstanceRequirements
{
    public static PackRequirements Read(string root, string key)
    {
        ContentService.SafeFile(key);
        var pack = Path.Combine(root, "instances", key, ".blocklaunch-pack.json");
        if (File.Exists(pack))
        {
            var installed = AtomicFile.ReadJson<InstalledPack>(pack);
            return new(installed.Minecraft, installed.Loader, installed.LoaderVersion);
        }
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var next = key;
        var minecraft = "";
        var loader = "vanilla";
        var release = "";
        while (!string.IsNullOrWhiteSpace(next))
        {
            ContentService.SafeFile(next);
            if (next is "." or ".." || !seen.Add(next) || seen.Count > 16)
                throw new IOException("版本继承关系无效。");
            var file = Path.Combine(root, "versions", next, next + ".json");
            if (!File.Exists(file))
                throw new IOException("缺少版本元数据：" + next);
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var json = document.RootElement;
            if (loader == "vanilla" && json.TryGetProperty("libraries", out var libraries))
            {
                foreach (var library in libraries.EnumerateArray())
                {
                    var parts = library.GetProperty("name").GetString()?.Split(':') ?? [];
                    if (parts.Length < 3)
                        continue;
                    var kind = (parts[0] + ":" + parts[1]) switch
                    {
                        "net.fabricmc:fabric-loader" => "fabric",
                        "org.quiltmc:quilt-loader" => "quilt",
                        "net.minecraftforge:forge" => "forge",
                        "net.neoforged:neoforge" => "neoforge",
                        "optifine:OptiFine" => "optifine",
                        _ => ""
                    };
                    if (kind.Length > 0)
                    {
                        loader = kind;
                        release = parts[2];
                        break;
                    }
                }
            }
            if (json.TryGetProperty("inheritsFrom", out var parent))
                next = parent.GetString() ?? "";
            else
            {
                minecraft = json.GetProperty("id").GetString() ?? next;
                break;
            }
        }
        if (minecraft.Length == 0)
            throw new IOException("无法读取 Minecraft 版本。");
        if (loader == "forge" && release.StartsWith(minecraft + "-", StringComparison.Ordinal))
            release = release[(minecraft.Length + 1)..];
        return new(minecraft, loader, release);
    }
}
