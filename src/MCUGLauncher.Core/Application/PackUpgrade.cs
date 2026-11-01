using System.Security.Cryptography;

namespace BlockLaunch;

/// <summary>
/// 表示 PackUpgrade文件 数据。
/// </summary>
public record PackUpgradeFile(string Relative, string Reason, bool Conflict, bool KeepOld)
{
    public string Label => Relative + " · " + Reason;
}

/// <summary>
/// 提供 PackUpgradePreview 相关功能。
/// </summary>
public sealed class PackUpgradePreview : IDisposable
{
    /// <summary>
    /// 获取或设置 根目录。
    /// </summary>
    public string Root { get; init; } = "";
    /// <summary>
    /// 获取或设置 Workspace。
    /// </summary>
    public string Workspace { get; init; } = "";
    /// <summary>
    /// 获取或设置 Source。
    /// </summary>
    public string Source { get; init; } = "";
    /// <summary>
    /// 获取或设置 OriginalKey。
    /// </summary>
    public string OriginalKey { get; init; } = "";
    /// <summary>
    /// 获取或设置 Pack。
    /// </summary>
    public InstalledPack Pack { get; init; } = null!;
    /// <summary>
    /// 获取或设置 OriginalFiles。
    /// </summary>
    public Dictionary<string, string> OriginalFiles { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// 获取或设置 Files。
    /// </summary>
    public List<PackUpgradeFile> Files { get; init; } = [];
    public bool HasBaseline
    {
        get;
        init;
    }

    /// <summary>
    /// 执行 Dispose。
    /// </summary>
    public void Dispose()
    {
        var parent = Path.GetFullPath(Path.Combine(Root, ".blocklaunch-upgrade-staging")) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(Workspace);
        if (!target.StartsWith(parent, StringComparison.OrdinalIgnoreCase))
            throw new IOException("升级暂存路径无效。");
        if (Directory.Exists(target))
            Directory.Delete(target, true);
    }
}

/// <summary>
/// 提供 PackUpgrade 相关功能。
/// </summary>
public static class PackUpgrade
{
    static readonly string[] Folders = ["mods", "config", "defaultconfigs", "resourcepacks", "shaderpacks", "kubejs", "scripts", "saves", "screenshots", "datapacks"];
    static readonly string[] Files = ["options.txt", "optionsof.txt", "optionsshaders.txt", "servers.dat", "servers.dat_old"];

    static void NoLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("升级不支持链接目录或文件：" + path);
    }

    sealed class WorldLease : IDisposable
    {
        readonly List<FileStream> locks = [];

        public WorldLease(string source)
        {
            try
            {
                var saves = Path.Combine(source, "saves");
                if (!Directory.Exists(saves))
                    return;
                NoLink(saves);
                foreach (var world in Directory.GetDirectories(saves))
                {
                    NoLink(world);
                    if (!File.Exists(Path.Combine(world, "level.dat")))
                        continue;
                    var path = Path.Combine(world, "session.lock");
                    if (File.Exists(path))
                        NoLink(path);
                    locks.Add(new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
                }
            }
            catch
            {
                Dispose();
                throw new IOException("源存档正在使用或无法锁定，请退出游戏后再升级。");
            }
        }

        /// <summary>
        /// 执行 Dispose。
        /// </summary>
        public void Dispose()
        {
            foreach (var file in locks)
                file.Dispose();
        }
    }

    static IEnumerable<string> Enumerate(string directory, CancellationToken token)
    {
        NoLink(directory);
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            token.ThrowIfCancellationRequested();
            NoLink(path);
            if (Directory.Exists(path))
            {
                foreach (var file in Enumerate(path, token))
                    yield return file;
            }
            else if (!path.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
                yield return path;
        }
    }

    /// <summary>
    /// 执行 Snapshot。
    /// </summary>
    public static async Task<Dictionary<string, string>> Snapshot(string directory, CancellationToken token)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(directory))
            return result;
        NoLink(directory);
        long total = 0;
        var paths = Folders.Where(f => Directory.Exists(Path.Combine(directory, f))).SelectMany(f => Enumerate(Path.Combine(directory, f), token)).Concat(Files.Select(f => Path.Combine(directory, f)).Where(File.Exists));
        foreach (var path in paths)
        {
            token.ThrowIfCancellationRequested();
            NoLink(path);
            total = checked(total + new FileInfo(path).Length);
            if (total > 20L * 1024 * 1024 * 1024 || result.Count >= 100000)
                throw new IOException("升级迁移最多支持 20 GiB / 10 万文件。");
            await using var input = File.OpenRead(path);
            result.Add(Path.GetRelativePath(directory, path).Replace('\\', '/'), Convert.ToHexString(await SHA256.HashDataAsync(input, token)));
        }

