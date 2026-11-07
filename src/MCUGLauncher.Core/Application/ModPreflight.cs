using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BlockLaunch;

/// <summary>
/// 表示 模组Finding 数据。
/// </summary>
public record ModFinding(string Severity, string File, string Message);

/// <summary>
/// 表示 模组Descriptor 数据。
/// </summary>
public record ModDescriptor(string Id, string Version, string File, string Loader, Dictionary<string, string[]> Depends, Dictionary<string, string[]> Breaks, bool Nested = false);

/// <summary>
/// 表示 模组Scan 数据。
/// </summary>
public record ModScan(List<ModDescriptor> Mods, List<ModFinding> Findings)
{
    public string Summary => $"{Mods.Count} 个元数据条目 · {Findings.Count(f => f.Severity == "错误")} 项明确问题 · {Findings.Count(f => f.Severity != "错误")} 项需核对";
}

/// <summary>
/// 提供 模组Preflight 相关功能。
/// </summary>
public static class ModPreflight
{
    /// <summary>
    /// 扫描。
    /// </summary>
    public static ModScan Scan(string directory, string minecraft, string loader, int java = 0, string loaderVersion = "", CancellationToken token = default)
    {
        var mods = new List<ModDescriptor>();
        var findings = new List<ModFinding>();
        var nestedFiles = 0;
        long nestedBytes = 0;
        var path = Path.Combine(directory, "mods");
        if (!Directory.Exists(path))
            return new(mods, findings);
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("不扫描链接 Mod 目录。");
        foreach (var file in Directory.EnumerateFiles(path, "*.jar").Take(3001))
        {
            token.ThrowIfCancellationRequested();
            if (mods.Count > 3000)
            {
                findings.Add(new("提示", "", "条目过多，已停止扫描。"));
                break;
            }

            try
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("不扫描链接文件");
                using var stream = File.OpenRead(file);
                using var archive = new ZipArchive(stream);
                var before = mods.Count;
                Read(archive, Path.GetFileName(file), false, 0);
                if (before == mods.Count)
                    findings.Add(new("提示", Path.GetFileName(file), "未识别元数据，交由加载器判断；可能是旧模组或辅助库。"));
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or ArgumentException or KeyNotFoundException or InvalidOperationException)
            {
                findings.Add(new("提示", Path.GetFileName(file), "无法读取元数据：" + ex.Message));
            }
        }

        foreach (var group in mods.Where(m => !m.Nested).GroupBy(m => m.Id).Where(g => g.Select(m => m.File).Distinct().Count() > 1))
            findings.Add(new("错误", string.Join("、", group.Select(m => m.File)), "重复的 Mod ID：" + group.Key));
        var installed = mods.GroupBy(m => m.Id).ToDictionary(g => g.Key, g => g.Select(m => m.Version).ToList());
        installed["minecraft"] = [minecraft];
        if (java > 0)
            installed["java"] = [java.ToString()];
        if (loaderVersion.Length > 0)
            installed[loader switch
            {
                "fabric" => "fabricloader",
                "quilt" => "quilt_loader",
                _ => loader
            }] = [loaderVersion];
        foreach (var mod in mods)
        {
            var compatible = mod.Loader == loader || mod.Loader == "fabric" && loader == "quilt";
            if (!compatible)
            {
                findings.Add(new("错误", mod.File, $"{mod.Id} 是 {mod.Loader} 模组，当前为 {loader}。"));
                continue;
            }

            foreach (var dep in mod.Depends)
            {
                if (!installed.TryGetValue(dep.Key, out var versions))
                {
                    if (dep.Key is "java" or "fabricloader" or "quilt_loader" or "forge" or "neoforge")
                    {
                        findings.Add(new("提示", mod.File, $"未取得 {dep.Key} 版本，无法核对 {string.Join(" 或 ", dep.Value)}。"));
                        continue;
                    }

                    findings.Add(new("错误", mod.File, $"{mod.Id} 缺少前置 {dep.Key}（{string.Join(" 或 ", dep.Value)}）。"));
                    continue;
                }

                var results = versions.SelectMany(v => dep.Value.Select(r => Matches(v, r))).ToList();
                if (results.All(r => r == false))
                    findings.Add(new("错误", mod.File, $"{mod.Id} 要求 {dep.Key} {string.Join(" 或 ", dep.Value)}，当前 {string.Join(" / ", versions)}。"));
                else if (!results.Contains(true))
                    findings.Add(new("提示", mod.File, $"{dep.Key} 的复杂版本条件暂不能确定：{string.Join(" 或 ", dep.Value)}。"));
            }

            foreach (var dep in mod.Breaks)
                if (installed.TryGetValue(dep.Key, out var versions) && versions.Any(v => dep.Value.Any(r => Matches(v, r) == true)))
                    findings.Add(new("错误", mod.File, $"{mod.Id} 声明与 {dep.Key} 不兼容。"));
        }

