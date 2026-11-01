using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BlockLaunch;

/// <summary>
/// 表示 PackRequirements 数据。
/// </summary>
public record PackRequirements(string Minecraft, string Loader, string LoaderVersion);

/// <summary>
/// 表示 InstalledPack 数据。
/// </summary>
public record InstalledPack(string Name, string Minecraft, string Loader, string LoaderVersion, string LaunchVersion, string InstanceId);

/// <summary>
/// 表示 PackOverview 数据。
/// </summary>
public record PackOverview(string Name, string Format, PackRequirements Requirements, int Entries);

/// <summary>
/// 提供 PackService 相关功能。
/// </summary>
public sealed class PackService(HttpClient http, CancellationToken token = default)
{
    /// <summary>
    /// 执行 Safe路径。
    /// </summary>
    public static string SafePath(string root, string relative)
    {
        relative = relative.Replace('\\', '/');
        var segments = relative.Split('/');
        if (segments.Length == 0 || segments.Any(s => s is "" or "." or ".." || s.EndsWith('.') || s.EndsWith(' ') || s.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) || Path.IsPathRooted(relative) || relative.Contains(':'))
            throw new IOException("整合包包含不安全的文件路径：" + relative);
        var basePath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(Path.Combine(basePath, relative));
        if (!target.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            throw new IOException("整合包文件超出实例目录。");
        return target;
    }

    static string Get(JsonElement json, string name, string fallback = "") => json.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() ?? fallback : fallback;

    static void ValidateVersion(string version)
    {
        if (!Regex.IsMatch(version, @"^[A-Za-z0-9_.+\-]+$") || version.Contains(".."))
            throw new IOException("整合包版本号无效。");
    }

    static PackRequirements Components(JsonElement json)
    {
        var dependencies = new Dictionary<string, string>();
        foreach (var component in json.GetProperty("components").EnumerateArray())
        {
            var uid = Get(component, "uid");
            var version = Get(component, "version");
            var key = uid switch
            {
                "net.minecraft" => "minecraft",
                "net.fabricmc.fabric-loader" => "fabric-loader",
                "org.quiltmc.quilt-loader" => "quilt-loader",
                "net.minecraftforge" => "forge",
                "net.neoforged" => "neoforge",
                "org.lwjgl" or "org.lwjgl3" => "",
                _ => throw new IOException("不支持的 MultiMC 组件：" + uid)
            };
            if (key != "")
                dependencies.Add(key, version);
        }

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(dependencies));
        return ParseRequirements(document.RootElement);
    }

    /// <summary>
    /// 读取Overview。
    /// </summary>
    public static PackOverview ReadOverview(string archivePath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > 100000 || archive.Entries.Sum(e => e.Length) > 8_000_000_000L)
            throw new IOException("整合包展开后过大。");
        var marker = archive.Entries.Where(e => e.Name is "modrinth.index.json" or "mmc-pack.json").OrderBy(e => e.FullName.Count(c => c == '/')).FirstOrDefault() ?? throw new IOException("未识别到支持的整合包清单。请使用 Modrinth .mrpack 或 MultiMC / Prism ZIP。");
        if (marker.Length > 4 * 1024 * 1024)
            throw new IOException("整合包清单过大。");
        using var stream = marker.Open();
        using var document = JsonDocument.Parse(stream);
        var json = document.RootElement;
        var name = Get(json, "name", Path.GetFileNameWithoutExtension(archivePath));
        if (marker.Name == "modrinth.index.json")
        {
            if (json.GetProperty("formatVersion").GetInt32() != 1 || Get(json, "game") != "minecraft")
                throw new IOException("不支持的 Modrinth 整合包格式版本。");
            return new(name, "Modrinth", ParseRequirements(json.GetProperty("dependencies")), archive.Entries.Count);
        }

