using System.Security.Cryptography;
using System.Text;

namespace BlockLaunch;

/// <summary>
/// 表示 内容更新 数据。
/// </summary>
public record ContentUpdate(ManagedContent Current, ContentVersion? Latest, string Error)
{
    public string Label => Current.Name + " · " + (Latest == null ? Error : Current.VersionId + " → " + Latest.VersionNumber);
}

/// <summary>
/// 表示 内容Change 数据。
/// </summary>
public record ContentChange(string Project, string Before, string After, string Path, string? OldPath = null)
{
    public string Label => Project + " · " + Before + " → " + After + "\n" + (OldPath != null && OldPath != Path ? "移除 " + OldPath + "\n写入 " : "写入 ") + Path;
}

/// <summary>
/// 表示 内容更新Preview 数据。
/// </summary>
public record ContentUpdatePreview(InstallPlan Plan, string Fingerprint, List<ContentChange> Changes, long Bytes);

/// <summary>
/// 提供 内容Updates 相关功能。
/// </summary>
public static class ContentUpdates
{
    /// <summary>
    /// 执行 Kind。
    /// </summary>
    public static string Kind(ManagedContent item) => item.RelativePath.Split('/')[0] switch
    {
        "mods" => "mod",
        "shaderpacks" => "shader",
        "datapacks" => "datapack",
        _ => "resourcepack"
    };

    /// <summary>
    /// 检查。
    /// </summary>
    public static async Task<List<ContentUpdate>> Check(IResourceProvider provider, string instance, string game, string loader, CancellationToken token)
    {
        var result = new List<ContentUpdate>();
        foreach (var item in ContentService.ReadManaged(instance))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var versions = await provider.Versions(item.ProjectId, game, Kind(item) == "datapack" ? "datapack" : loader, Kind(item));
                var latest = versions.Where(v => v.VersionType == "release").OrderByDescending(v => v.DatePublished).FirstOrDefault();
                result.Add(new(item, latest?.Id == item.VersionId ? null : latest, latest == null ? "没有兼容正式版" : latest.Id == item.VersionId ? "已是最新正式版" : ""));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                result.Add(new(item, null, ex.Message));
            }
        }

        return result;
    }

    /// <summary>
    /// 执行 Fingerprint。
    /// </summary>
    public static async Task<string> Fingerprint(string instance, CancellationToken token)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var paths = ContentService.ListLocal(instance).Select(x => x.Path).ToList();
        var manifest = System.IO.Path.Combine(instance, ".blocklaunch-content.json");
        if (File.Exists(manifest))
            paths.Add(manifest);
        foreach (var path in paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            hash.AppendData(Encoding.UTF8.GetBytes(System.IO.Path.GetRelativePath(instance, path).ToLowerInvariant() + "\0"));
            await using var input = File.OpenRead(path);
            hash.AppendData(await SHA256.HashDataAsync(input, token));
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    /// <summary>
    /// 执行 Preview。
    /// </summary>
    public static async Task<ContentUpdatePreview> Preview(IResourceProvider provider, string instance, IEnumerable<ContentVersion> selected, string game, string loader, CancellationToken token)
    {
        var before = await Fingerprint(instance, token);
        var managed = ContentService.ReadManaged(instance);
        var versions = new Dictionary<string, ContentVersion>();
        var types = new Dictionary<string, string>();
        foreach (var root in selected)
        {
            token.ThrowIfCancellationRequested();
            var current = managed.FirstOrDefault(m => m.ProjectId == root.ProjectId);
            var rootLoader = current != null && Kind(current) == "datapack" ? "datapack" : loader;
            var plan = await provider.Resolve(root, game, rootLoader);
            foreach (var version in plan.Versions)
            {
                if (versions.TryGetValue(version.ProjectId, out var old) && old.Id != version.Id)
                    throw new IOException("所选更新的依赖版本冲突：" + version.Name + "。请分批选择兼容版本。");
                versions[version.ProjectId] = version;
                types[version.ProjectId] = plan.Types[version.ProjectId];
            }
        }

        if (versions.Count == 0)
            throw new IOException("请至少选择一项更新。");
        var changes = new List<ContentChange>();
        long bytes = 0;
        foreach (var version in versions.Values)
        {
            var file = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files.First();
            var old = managed.FirstOrDefault(m => m.ProjectId == version.ProjectId);
            var folder = types[version.ProjectId] switch
            {
                "mod" => "mods",
                "shader" => "shaderpacks",
                "datapack" => "datapacks",
                _ => "resourcepacks"
            };
            var suffix = old != null && File.Exists(System.IO.Path.Combine(instance, old.RelativePath) + ".disabled") ? ".disabled" : "";
            changes.Add(new(version.Name, old?.VersionId ?? "新增依赖", version.VersionNumber.Length > 0 ? version.VersionNumber : version.Id, folder + "/" + file.Filename + suffix, old == null ? null : old.RelativePath + suffix));
            bytes = checked(bytes + file.Size);
        }

        if (before != await Fingerprint(instance, token))
            throw new IOException("生成预览时本地内容已改变，请重新检查。");
        var frozen = new InstallPlan(versions.Values.ToList(), types);
        ContentService.ValidateUpdateDependencies(instance, frozen);
        return new(frozen, before, changes, bytes);
    }
}