        return new(mods, findings.Distinct().ToList());
        void Read(ZipArchive zip, string file, bool nested, int depth)
        {
            token.ThrowIfCancellationRequested();
            if (depth > 3 || mods.Count > 3000)
                return;
            var json = zip.GetEntry("fabric.mod.json");
            var quilt = zip.GetEntry("quilt.mod.json");
            if (loader is "forge" or "neoforge" && (zip.GetEntry("META-INF/mods.toml") != null || zip.GetEntry("META-INF/neoforge.mods.toml") != null))
            {
                json = null;
                quilt = null;
            }

            if (json != null || quilt != null)
            {
                using var doc = JsonDocument.Parse(Text(json ?? quilt!));
                var root = doc.RootElement;
                if (json == null)
                    root = root.GetProperty("quilt_loader");
                var id = root.GetProperty("id").GetString()!;
                var version = root.GetProperty("version").GetString() ?? "";
                var depends = new Dictionary<string, string[]>();
                var breaks = new Dictionary<string, string[]>();
                if (json != null)
                {
                    foreach (var pair in new[] {
                        ("depends", depends),
                        ("breaks", breaks)
                    }

                    )
                        if (root.TryGetProperty(pair.Item1, out var set))
                            foreach (var item in set.EnumerateObject())
                                pair.Item2[item.Name] = Ranges(item.Value);
                }
                else if (root.TryGetProperty("depends", out var set) && set.ValueKind == JsonValueKind.Array)
                    foreach (var dep in set.EnumerateArray())
                        if (dep.ValueKind == JsonValueKind.Object && dep.TryGetProperty("id", out var name) && (!dep.TryGetProperty("optional", out var optional) || optional.ValueKind != JsonValueKind.True))
                            depends[name.GetString()!] = dep.TryGetProperty("versions", out var ranges) ? Ranges(ranges) : ["*"];
                mods.Add(new(id, version, file, json != null ? "fabric" : "quilt", depends, breaks, nested));
                if (root.TryGetProperty("provides", out var provides) && provides.ValueKind == JsonValueKind.Array)
                    foreach (var alias in provides.EnumerateArray())
                        if (alias.ValueKind == JsonValueKind.String)
                            mods.Add(new(alias.GetString()!, version, file, json != null ? "fabric" : "quilt", new(), new(), true));
                if (root.TryGetProperty("jars", out var jars) && jars.ValueKind == JsonValueKind.Array)
                    foreach (var jar in jars.EnumerateArray())
                    {
                        var name = jar.ValueKind == JsonValueKind.String ? jar.GetString() : jar.GetProperty("file").GetString();
                        var entry = zip.GetEntry(name!);
                        if (entry == null || entry.Length > 32 * 1024 * 1024 || ++nestedFiles > 256 || (nestedBytes += entry.Length) > 128 * 1024 * 1024)
                        {
                            findings.Add(new("提示", file, "内嵌模组扫描达到大小或数量限制，依赖检查可能不完整。"));
                            continue;
                        }

                        using var data = new MemoryStream();
                        using (var input = entry.Open())
                            input.CopyTo(data);
                        data.Position = 0;
                        using var child = new ZipArchive(data);
                        Read(child, file + "!" + name, true, depth + 1);
                    }

                return;
            }

            var toml = zip.GetEntry("META-INF/neoforge.mods.toml") ?? zip.GetEntry("META-INF/mods.toml");
            if (toml == null)
                return;
            var jarMetadata = zip.GetEntry("META-INF/jarjar/metadata.json");
            if (jarMetadata != null)
            {
                using var nestedDoc = JsonDocument.Parse(Text(jarMetadata));
                if (nestedDoc.RootElement.TryGetProperty("jars", out var packaged))
                    foreach (var item in packaged.EnumerateArray())
                    {
                        if (!item.TryGetProperty("path", out var nestedPath))
                            continue;
                        var entry = zip.GetEntry(nestedPath.GetString()!);
                        if (entry == null || entry.Length > 32 * 1024 * 1024 || ++nestedFiles > 256 || (nestedBytes += entry.Length) > 128 * 1024 * 1024)
                        {
                            findings.Add(new("提示", file, "内嵌模组扫描达到大小或数量限制，依赖检查可能不完整。"));
                            continue;
                        }

                        using var data = new MemoryStream();
                        using (var input = entry.Open())
                            input.CopyTo(data);
                        data.Position = 0;
                        using var child = new ZipArchive(data);
                        Read(child, file + "!" + entry.FullName, true, depth + 1);
                    }
            }
            // Read simple literal fields only. Complex TOML remains an explicit unknown rather than a guessed constraint.
            var text = Text(toml);
            var kind = toml.FullName.Contains("neoforge") ? "neoforge" : "forge";
            var sections = Regex.Matches(text, @"(?ms)^\s*\[\[(?<section>[^\]]+)\]\](?<body>.*?)(?=^\s*\[|\z)");
            string Value(string body, string key) => Regex.Match(body, @"(?m)^\s*" + Regex.Escape(key) + "\\s*=\\s*[\"'](?<v>[^\"'\\r\\n]*)[\"']\\s*(?:#.*)?$").Groups["v"].Value;
            foreach (Match section in sections.Where(s => s.Groups["section"].Value.Trim() == "mods"))
            {
                var body = section.Groups["body"].Value;
                var id = Value(body, "modId");
                if (id.Length == 0)
                    continue;
                var deps = new Dictionary<string, string[]>();
                var incompatible = new Dictionary<string, string[]>();
                foreach (Match dep in sections.Where(s => s.Groups["section"].Value.Trim() == "dependencies." + id))
                {
                    var value = dep.Groups["body"].Value;
                    if (Value(value, "side") == "SERVER")
                        continue;
                    var name = Value(value, "modId");
                    if (name.Length == 0)
                        continue;
                    var required = Regex.IsMatch(value, @"(?m)^\s*mandatory\s*=\s*true\b") || Value(value, "type") == "required";
                    if (required)
                        deps[name] = [Value(value, "versionRange") is { Length: > 0 } range ? range : "*"];
                    if (Value(value, "type") == "incompatible")
                        incompatible[name] = [Value(value, "versionRange")];
                }

                mods.Add(new(id, Value(body, "version"), file, kind, deps, incompatible, nested));
            }
        }
    }

    static string Text(ZipArchiveEntry entry)
    {
        if (entry.Length > 1024 * 1024)
            throw new IOException("元数据超过 1 MiB");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    static string[] Ranges(JsonElement value) => value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToArray() : value.ValueKind == JsonValueKind.String ? [value.GetString()!] : ["[无法解析]"];

    /// <summary>
    /// 执行 Matches。
    /// </summary>
    public static bool? Matches(string actual, string rule)
    {
        rule = rule.Trim();
        if (rule is "*" or "")
            return true;
        if (actual == rule)
            return true;
        if (rule.Contains("||"))
        {
            var parts = rule.Split("||").Select(r => Matches(actual, r)).ToList();
            return parts.Contains(true) ? true : parts.Contains(null) ? null : false;
        }

        var range = Regex.Match(rule, @"^(?<left>[\[\(])(?<min>[^,]*),(?<max>[^\]\)]*)(?<right>[\]\)])$");
        if (range.Success)
        {
            var lower = range.Groups["min"].Value.Trim();
            var upper = range.Groups["max"].Value.Trim();
            var a = lower.Length == 0 ? true : Compare(actual, lower) is
            {
            } c ? (range.Groups["left"].Value == "[" ? c >= 0 : c > 0) : (bool?)null;
            var b = upper.Length == 0 ? true : Compare(actual, upper) is
            {
            } d ? (range.Groups["right"].Value == "]" ? d <= 0 : d < 0) : (bool?)null;
            return a == false || b == false ? false : a == null || b == null ? null : true;
        }

        if (rule.StartsWith('[') && rule.EndsWith(']') && !rule.Contains(','))
            return actual == rule[1..^1];
        var conditions = rule.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (conditions.Length > 1)
        {
            var values = conditions.Select(r => Matches(actual, r)).ToList();
            return values.Contains(false) ? false : values.Contains(null) ? null : true;
        }

        var m = Regex.Match(rule, @"^(?<op>>=|<=|>|<|=|\^|~)?(?<v>\d+(?:\.(?:\d+|[xX*]))*(?:[-+][\w.+-]+)?)$");
        if (!m.Success)
            return null;
        var wanted = m.Groups["v"].Value;
        var op = m.Groups["op"].Value;
        if (wanted.Contains('*') || wanted.Contains('x') || wanted.Contains('X'))
        {
            if (op.Length > 0 && op != "=")
                return null;
            var prefix = Regex.Split(wanted, @"[xX*]")[0];
            return actual.StartsWith(prefix, StringComparison.Ordinal) || actual == prefix.TrimEnd('.');
        }

        var comparison = Compare(actual, wanted);
        if (comparison == null)
            return null;
        if (op is "^" or "~")
        {
            var numbers = wanted.Split('+')[0].Split('-')[0].Split('.').Select(int.Parse).ToArray();
            var increment = op == "~" ? Math.Min(1, numbers.Length - 1) : Array.FindIndex(numbers, n => n != 0);
            if (increment < 0)
                increment = numbers.Length - 1;
            var upper = numbers.ToArray();
            upper[increment]++;
            for (var i = increment + 1; i < upper.Length; i++)
                upper[i] = 0;
            return comparison >= 0 && Compare(actual, string.Join('.', upper)) < 0;
        }

        return op switch
        {
            ">=" => comparison >= 0,
            "<=" => comparison <= 0,
            ">" => comparison > 0,
            "<" => comparison < 0,
            _ => comparison == 0
        };
    }

    static int? Compare(string a, string b)
    {
        var match = @"^\d+(?:\.\d+)*(?:-[\w.-]+)?(?:\+[\w.+-]+)?$";
        if (!Regex.IsMatch(a, match) || !Regex.IsMatch(b, match))
            return null;
        var av = a.Split('+')[0].Split('-', 2);
        var bv = b.Split('+')[0].Split('-', 2);
        var aa = av[0].Split('.');
        var bb = bv[0].Split('.');
        for (var i = 0; i < Math.Max(aa.Length, bb.Length); i++)
        {
            if (!int.TryParse(i < aa.Length ? aa[i] : "0", out var x) || !int.TryParse(i < bb.Length ? bb[i] : "0", out var y))
                return null;
            if (x != y)
                return x.CompareTo(y);
        }

        if (av.Length != bv.Length)
            return av.Length == 1 ? 1 : -1;
        if (av.Length == 1)
            return 0;
        var ap = av[1].Split('.');
        var bp = bv[1].Split('.');
        for (var i = 0; i < Math.Min(ap.Length, bp.Length); i++)
        {
            var an = int.TryParse(ap[i], out var x);
            var bn = int.TryParse(bp[i], out var y);
            var c = an && bn ? x.CompareTo(y) : an != bn ? an ? -1 : 1 : string.CompareOrdinal(ap[i], bp[i]);
            if (c != 0)
                return c;
        }

        return ap.Length.CompareTo(bp.Length);
    }
}
