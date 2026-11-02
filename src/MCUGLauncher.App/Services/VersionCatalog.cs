using System.Text.Json;
using System.Text.RegularExpressions;
using MCUGLauncher;
using MCUGLauncher.Models;

namespace MCUGLauncher.Services;

/// <summary>
/// 本地版本目录扫描。
/// </summary>
/// <remarks>
/// B component: constructs Models.VersionGroup/InstanceInfo/ModInfo and reads InstanceInfo.Name.
/// Reuses Core InstanceCatalog.MetadataDirectory/DirectoryFor, AtomicFile.ReadJson, ContentService.SafeFile and ProfileInspector.Read(string,string).
/// Reads versions and instance JSON only; no network, UI or command-handler dependency.
/// ContentService.Toggle/MoveToBackup handle mutations; member contracts: docs/components/VersionCatalog.md.
/// Empty directories return an empty collection rather than a synthetic selectable instance.
/// </remarks>
public sealed class VersionCatalog
{
    /// <summary>
    /// 获取或设置 分组。
    /// </summary>
    public IReadOnlyList<VersionGroup> Groups { get; init; } = [];
    /// <summary>
    /// 获取或设置 Instances。
    /// </summary>
    public IReadOnlyList<InstanceInfo> Instances { get; init; } = [];
    /// <summary>
    /// 获取或设置 Selected。
    /// </summary>
    public string Selected { get; init; } = "";
    /// <summary>
    /// 获取或设置 根目录。
    /// </summary>
    public string Root { get; init; } = "";

    /// <summary>
    /// 扫描版本并返回目录快照。
    /// </summary>
    /// <param name="root">游戏根目录</param>
    /// <param name="selected">上次选中的实例名</param>
    /// <returns>版本分组、实例列表与最终选中项</returns>
    /// <remarks>
    /// 选中实例不存在时回退到扫描到的第一个实例。
    /// </remarks>
    public static VersionCatalog Load(string root, string selected)
    {
        var groups = new List<VersionGroup>();
        var instances = new List<InstanceInfo>();
        var versionsDir = Path.Combine(root, "versions");
        var vanilla = new List<string[]>();
        var loaders = new Dictionary<string, List<string[]>>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(versionsDir))
        {
            foreach (var dir in Directory.EnumerateDirectories(versionsDir).OrderByDescending(Path.GetFileName))
            {
                var name = Path.GetFileName(dir);
                var jsonPath = Path.Combine(dir, name + ".json");
                if (!File.Exists(jsonPath))
                    continue;

                var detail = ReadDetails(root, name);
                var loader = detail.Loader;
                var meta = string.IsNullOrWhiteSpace(detail.Minecraft)
                    ? loader
                    : $"{detail.Minecraft} · {loader.ToLowerInvariant()}";
                var row = new[] { name, meta };
                if (loader == "原版")
                    vanilla.Add(row);
                else
                {
                    if (!loaders.TryGetValue(loader, out var list))
                        loaders[loader] = list = [];
                    list.Add(row);
                }

                var mods = CountMods(root, name);
                instances.Add(new InstanceInfo(name, loader, mods, 0) { DisplayName = DisplayName(root, name), Minecraft = detail.Minecraft });
            }
        }

        if (vanilla.Count > 0)
            groups.Add(new VersionGroup("原版游戏", vanilla.Count, vanilla));
        foreach (var pair in loaders.OrderBy(pair => pair.Key))
            groups.Add(new VersionGroup(pair.Key + " 版本", pair.Value.Count, pair.Value));

