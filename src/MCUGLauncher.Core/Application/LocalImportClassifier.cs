using System.IO.Compression;
using System.Text.Json;

namespace MCUGLauncher;

/// <summary>本地文件的安装类别；未知文件不得根据扩展名直接安装。</summary>
public enum LocalImportKind
{
    Unknown, Mod, Modpack, World, ResourcePack, ShaderPack, DataPack, AmbiguousPack
}

/// <summary>压缩包识别结果。Loaders 来自模组标识，不推断游戏版本兼容性。</summary>
public sealed record LocalImportDetection(LocalImportKind Kind, string[] Loaders, string[]? Choices = null);

/// <summary>只读检查本地压缩包结构和路径，不复制文件、不修改实例。</summary>
public static class LocalImportClassifier
{
    public static LocalImportKind Identify(string file) => Inspect(file).Kind;

    public static LocalImportDetection Inspect(string file)
    {
        var extension = Path.GetExtension(file).ToLowerInvariant();
        if (extension is not (".jar" or ".zip" or ".mrpack"))
            return new(LocalImportKind.Unknown, []);
        using var zip = ZipFile.OpenRead(file);
        if (zip.Entries.Count > 100_000 || zip.Entries.Sum(e => e.Length) > 8_000_000_000L)
            throw new IOException("压缩包展开后过大或文件数量过多");
        var names = new HashSet<string>(StringComparer.Ordinal);
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            var path = entry.FullName.Replace('\\', '/').TrimEnd('/');
            if (path.Length == 0 || path.StartsWith('/') || path.Contains(':') || path.Split('/').Any(p => p is ".." or "." or "") ||
                !unique.Add(path) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                throw new IOException("压缩包包含不安全路径、重复文件或符号链接");
            names.Add(path);
        }
        if (extension == ".jar")
        {
            var loaders = new List<string>();
            if (names.Contains("fabric.mod.json"))
                loaders.Add("fabric");
            if (names.Contains("quilt.mod.json"))
                loaders.Add("quilt");
            if (names.Contains("META-INF/mods.toml"))
                loaders.Add("forge");
            if (names.Contains("META-INF/neoforge.mods.toml"))
                loaders.Add("neoforge");
            // 旧版 Forge 的 mcmod.info 无法可靠推断所需加载器版本。
            if (names.Contains("mcmod.info") && !loaders.Contains("forge"))
                loaders.Add("forge");
            return new(loaders.Count > 0 ? LocalImportKind.Mod : LocalImportKind.Unknown, loaders.ToArray());
        }
        // PackService 已支持包裹目录，识别阶段也必须按清单文件名查找。
        if (names.Any(n => Path.GetFileName(n) is "modrinth.index.json" or "mmc-pack.json"))
            return new(LocalImportKind.Modpack, []);
        if (names.Any(n => Path.GetFileName(n) == "mcbbs.packmeta") || zip.Entries.Any(IsCurseForgeManifest))
            throw new IOException("此整合包格式尚未支持，请使用 Modrinth 或 MultiMC/Prism 格式");
        if (extension == ".mrpack")
            return new(LocalImportKind.Unknown, []);
        if (names.Any(n => n == "level.dat" || n.EndsWith("/level.dat") && n.Count(c => c == '/') == 1))
            return new(LocalImportKind.World, []);
        var shader = names.Any(n => n.StartsWith("shaders/"));
        var pack = names.Contains("pack.mcmeta");
        if (shader && pack)
            return new(LocalImportKind.AmbiguousPack, [], ["resourcepacks", "shaderpacks"]);
        if (shader)
            return new(LocalImportKind.ShaderPack, []);
        if (pack)
        {
            var data = names.Any(n => n.StartsWith("data/"));
            var assets = names.Any(n => n.StartsWith("assets/"));
            return new(data == assets ? LocalImportKind.AmbiguousPack : data ? LocalImportKind.DataPack : LocalImportKind.ResourcePack, [], data == assets ? ["resourcepacks"] : null);
        }
        return new(LocalImportKind.Unknown, []);
    }
    // A resource ZIP can contain an unrelated manifest.json; its filename alone is not a pack signature.
    static bool IsCurseForgeManifest(ZipArchiveEntry entry)
    {
        if (entry.Name != "manifest.json" || entry.Length > 4 * 1024 * 1024)
            return false;
        try
        {
            using var input = entry.Open();
            using var json = JsonDocument.Parse(input);
            var root = json.RootElement;
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("minecraft", out var game) &&
                game.ValueKind == JsonValueKind.Object && root.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException) { return false; }
    }
}
