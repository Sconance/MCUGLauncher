using System.Text.Json;
using System.Text.RegularExpressions;
using BlockLaunch;
using MCUGLauncher.Models;

namespace MCUGLauncher.Services;

/// <summary>
/// 本地版本目录扫描。
/// </summary>
/// <remarks>
/// 只读 versions 目录与实例 JSON，不访问网络；供界面状态、启动与内容安装共用。
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

        if (instances.Count == 0)
            instances.Add(new InstanceInfo("未找到版本", "请安装版本", 0, 0) { IsPlaceholder = true });

        var current = instances.FirstOrDefault(i => i.Name.Equals(selected, StringComparison.OrdinalIgnoreCase))?.Name
            ?? instances[0].Name;
        return new VersionCatalog { Groups = groups, Instances = instances, Selected = current, Root = root };
    }

    static string DisplayName(string root, string key)
    {
        var file = Path.Combine(new InstanceCatalog(root).MetadataDirectory(key), ".blocklaunch-instance.json");
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
    /// 包含 Minecraft 版本、加载器与加载器版本。
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
            var id = json.TryGetProperty("id", out var idValue) ? idValue.GetString() ?? name : name;
            var inherits = json.TryGetProperty("inheritsFrom", out var parentValue) ? parentValue.GetString() ?? "" : "";
            var type = json.TryGetProperty("type", out var typeValue) ? typeValue.GetString() ?? "" : "";
            var minecraft = "";
            var probe = name;
            for (var depth = 0; depth < 8 && probe.Length > 0; depth++)
            {
                var probeFile = Path.Combine(root, "versions", probe, probe + ".json");
                if (!File.Exists(probeFile))
                    break;
                using var probeDoc = JsonDocument.Parse(File.ReadAllText(probeFile));
                if (probeDoc.RootElement.TryGetProperty("inheritsFrom", out var next))
                {
                    probe = next.GetString() ?? "";
                    continue;
                }

                if (probeDoc.RootElement.TryGetProperty("id", out var rootId))
                    minecraft = rootId.GetString() ?? "";
                break;
            }

            if (minecraft.Length == 0)
                minecraft = inherits.Length > 0 ? inherits : id;
            var loader = DetectLoader(name + " " + inherits + " " + id);
            var java = json.TryGetProperty("javaVersion", out var javaVersion) && javaVersion.TryGetProperty("majorVersion", out var major)
                ? major.GetInt32().ToString()
                : "";
            return new VersionDetails(name, minecraft, loader, java, true);
        }
        catch (JsonException)
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
    /// 设置Enabled。
    /// </summary>
    public static void SetEnabled(ModInfo mod, bool enabled)
    {
        var target = enabled
            ? mod.Path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                ? mod.Path[..^".disabled".Length]
                : mod.Path
            : mod.Path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                ? mod.Path
                : mod.Path + ".disabled";
        if (!target.Equals(mod.Path, StringComparison.OrdinalIgnoreCase))
            File.Move(mod.Path, target);
    }

    /// <summary>
    /// 删除。
    /// </summary>
    public static void Delete(ModInfo mod) => File.Delete(mod.Path);

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
        if (File.Exists(Path.Combine(metadata, ".blocklaunch-instance.json")))
            return catalog.DirectoryFor(instance);
        if (File.Exists(Path.Combine(metadata, ".blocklaunch-pack.json")))
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

    static string DetectLoader(string value)
    {
        var text = value.ToLowerInvariant();
        if (text.Contains("fabric"))
            return "Fabric";
        if (text.Contains("neoforge"))
            return "NeoForge";
        if (text.Contains("forge"))
            return "Forge";
        if (text.Contains("quilt"))
            return "Quilt";
        if (text.Contains("optifine"))
            return "OptiFine";
        return "原版";
    }
}

/// <summary>
/// 表示 版本Details 数据。
/// </summary>
public sealed record VersionDetails(string Name, string Minecraft, string Loader, string Java, bool Complete);
