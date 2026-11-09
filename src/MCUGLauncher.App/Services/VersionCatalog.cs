using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BlockLaunch;
using MCUGLauncher.Models;

namespace MCUGLauncher.Services;

/// <summary>
/// 本地版本目录扫描。
/// </summary>
/// <remarks>
/// B component: constructs Models.VersionGroup/InstanceInfo/ModInfo and reads InstanceInfo.Name.
/// Reuses Core InstanceCatalog.MetadataDirectory/DirectoryFor, AtomicFile.ReadJson and ContentService.SafeFile.
/// Reads versions and instance JSON only; no network, UI or command-handler dependency.
/// Empty directories return an empty collection rather than a synthetic selectable instance.
/// </remarks>
public sealed class VersionCatalog
{
    /// <summary>
    /// 获取或设置 分组。
    /// </summary>
    public IReadOnlyList<VersionGroup> Groups
    {
        get;
        init;
    } = [];

    /// <summary>
    /// 获取或设置 Instances。
    /// </summary>
    public IReadOnlyList<InstanceInfo> Instances
    {
        get;
        init;
    } = [];

    /// <summary>
    /// 获取或设置 Selected。
    /// </summary>
    public string Selected
    {
        get;
        init;
    } = "";

    /// <summary>
    /// 获取或设置 根目录。
    /// </summary>
    public string Root
    {
        get;
        init;
    } = "";

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
                instances.Add(new InstanceInfo(name, loader, mods, 0)
                {
                    DisplayName = DisplayName(root, name),
                    Minecraft = detail.Minecraft,
                    VersionType = detail.VersionType
                });
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
        var file = Path.Combine(new InstanceCatalog(root).MetadataDirectory(key), ".blocklaunch-instance.json");
        if (!File.Exists(file))
            return key;
        try
        {
            return AtomicFile.ReadJson<InstanceDescriptor>(file).DisplayName;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return key;
        }
    }

    /// <summary>
    /// 读取实例的版本详情。
    /// </summary>
    /// <remarks>
    /// 版本号与版本类型的判定顺序对齐 PCL2 的 ModMinecraft.vb：
    /// 先看发布年份与 type=pending 这类硬条件，再依次从 clientVersion、--fml.mcVersion、
    /// 继承链、id / 文件夹名正则里取版本号，最后按 type 与愚人节名单给类型文案。
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
            var id = VersionNaming.String(json, "id") is { Length: > 0 } jsonId ? jsonId : name;
            var inherits = VersionNaming.String(json, "inheritsFrom");
            var type = VersionNaming.String(json, "type");
            var releaseTime = VersionNaming.ReleaseTime(json);
            var loader = DetectLoaderFromContent(json);
            if (loader == "原版")
                loader = DetectLoader(name + " " + inherits + " " + id);
            var java = json.TryGetProperty("javaVersion", out var javaVersion) &&
                       javaVersion.TryGetProperty("majorVersion", out var major)
                ? major.GetInt32().ToString()
                : "";

            // PCL2：发布年份在 2000–2012 之间直接归为远古版本，不再猜版本号
            if (VersionNaming.IsAncient(releaseTime))
                return new VersionDetails(name, "", loader, java, true, VersionNaming.Ancient);

            // PCL2：type=pending 是实验性快照，版本号本身不可靠
            if (type.Equals("pending", StringComparison.OrdinalIgnoreCase))
                return new VersionDetails(name, "", loader, java, true, VersionNaming.Experimental);

            var minecraft = ResolveMinecraft(root, name, json, id, inherits);
            return new VersionDetails(name, minecraft, loader, java, true,
                VersionNaming.Classify(json, releaseTime, name, minecraft));
        }
        catch (JsonException)
        {
            return new VersionDetails(name, "", "原版", "", false);
        }
    }

    /// <summary>按 PCL2 的顺序确定游戏版本号，取不到时返回空串。</summary>
    static string ResolveMinecraft(string root, string name, JsonElement json, string id, string inherits)
    {
        // 1. 版本 JSON 自带的 clientVersion / minecraftVersion（官方版本与多数整合包）
        if (OwnVersion(json) is { Length: > 0 } own)
            return own;
        // 2. Forge / NeoForge 写在启动参数里的 --fml.mcVersion
        if (Argument(json, "--fml.mcVersion") is { Length: > 0 } fml)
            return fml;
        // 3. 继承链：逐级向上找 clientVersion
        var probe = inherits;
        var seen = 0;
        for (var depth = 0; depth < 8 && probe.Length > 0 && seen < 8; depth++)
        {
            var probeFile = Path.Combine(root, "versions", probe, probe + ".json");
            if (!File.Exists(probeFile))
                break;
            using var probeDoc = JsonDocument.Parse(File.ReadAllText(probeFile));
            var parent = probeDoc.RootElement;
            if (OwnVersion(parent) is { Length: > 0 } parentVersion)
                return parentVersion;
            probe = VersionNaming.String(parent, "inheritsFrom");
            seen++;
        }

        // 4. 从 id / 继承值 / 文件夹名里按 PCL2 的正则提取（"Steam Era1.3" 这类名字不会误判）
        return VersionNaming.MatchVersion(id + " " + inherits)
               ?? VersionNaming.MatchVersion(name)
               ?? "";
    }

    static string OwnVersion(JsonElement json) =>
        VersionNaming.String(json, "clientVersion") is { Length: > 0 } client
            ? client
            : VersionNaming.String(json, "minecraftVersion");

    /// <summary>取 arguments.game 里某个参数的下一个值，形态支持 "--flag value" 与 "--flag=value"。</summary>
    static string Argument(JsonElement json, string flag)
    {
        if (!json.TryGetProperty("arguments", out var arguments)
            || !arguments.TryGetProperty("game", out var game)
            || game.ValueKind != JsonValueKind.Array)
            return "";
        var tokens = game.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? "")
            .ToArray();
        for (var index = 0; index < tokens.Length; index++)
        {
            if (tokens[index] == flag && index + 1 < tokens.Length)
                return tokens[index + 1];
            if (tokens[index].StartsWith(flag + "=", StringComparison.Ordinal))
                return tokens[index][(flag.Length + 1)..];
        }

        return "";
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

    /// <summary>
    /// 从版本 JSON 的 mainClass 与 libraries 判断加载器，和 PCL2 扫 JSON 文本的做法等价。
    /// 整合包常把版本目录与 id 都改成自己的名字（如「FO bingo 26.2」），只有库名靠得住。
    /// </summary>
    static string DetectLoaderFromContent(JsonElement json)
    {
        var text = new StringBuilder();
        if (json.TryGetProperty("mainClass", out var mainClass) && mainClass.ValueKind == JsonValueKind.String)
            text.Append(mainClass.GetString()).Append(' ');
        if (json.TryGetProperty("libraries", out var libraries) && libraries.ValueKind == JsonValueKind.Array)
            foreach (var library in libraries.EnumerateArray())
                if (library.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                    text.Append(name.GetString()).Append(' ');
        var haystack = text.ToString().ToLowerInvariant();
        // Quilt 依赖里带 fabric-loader，NeoForge 1.20.1 的库名里带 net.neoforged:forge，先判更具体的
        if (haystack.Contains("quilt-loader") || haystack.Contains("org.quiltmc.loader"))
            return "Quilt";
        if (haystack.Contains("fabric-loader") || haystack.Contains("net.fabricmc.loader"))
            return "Fabric";
        if (haystack.Contains("neoforge") || haystack.Contains("neoforged"))
            return "NeoForge";
        if (haystack.Contains("minecraftforge") || haystack.Contains("fmlloader") || haystack.Contains("fmlcore"))
            return "Forge";
        if (haystack.Contains("optifine"))
            return "OptiFine";
        return "原版";
    }

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
public sealed record VersionDetails(
    string Name,
    string Minecraft,
    string Loader,
    string Java,
    bool Complete,
    string VersionType = "");