        var current = instances.FirstOrDefault(i => i.Name.Equals(selected, StringComparison.OrdinalIgnoreCase))?.Name
            ?? instances.FirstOrDefault()?.Name ?? "";
        return new VersionCatalog { Groups = groups, Instances = instances, Selected = current, Root = root };
    }

    static string DisplayName(string root, string key)
    {
        var file = Path.Combine(new InstanceCatalog(root).MetadataDirectory(key), ".mcuglauncher-instance.json");
        if (!File.Exists(file))
            return key;
        try
        {
            return AtomicFile.ReadJson<InstanceDescriptor>(file).DisplayName;
        }
        catch (Exception ex) when (ex is IOException or JsonException) { return key; }
    }

    /// <summary>
    /// 读取实例的版本详情。
    /// </summary>
    /// <remarks>
    /// 包含 Minecraft 版本、加载器、当前档案声明的 Java 主版本与元数据完整性。
    /// </remarks>
    public static VersionDetails ReadDetails(string root, string name)
    {
        var file = Path.Combine(root, "versions", name, name + ".json");
        if (!File.Exists(file))
            return new VersionDetails(name, "", "原版", "", false);

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var json = doc.RootElement;
            var requirements = ProfileInspector.Read(root, name);
            var loader = requirements.Loader switch
            {
                "fabric" => "Fabric",
                "quilt" => "Quilt",
                "forge" => "Forge",
                "neoforge" => "NeoForge",
                "optifine" => "OptiFine",
                _ => "原版"
            };
            var java = json.TryGetProperty("javaVersion", out var javaVersion) && javaVersion.TryGetProperty("majorVersion", out var major)
                ? major.GetInt32().ToString()
                : "";
            return new VersionDetails(name, requirements.Minecraft, loader, java, true);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new VersionDetails(name, "", "原版", "", false);
        }
    }

    /// <summary>
    /// 读取实例 mods 目录。
    /// </summary>
    /// <remarks>
    /// 返回文件名、大小、启用状态与版本信息。
    /// </remarks>
    public static IReadOnlyList<ModInfo> ReadMods(string root, string instance)
    {
        if (string.IsNullOrWhiteSpace(instance))
            return [];
        var dir = Path.Combine(InstanceDirectory(root, instance), "mods");
        if (!Directory.Exists(dir))
            return [];
        var result = new List<ModInfo>();
        foreach (var file in Directory.EnumerateFiles(dir).Where(IsModFile).OrderBy(Path.GetFileName))
        {
            var name = Path.GetFileName(file);
            var disabled = name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
            var clean = disabled ? name[..^".disabled".Length] : name;
            var version = Regex.Match(clean, @"(?<v>\d+(?:\.\d+){1,3}(?:[-+][\w.]+)?)").Groups["v"].Value;
            var id = clean.Replace(".jar", "", StringComparison.OrdinalIgnoreCase)
                .Replace("-fabric", "", StringComparison.OrdinalIgnoreCase)
                .Replace("-forge", "", StringComparison.OrdinalIgnoreCase);
            result.Add(new ModInfo(
                id,
                clean.Replace(".jar", "", StringComparison.OrdinalIgnoreCase),
                clean,
                version,
                !disabled,
                file,
                new FileInfo(file).Length,
                "Fabric"));
        }
        return result;
    }

    /// <summary>
    /// 通过 Core 切换启用状态，相同状态不重复切换。
    /// </summary>
    public static void SetEnabled(ModInfo mod, bool enabled)
    {
        if (mod.Enabled == enabled)
            return;
        ContentService.Toggle(LocalFile(mod));
    }

    /// <summary>经 Core 依赖校验移除模组，保留可恢复的文件备份。</summary>
    public static void Delete(ModInfo mod)
    {
        var instance = Directory.GetParent(Path.GetDirectoryName(mod.Path)!)!.FullName;
        ContentService.MoveToBackup(instance, LocalFile(mod));
    }

    static LocalContent LocalFile(ModInfo mod) => new(mod.Path, mod.Name, "", mod.Enabled ? "已启用" : "已禁用", null);

    /// <summary>
    /// 执行 实例Directory。
    /// </summary>
    public static string InstanceDirectory(string root, string instance)
    {
        ContentService.SafeFile(instance);
        if (instance is "." or "..")
            throw new IOException("无效的实例名称。");
        var catalog = new InstanceCatalog(root);
        var metadata = catalog.MetadataDirectory(instance);
        if (File.Exists(Path.Combine(metadata, ".mcuglauncher-instance.json")))
            return catalog.DirectoryFor(instance);
        if (File.Exists(Path.Combine(metadata, ".mcuglauncher-pack.json")))
            return metadata;
        return Path.Combine(root, "versions", instance);
    }

    static int CountMods(string root, string instance)
    {
        var dir = Path.Combine(InstanceDirectory(root, instance), "mods");
        return Directory.Exists(dir) ? Directory.EnumerateFiles(dir).Count(IsModFile) : 0;
    }

    static bool IsModFile(string file) =>
        file.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
        file.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase);

}
/// <summary>
/// 表示 版本Details 数据。
/// </summary>
public sealed record VersionDetails(string Name, string Minecraft, string Loader, string Java, bool Complete);
