using System.IO.Compression;

namespace BlockLaunch;

/// <summary>
/// 定义 LocalImportKind 的取值。
/// </summary>
public enum LocalImportKind
{
    Unknown,
    Mod,
    Modpack,
    World,
    ResourcePack,
    ShaderPack,
    DataPack
}

/// <summary>
/// 提供 LocalImportClassifier 相关功能。
/// </summary>
public static class LocalImportClassifier
{
    /// <summary>
    /// 执行 Identify。
    /// </summary>
    public static LocalImportKind Identify(string file)
    {
        var extension = Path.GetExtension(file).ToLowerInvariant();
        if (extension == ".jar")
            return LocalImportKind.Mod;
        if (extension is not (".zip" or ".mrpack"))
            return LocalImportKind.Unknown;
        using var zip = ZipFile.OpenRead(file);
        if (zip.Entries.Count > 100_000)
            throw new IOException("压缩包文件数量过多。");
        var names = zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToHashSet(StringComparer.Ordinal);
        if (names.Contains("modrinth.index.json") || names.Contains("mmc-pack.json"))
            return LocalImportKind.Modpack;
        if (names.Contains("manifest.json") || names.Contains("mcbbs.packmeta"))
            throw new IOException("此整合包格式尚未支持，请使用 Modrinth 或 MultiMC/Prism 格式。");
        if (names.Any(n => n == "level.dat" || n.EndsWith("/level.dat") && n.Count(c => c == '/') == 1))
            return LocalImportKind.World;
        if (names.Any(n => n.StartsWith("shaders/")))
            return LocalImportKind.ShaderPack;
        if (names.Contains("pack.mcmeta"))
            return names.Any(n => n.StartsWith("data/")) && !names.Any(n => n.StartsWith("assets/")) ? LocalImportKind.DataPack : LocalImportKind.ResourcePack;
        return LocalImportKind.Unknown;
    }
}