        return result;
    }

    /// <summary>
    /// 准备。
    /// </summary>
    public static async Task<PackUpgradePreview> Prepare(HttpClient http, string root, string originalKey, string source, string archive, Func<PackRequirements, Task<string>> installGame, bool optional, Action<string>? progress, CancellationToken token)
    {
        var catalog = new InstanceCatalog(root);
        var descriptor = catalog.GetOrCreate(originalKey);
        if (!File.Exists(Path.Combine(catalog.MetadataDirectory(originalKey), ".blocklaunch-pack.json")))
            throw new IOException("请选择已导入的整合包实例。");
        if (Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar).Equals(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new IOException("请先将整合包设为独立实例后升级。");
        var workspace = Path.Combine(Path.GetFullPath(root), ".blocklaunch-upgrade-staging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            progress?.Invoke("读取旧实例与自定义文件…");
            using var sourceLock = ContentService.LockInstance(source);
            using var worlds = new WorldLease(source);
            var oldFiles = await Snapshot(source, token);
            var baselineFile = Path.Combine(catalog.MetadataDirectory(originalKey), ".blocklaunch-pack-files.json");
            var baseline = File.Exists(baselineFile) ? new Dictionary<string, string>(AtomicFile.ReadJson<Dictionary<string, string>>(baselineFile), StringComparer.OrdinalIgnoreCase) : new(StringComparer.OrdinalIgnoreCase);
            var pack = await new PackService(http, token).Import(archive, workspace, installGame, optional, progress);
            var candidate = Path.Combine(workspace, "instances", pack.InstanceId);
            var fresh = await Snapshot(candidate, token);
            var choices = new List<PackUpgradeFile>();
            foreach (var (relative, hash) in oldFiles)
            {
                var personal = relative.StartsWith("saves/", StringComparison.OrdinalIgnoreCase) || relative.StartsWith("screenshots/", StringComparison.OrdinalIgnoreCase) || Files.Contains(relative, StringComparer.OrdinalIgnoreCase);
                baseline.TryGetValue(relative, out var original);
                fresh.TryGetValue(relative, out var updated);
                if (personal)
                {
                    choices.Add(new(relative, "保留玩家数据", false, true));
                    continue;
                }

                if (updated == hash)
                    continue;
                if (original == hash)
                    continue;
                // Unmodified old package file: use the new package or remove obsolete file.
                var conflict = updated != null && updated != original || !File.Exists(baselineFile) && relative.StartsWith("mods/", StringComparison.OrdinalIgnoreCase);
                choices.Add(new(relative, conflict ? "与新包冲突或旧包来源未知，请确认是否保留" : "保留自定义文件", conflict, true));
            }

            if (!Same(oldFiles, await Snapshot(source, token)))
                throw new IOException("准备升级期间旧实例已改变，请重新预览。");
            return new()
            {
                Root = Path.GetFullPath(root),
                Workspace = workspace,
                Source = Path.GetFullPath(source),
                OriginalKey = originalKey,
                Pack = pack,
                OriginalFiles = oldFiles,
                Files = choices,
                HasBaseline = File.Exists(baselineFile)
            };
        }
        catch
        {
            if (Directory.Exists(workspace))
                Directory.Delete(workspace, true);
            throw;
        }
    }

    static bool Same(Dictionary<string, string> a, Dictionary<string, string> b) => a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var hash) && hash == p.Value);

    /// <summary>
    /// 执行 Commit。
    /// </summary>
    public static async Task<string> Commit(PackUpgradePreview preview, IEnumerable<string> keepOld, LaunchSettings settings, Action<string>? progress, CancellationToken token)
    {
        using var lease = ContentService.LockInstance(preview.Source);
        using var worlds = new WorldLease(preview.Source);
        if (!Same(preview.OriginalFiles, await Snapshot(preview.Source, token)))
            throw new IOException("旧实例在预览后已改变，请重新准备升级。");
        var selected = keepOld.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (selected.Any(p => !preview.Files.Any(f => f.Relative.Equals(p, StringComparison.OrdinalIgnoreCase))))
            throw new IOException("迁移清单不在预览范围内。");
        var candidate = Path.Combine(preview.Workspace, "instances", preview.Pack.InstanceId);
        foreach (var relative in selected)
        {
            token.ThrowIfCancellationRequested();
            var source = PackService.SafePath(preview.Source, relative);
            var target = PackService.SafePath(candidate, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            progress?.Invoke("迁移：" + relative);
            await using (var input = File.OpenRead(source))
            await using (var output = File.Create(target))
                await input.CopyToAsync(output, token);
        }

        if (!Same(preview.OriginalFiles, await Snapshot(preview.Source, token)))
            throw new IOException("迁移期间旧实例已改变；升级未提交。");
        var managed = new List<ManagedContent>();
        foreach (var record in ContentService.ReadManaged(preview.Source))
        {
            var path = PackService.SafePath(candidate, record.RelativePath);
            var actual = File.Exists(path) ? path : File.Exists(path + ".disabled") ? path + ".disabled" : null;
            if (actual == null)
                continue;
            await using var input = File.OpenRead(actual);
            if (Convert.ToHexString(await SHA512.HashDataAsync(input, token)).Equals(record.Sha512, StringComparison.OrdinalIgnoreCase))
                managed.Add(record);
        }

        if (managed.Count > 0)
            await File.WriteAllTextAsync(Path.Combine(candidate, ".blocklaunch-content.json"), System.Text.Json.JsonSerializer.Serialize(managed, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower }), token);
        var catalog = new InstanceCatalog(preview.Root);
        var targetDirectory = catalog.MetadataDirectory(preview.Pack.InstanceId);
        var effective = System.Text.Json.JsonSerializer.Deserialize<LaunchSettings>(System.Text.Json.JsonSerializer.Serialize(settings))!;
        effective.Isolation = "独立实例";
        AtomicFile.WriteJson(Path.Combine(candidate, ".blocklaunch-instance.json"), new InstanceDescriptor { Key = preview.Pack.InstanceId, DisplayName = preview.Pack.Name + " · 升级", LaunchProfileId = preview.Pack.LaunchVersion, Launch = effective, Category = "整合包" });
        AtomicFile.WriteJson(Path.Combine(candidate, ".blocklaunch-upgrade.json"), new Dictionary<string, string> { ["OriginalKey"] = preview.OriginalKey });
        token.ThrowIfCancellationRequested();
        using var commit = OperationExecution.BeginCommit();
        var profile = Path.Combine(preview.Root, "versions", preview.Pack.InstanceId);
        if (Directory.Exists(targetDirectory) || Directory.Exists(profile))
            throw new IOException("升级实例标识冲突。");
        var commitStage = Path.Combine(preview.Root, ".blocklaunch-staging", "upgrade-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(commitStage);
        using var stageLease = new FileStream(Path.Combine(commitStage, "operation.lock"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        var commitRecord = new PackCommitRecord(preview.Pack.InstanceId, "Preparing");
        AtomicFile.WriteJson(Path.Combine(commitStage, "pack-operation.json"), commitRecord);
        Directory.CreateDirectory(Path.GetDirectoryName(targetDirectory)!);
        Directory.Move(candidate, targetDirectory);
        try
        {
            Directory.CreateDirectory(profile);
            AtomicFile.WriteJson(Path.Combine(profile, preview.Pack.InstanceId + ".json"), new
            {
                id = preview.Pack.InstanceId,
                inheritsFrom = preview.Pack.LaunchVersion,
                jar = preview.Pack.Minecraft,
                type = "release"
            });
            AtomicFile.WriteJson(Path.Combine(commitStage, "pack-operation.json"), commitRecord with
            {
                State = "Committed"
            });
        }
        catch
        {
            Directory.Move(targetDirectory, candidate);
            if (Directory.Exists(profile) && !Directory.EnumerateFileSystemEntries(profile).Any())
                Directory.Delete(profile);
            throw;
        }

        return preview.Pack.InstanceId;
    }
}