        if (json.TryGetProperty("formatVersion", out var format) && format.GetInt32() != 1)
            throw new IOException("不支持的 MultiMC 整合包格式版本。");
        return new(name, "MultiMC / Prism", Components(json), archive.Entries.Count);
    }

    /// <summary>
    /// 解析Requirements。
    /// </summary>
    public static PackRequirements ParseRequirements(JsonElement dependencies)
    {
        var mc = Get(dependencies, "minecraft");
        ValidateVersion(mc);
        var loaders = dependencies.EnumerateObject().Where(p => p.Name != "minecraft").ToList();
        if (loaders.Count > 1)
            throw new IOException("整合包声明了多个加载器，暂不支持。");
        if (loaders.Count == 0)
            return new(mc, "vanilla", "");
        var selected = loaders[0];
        var version = selected.Value.GetString()!;
        ValidateVersion(version);
        var loader = selected.Name switch
        {
            "fabric-loader" => "fabric",
            "quilt-loader" => "quilt",
            "forge" => "forge",
            "neoforge" => "neoforge",
            _ => throw new IOException("不支持的整合包依赖：" + selected.Name)
        };
        return new(mc, loader, version);
    }

    /// <summary>
    /// 执行 下载。
    /// </summary>
    public async Task Download(string url, string target, string sha512, Action<string>? progress = null)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host is not ("cdn.modrinth.com" or "github.com" or "raw.githubusercontent.com" or "gitlab.com"))
            throw new IOException("整合包下载地址不在支持的 HTTPS 来源中。");
        if (sha512.Length != 128 || sha512.Any(c => !Uri.IsHexDigit(c)))
            throw new IOException("整合包缺少有效 SHA-512。");
        var cached = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BlockLaunch", "downloads", sha512.ToLowerInvariant());
        await ReliableDownloader.Download(http, uri, cached, sha512, progress: progress, token: token);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);
        File.Copy(cached, target, true);
    }

    /// <summary>
    /// 导入。
    /// </summary>
    public async Task<InstalledPack> Import(string archivePath, string root, Func<PackRequirements, Task<string>> installGame, bool optional, Action<string>? progress = null, string? instanceId = null, Action<double>? overallProgress = null)
    {
        var alias = instanceId ?? "pack-" + Guid.NewGuid().ToString("N")[..12];
        if (!Regex.IsMatch(alias, "^pack-[a-f0-9]{12}$"))
            throw new IOException("整合包实例标识无效。");
        var committedProfile = Path.Combine(root, "versions", alias, alias + ".json");
        if (instanceId != null && File.Exists(committedProfile))
        {
            var existing = AtomicFile.ReadJson<InstalledPack>(Path.Combine(root, "instances", alias, ".blocklaunch-pack.json"));
            if (existing.InstanceId != alias)
                throw new IOException("整合包实例身份不匹配。");
            return existing;
        }

        var stage = Path.Combine(root, ".blocklaunch-staging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var instanceStage = Path.Combine(stage, "instance");
        Directory.CreateDirectory(instanceStage);
        var stageLease = new FileStream(Path.Combine(stage, "operation.lock"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        AtomicFile.WriteJson(Path.Combine(stage, "pack-operation.json"), new PackCommitRecord(alias, "Preparing"));
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            if (archive.Entries.Count > 100000 || archive.Entries.Sum(e => e.Length) > 8_000_000_000L)
                throw new IOException("整合包展开后过大。");
            var markers = archive.Entries.Where(e => e.Name is "modrinth.index.json" or "mmc-pack.json").ToList();
            var marker = markers.OrderBy(e => e.FullName.Count(c => c == '/')).FirstOrDefault() ?? throw new IOException("当前支持 Modrinth .mrpack 和 MultiMC / Prism ZIP。CurseForge / HMCL 专用包请先导出为 .mrpack 或 MultiMC 包。");
            var prefix = marker.FullName[..^marker.Name.Length];
            using var document = await JsonDocument.ParseAsync(marker.Open());
            var json = document.RootElement;
            string name;
            PackRequirements requirements;
            if (marker.Name == "modrinth.index.json")
            {
                if (json.GetProperty("formatVersion").GetInt32() != 1 || Get(json, "game") != "minecraft")
                    throw new IOException("不支持的 Modrinth 整合包格式版本。");
                name = Get(json, "name", Path.GetFileNameWithoutExtension(archivePath));
                requirements = ParseRequirements(json.GetProperty("dependencies"));
                var files = json.GetProperty("files").EnumerateArray().ToList();
                if (files.Count > 30000)
                    throw new IOException("整合包文件数量过多。");
                var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var downloads = new List<(JsonElement File, string Relative, string Destination)>();
                foreach (var file in files)
                {
                    token.ThrowIfCancellationRequested();
                    var relative = Get(file, "path");
                    var dest = SafePath(instanceStage, relative);
                    if (!paths.Add(dest))
                        throw new IOException("整合包清单含有重复路径。");
                    if (file.TryGetProperty("env", out var env))
                    {
                        var side = Get(env, "client", "required");
                        if (side == "unsupported" || side == "optional" && !optional)
                            continue;
                        if (side is not ("required" or "optional"))
                            throw new IOException("未知的客户端文件标记。");
                    }

                    var hash = Get(file.GetProperty("hashes"), "sha512");
                    if (hash.Length != 128 || hash.Any(c => !Uri.IsHexDigit(c)))
                        throw new IOException("整合包缺少有效 SHA-512：" + relative);
                    downloads.Add((file, relative, dest));
                }

                var completed = 0;
                // Group identical digests so two workers never append to the same durable cache file.
                var groups = downloads.GroupBy(d => Get(d.File.GetProperty("hashes"), "sha512"), StringComparer.OrdinalIgnoreCase);
                await Parallel.ForEachAsync(groups, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = token }, async (group, ct) =>
                {
                    foreach (var item in group)
                    {
                        ct.ThrowIfCancellationRequested();
                        Exception? last = null;
                        var success = false;
                        foreach (var download in item.File.GetProperty("downloads").EnumerateArray())
                        {
                            try
                            {
                                await Download(download.GetString()!, item.Destination, group.Key, progress);
                                success = true;
                                break;
                            }
                            catch (Exception ex) when (!token.IsCancellationRequested && (ex is HttpRequestException or IOException or TaskCanceledException))
                            {
                                last = ex;
                            }
                        }

                        if (!success)
                            throw new IOException("无法下载或校验：" + item.Relative, last);
                        var count = Interlocked.Increment(ref completed);
                        overallProgress?.Invoke(InstallationStages.PackFiles * count / downloads.Count);
                        progress?.Invoke($"整合包文件 {count}/{downloads.Count} · {Path.GetFileName(item.Relative)}");
                    }
                });
                await ExtractLayer(archive, prefix + "overrides/", instanceStage, token);
                await ExtractLayer(archive, prefix + "client-overrides/", instanceStage, token);
            }
            else
            {
                if (json.TryGetProperty("formatVersion", out var format) && format.GetInt32() != 1)
                    throw new IOException("不支持的 MultiMC 整合包格式版本。");
                name = Path.GetFileNameWithoutExtension(archivePath);
                var exported = archive.GetEntry(prefix + "blocklaunch-export.json");
                if (exported != null)
                {
                    using var metadata = await JsonDocument.ParseAsync(exported.Open(), cancellationToken: token);
                    name = Get(metadata.RootElement, "name", name);
                }

                requirements = Components(json);
                await ExtractLayer(archive, prefix + "minecraft/", instanceStage, token);
                await ExtractLayer(archive, prefix + ".minecraft/", instanceStage, token);
            }
            // Never allow pack contents to inject launcher-owned state.
            if (File.Exists(Path.Combine(instanceStage, ".blocklaunch-content.json")))
                throw new IOException("整合包包含保留的启动器配置文件。");
            AtomicFile.WriteJson(Path.Combine(instanceStage, ".blocklaunch-pack-files.json"), await PackUpgrade.Snapshot(instanceStage, token));
            progress?.Invoke($"安装 Minecraft {requirements.Minecraft} / {requirements.Loader} {requirements.LoaderVersion}");
            overallProgress?.Invoke(InstallationStages.PackFiles);
            var launchVersion = await installGame(requirements);
            overallProgress?.Invoke(InstallationStages.BeforeCommit);
            ContentService.SafeFile(launchVersion);
            token.ThrowIfCancellationRequested();
            var record = new InstalledPack(name, requirements.Minecraft, requirements.Loader, requirements.LoaderVersion, launchVersion, alias);
            await File.WriteAllTextAsync(Path.Combine(instanceStage, ".blocklaunch-pack.json"), JsonSerializer.Serialize(record));
            var instance = Path.Combine(root, "instances", alias);
            Directory.CreateDirectory(Path.GetDirectoryName(instance)!);
            using var commit = OperationExecution.BeginCommit();
            var versionDir = Path.Combine(root, "versions", alias);
            Directory.CreateDirectory(versionDir);
            try
            {
                // Profile is the commit marker: incomplete instances do not enter the version list.
                Directory.Move(instanceStage, instance);
                await File.WriteAllTextAsync(Path.Combine(stage, "profile.json"), JsonSerializer.Serialize(new
                {
                    id = alias,
                    inheritsFrom = launchVersion,
                    jar = requirements.Minecraft,
                    type = "release"
                }));
                File.Move(Path.Combine(stage, "profile.json"), Path.Combine(versionDir, alias + ".json"));
            }
            catch
            {
                if (Directory.Exists(instance))
                    Directory.Move(instance, instanceStage);
                throw;
            }

            return record;
        }
        finally
        {
            stageLease.Dispose();
            if (Directory.Exists(stage))
                Directory.Delete(stage, true);
        }
    }

    static async Task ExtractLayer(ZipArchive archive, string prefix, string destination, CancellationToken token = default)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith(prefix, StringComparison.Ordinal)))
        {
            token.ThrowIfCancellationRequested();
            if (entry.FullName.EndsWith('/'))
                continue;
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                throw new IOException("整合包包含符号链接。");
            var relative = entry.FullName[prefix.Length..];
            if (relative.Replace('\\', '/').Split('/')[0].StartsWith(".blocklaunch", StringComparison.OrdinalIgnoreCase))
                throw new IOException("整合包不能覆盖启动器实例或事务配置。");
            var path = SafePath(destination, relative);
            if (!paths.Add(path))
                throw new IOException("整合包覆写目录存在重复路径。");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var input = entry.Open();
            await using var output = File.Create(path);
            await input.CopyToAsync(output, token);
        }
    }
}
