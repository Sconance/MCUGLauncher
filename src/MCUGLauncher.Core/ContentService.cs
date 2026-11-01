using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlockLaunch;

public record ContentProject(string Id, string Slug, string Title, string Description, string Author, long Downloads)
{
    public string IconUrl { get; init; } = "";
    public string Tags { get; init; } = "";
    public bool IsFavorite
    {
        get;
        init;
    }
    public string CustomTags { get; init; } = "";

    public string DisplayTitle => (IsFavorite ? "★ " : "") + Title;

    public string Initial => Title.Length == 0 ? "M" : Title[..1].ToUpperInvariant();

    public string Meta => $"{Author}  ·  {Downloads:N0} 次下载 · Modrinth" + (Tags.Length > 0 ? "\n" + Tags : "") + (CustomTags.Length > 0 ? "\n收藏标签：" + CustomTags : "");
}

public record SearchPage(List<ContentProject> Items, int Total);

public class ContentFile
{
    public string Filename { get; set; } = "";
    public string Url { get; set; } = "";
    public long Size
    {
        get;
        set;
    }
    public bool Primary
    {
        get;
        set;
    }
    public Dictionary<string, string> Hashes { get; set; } = new();
}

public class ContentDependency
{
    public string? ProjectId
    {
        get;
        set;
    }
    public string? VersionId
    {
        get;
        set;
    }
    public string DependencyType { get; set; } = "";
}

public class ContentVersion
{
    public string Id { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string Name { get; set; } = "";
    public string VersionNumber { get; set; } = "";
    public string VersionType { get; set; } = "";
    public DateTimeOffset DatePublished
    {
        get;
        set;
    }
    public string[] GameVersions { get; set; } = [];
    public string[] Loaders { get; set; } = [];
    public List<ContentFile> Files { get; set; } = [];
    public List<ContentDependency> Dependencies { get; set; } = [];

    public string Label => $"{VersionNumber} · {VersionType} · {DatePublished:yyyy-MM-dd}";
}

public class ManagedContent
{
    public string ProjectId { get; set; } = "";
    public string VersionId { get; set; } = "";
    public string Name { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string Sha512 { get; set; } = "";
    public List<ContentDependency> Dependencies { get; set; } = [];
}

public record LocalContent(string Path, string Name, string Detail, string State, ManagedContent? Managed);

public record InstallPlan(List<ContentVersion> Versions, Dictionary<string, string> Types);

public interface IResourceProvider
{
    Task<SearchPage> Search(string query, string game, string loader, string type, int offset = 0);
    Task<List<ContentVersion>> Versions(string project, string game, string loader, string type);
    Task<InstallPlan> Resolve(ContentVersion version, string game, string loader);
}

public sealed class ContentService : IResourceProvider
{
    readonly HttpClient http;
    readonly CancellationToken token;
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public ContentService(HttpClient client, CancellationToken token = default)
    {
        http = client;
        this.token = token;
    }

    static string Esc(string s) => Uri.EscapeDataString(s);
    async Task<T> Get<T>(string route) => await http.GetFromJsonAsync<T>("https://api.modrinth.com/v2/" + route, Json, token) ?? throw new IOException("Modrinth 返回了空响应。");

    public Task<SearchPage> Search(string query, string game, string loader, string type, int offset = 0) => Search(query, game, loader, type, offset, "relevance");

    public async Task<SearchPage> Search(string query, string game, string loader, string type, int offset, string sort)
    {
        if (sort is not ("relevance" or "downloads" or "updated"))
            throw new ArgumentException("不支持的资源排序方式。", nameof(sort));
        var filters = new List<string[]>
        {
            new[] {
                "project_type:" + (type == "datapack" ? "mod" : type)
            }
        };
        if (type == "datapack")
            filters.Add(["categories:datapack"]);
        if (game.Length > 0)
            filters.Add(["versions:" + game]);
        if (type == "mod" && loader.Length > 0 && loader != "vanilla")
            filters.Add(["categories:" + loader]);
        using var doc = await Get<JsonDocument>($"search?query={Esc(query)}&facets={Esc(JsonSerializer.Serialize(filters))}&index={Esc(sort)}&offset={offset}&limit=15");
        var hits = doc.RootElement.GetProperty("hits").EnumerateArray().Select(e => new ContentProject(e.GetProperty("project_id").GetString()!, e.GetProperty("slug").GetString() ?? "", e.GetProperty("title").GetString()!, e.GetProperty("description").GetString() ?? "", e.GetProperty("author").GetString() ?? "", e.GetProperty("downloads").GetInt64()) { IconUrl = e.TryGetProperty("icon_url", out var icon) && Uri.TryCreate(icon.GetString(), UriKind.Absolute, out var uri) && uri.Host == "cdn.modrinth.com" && uri.Scheme == "https" ? uri.AbsoluteUri : "", Tags = e.TryGetProperty("categories", out var categories) ? string.Join(" · ", categories.EnumerateArray().Take(5).Select(c => c.GetString())) : "" }).ToList();
        return new(hits, doc.RootElement.GetProperty("total_hits").GetInt32());
    }

    public async Task<List<ContentVersion>> Versions(string project, string game, string loader, string type)
    {
        var loaderQuery = type == "datapack" || type == "mod" && loader.Length > 0 ? "&loaders=" + Esc(JsonSerializer.Serialize(new[] { type == "datapack" ? "datapack" : loader })) : "";
        var gameQuery = game.Length > 0 ? "&game_versions=" + Esc(JsonSerializer.Serialize(new[] { game })) : "";
        var items = await Get<List<ContentVersion>>($"project/{Esc(project)}/version?include_changelog=false{gameQuery}{loaderQuery}");
        return items.Where(v => Compatible(v, game, loader, type)).OrderByDescending(v => v.VersionType == "release").ThenByDescending(v => v.DatePublished).ToList();
    }

    static bool Compatible(ContentVersion v, string game, string loader, string type) => (game.Length == 0 || v.GameVersions.Contains(game)) && (type is not ("mod" or "datapack") || loader.Length == 0 || v.Loaders.Contains(type == "datapack" ? "datapack" : loader));

    public async Task<InstallPlan> Resolve(ContentVersion root, string game, string loader)
    {
        var versions = new Dictionary<string, ContentVersion>();
        var types = new Dictionary<string, string>();
        var active = new HashSet<string>();
        async Task Visit(ContentVersion v)
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(v.ProjectId) || string.IsNullOrWhiteSpace(v.Id))
                throw new IOException("依赖版本缺少项目或版本标识。");
            if (versions.TryGetValue(v.ProjectId, out var chosen))
            {
                if (chosen.Id != v.Id)
                    throw new IOException($"必需依赖版本冲突：{v.ProjectId}。请更换项目版本。");
                return;
            }

            if (versions.Count >= 128)
                throw new IOException("依赖数量过多，安装已停止。");
            using var project = await Get<JsonDocument>("project/" + Esc(v.ProjectId));
            var type = project.RootElement.GetProperty("project_type").GetString()!;
            if (type == "mod" && loader == "datapack" && v.Loaders.Contains("datapack"))
                type = "datapack";
            if (type is not ("mod" or "resourcepack" or "shader" or "datapack"))
                throw new IOException("不支持此依赖类型：" + type);
            if (type != "datapack" && project.RootElement.TryGetProperty("client_side", out var side) && side.GetString() == "unsupported")
                throw new IOException("项目不支持客户端：" + v.Name);
            if (!Compatible(v, game, loader, type))
                throw new IOException($"{v.Name} 与 {game} / {loader} 不兼容。");
            if (type == "mod" && loader == "vanilla")
                throw new IOException("请先安装并选择 Fabric / Quilt / Forge / NeoForge 版本，再安装模组。");
            versions.Add(v.ProjectId, v);
            types.Add(v.ProjectId, type);
            active.Add(v.Id);
            foreach (var dep in v.Dependencies.Where(d => d.DependencyType == "required").OrderBy(d => string.IsNullOrEmpty(d.VersionId)))
            {
                ContentVersion next;
                if (!string.IsNullOrEmpty(dep.VersionId))
                {
                    next = await Get<ContentVersion>("version/" + Esc(dep.VersionId));
                    if (next.Id != dep.VersionId || !string.IsNullOrEmpty(dep.ProjectId) && next.ProjectId != dep.ProjectId)
                        throw new IOException("必需依赖的项目或版本与响应不一致，已停止安装。");
                }
                else if (!string.IsNullOrEmpty(dep.ProjectId))
                {
                    if (versions.TryGetValue(dep.ProjectId, out var selected))
                    {
                        await Visit(selected);
                        continue;
                    }

                    using var dp = await Get<JsonDocument>("project/" + Esc(dep.ProjectId));
                    var dt = dp.RootElement.GetProperty("project_type").GetString()!;
                    next = (await Versions(dep.ProjectId, game, loader, dt)).FirstOrDefault() ?? throw new IOException("找不到兼容的必需依赖：" + dep.ProjectId);
                    if (next.ProjectId != dep.ProjectId)
                        throw new IOException("必需依赖的项目与响应不一致，已停止安装。");
                }
                else
                    throw new IOException("必需依赖缺少可下载的项目或版本信息，不能自动安装。");
                if (active.Contains(next.Id))
                    continue;
                await Visit(next);
            }

            active.Remove(v.Id);
        }

        await Visit(root);
        CheckDependencies(versions.Values.Select(v => new ManagedContent { ProjectId = v.ProjectId, VersionId = v.Id, Name = v.Name, Dependencies = v.Dependencies }).ToList());
        return new(versions.Values.ToList(), types);
    }

    static void CheckDependencies(List<ManagedContent> records)
    {
        foreach (var record in records)
            foreach (var dep in record.Dependencies)
            {
                var projectId = string.IsNullOrWhiteSpace(dep.ProjectId) ? null : dep.ProjectId;
                var versionId = string.IsNullOrWhiteSpace(dep.VersionId) ? null : dep.VersionId;
                var matches = records.Where(r => projectId != null ? r.ProjectId == projectId : versionId != null && r.VersionId == versionId).ToList();
                if (dep.DependencyType == "incompatible" && matches.Any(r => versionId == null || r.VersionId == versionId))
                    throw new IOException("已启用内容互斥：" + record.Name + " 与 " + matches[0].Name + "。请先禁用其中一个。");
                if (dep.DependencyType == "required" && (matches.Count == 0 || versionId != null && matches.All(r => r.VersionId != versionId)))
                    throw new IOException("必需依赖未启用或版本不匹配：" + record.Name + " 需要 " + (projectId ?? versionId ?? "未知项目") + "。请先安装或启用兼容依赖。");
            }
    }

    static void CheckActiveDependencies(string instance, List<ManagedContent> records, IReadOnlyDictionary<string, bool>? states = null)
    {
        var active = records.Where(r => states != null && states.TryGetValue(r.ProjectId, out var enabled) ? enabled : File.Exists(ContentPath(instance, r.RelativePath))).ToList();
        CheckDependencies(active);
    }

    public static void ValidateUpdateDependencies(string instance, InstallPlan plan)
    {
        var current = ReadManaged(instance);
        var projects = plan.Versions.Select(v => v.ProjectId).ToHashSet();
        var states = new Dictionary<string, bool>();
        var additions = plan.Versions.Select(v =>
        {
            var old = current.FirstOrDefault(m => m.ProjectId == v.ProjectId);
            states[v.ProjectId] = old == null || !File.Exists(ContentPath(instance, old.RelativePath) + ".disabled");
            return new ManagedContent
            {
                ProjectId = v.ProjectId,
                VersionId = v.Id,
                Name = v.Name,
                Dependencies = v.Dependencies
            };
        }).ToList();
        CheckActiveDependencies(instance, current.Where(m => !projects.Contains(m.ProjectId)).Concat(additions).ToList(), states);
    }

    public static string SafeFile(string file)
    {
        if (string.IsNullOrWhiteSpace(file) || file != System.IO.Path.GetFileName(file) || file.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || file.EndsWith('.') || file.EndsWith(' ') || file.Contains(':'))
            throw new IOException("无效的下载文件名。");
        return file;
    }

    static string Manifest(string instance) => System.IO.Path.Combine(instance, ".blocklaunch-content.json");

    public static FileStream LockInstance(string instance)
    {
        var folder = System.IO.Path.Combine(instance, ".blocklaunch");
        Directory.CreateDirectory(folder);
        try
        {
            return new FileStream(System.IO.Path.Combine(folder, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException ex)
        {
            throw new IOException("另一个内容任务正在修改此实例，请稍后重试。", ex);
        }
    }

    public static List<ManagedContent> ReadManaged(string instance) => File.Exists(Manifest(instance)) ? JsonSerializer.Deserialize<List<ManagedContent>>(File.ReadAllText(Manifest(instance)), Json) ?? [] : [];

    static string ContentPath(string instance, string relative)
    {
        var root = System.IO.Path.GetFullPath(instance) + System.IO.Path.DirectorySeparatorChar;
        var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relative));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !(relative.StartsWith("mods/") || relative.StartsWith("resourcepacks/") || relative.StartsWith("shaderpacks/") || relative.StartsWith("datapacks/")) || relative.Split('/').Length != 2)
            throw new IOException("内容记录包含无效路径。");
        SafeFile(System.IO.Path.GetFileName(path));
        return path;
    }

    static void WriteManaged(string instance, List<ManagedContent> records)
    {
        var file = Manifest(instance);
        var temp = file + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(records, Json));
        File.Move(temp, file, true);
    }

    static async Task<string> Hash(string path, CancellationToken cancellation = default)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA512.HashDataAsync(stream, cancellation)).ToLowerInvariant();
    }

    public async Task<int> Install(InstallPlan plan, string instance, Action<string>? progress = null, string? expectedFingerprint = null, Action<double>? overallProgress = null)
    {
        token.ThrowIfCancellationRequested();
        if (plan.Versions.Count == 0 || plan.Versions.Count > 128 || plan.Versions.Any(v => string.IsNullOrWhiteSpace(v.ProjectId) || string.IsNullOrWhiteSpace(v.Id)) || plan.Versions.Select(v => v.ProjectId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != plan.Versions.Count)
            throw new IOException("安装计划为空或包含无效、重复的项目。");
        foreach (var version in plan.Versions)
        {
            if (!plan.Types.TryGetValue(version.ProjectId, out var kind) || kind is not ("mod" or "shader" or "datapack" or "resourcepack"))
                throw new IOException("安装计划包含不支持的资源类型。");
            var file = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files.FirstOrDefault() ?? throw new IOException("版本没有下载文件。");
            SafeFile(file.Filename);
            if (!Uri.TryCreate(file.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "cdn.modrinth.com")
                throw new IOException("下载地址不是 Modrinth 官方 HTTPS CDN。");
            if (!file.Filename.EndsWith(kind == "mod" ? ".jar" : ".zip", StringComparison.OrdinalIgnoreCase) || file.Size < 0)
                throw new IOException("下载文件格式或大小无效：" + file.Filename);
            if (!file.Hashes.TryGetValue("sha512", out var digest) || digest.Length != 128 || digest.Any(c => !Uri.IsHexDigit(c)))
                throw new IOException("缺少有效 SHA-512，停止安装。");
        }

        Directory.CreateDirectory(instance);
        using var instanceLock = LockInstance(instance);
        if (expectedFingerprint != null && expectedFingerprint != await ContentUpdates.Fingerprint(instance, token))
            throw new IOException("内容自更新预览后已改变，请重新检查更新；未覆盖文件。");
        var managed = ReadManaged(instance);
        var replacing = plan.Versions.Select(v => v.ProjectId).ToHashSet();
        var files = plan.Versions.Select(v => (Version: v, File: v.Files.FirstOrDefault(f => f.Primary) ?? v.Files.FirstOrDefault() ?? throw new IOException("版本没有下载文件。"))).ToList();
        var added = files.Select(x => new ManagedContent
        {
            ProjectId = x.Version.ProjectId,
            VersionId = x.Version.Id,
            Name = x.Version.Name,
            RelativePath = (plan.Types[x.Version.ProjectId] switch
            {
                "mod" => "mods/",
                "shader" => "shaderpacks/",
                "datapack" => "datapacks/",
                _ => "resourcepacks/"
            }) + SafeFile(x.File.Filename),
            Sha512 = x.File.Hashes.GetValueOrDefault("sha512") ?? throw new IOException("缺少 SHA-512，停止安装。"),
            Dependencies = x.Version.Dependencies
        }).ToList();
        if (added.Select(a => a.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != added.Count)
            throw new IOException("下载计划存在文件名冲突。");
        var nextManaged = managed.Where(m => !replacing.Contains(m.ProjectId)).Concat(added).ToList();
        if (added.Any(a => a.Sha512.Length != 128 || a.Sha512.Any(c => !Uri.IsHexDigit(c))))
            throw new IOException("SHA-512 格式无效。");
        // Preserve each project's state even when its new release uses a different filename.
        var disabledProjects = new HashSet<string>();
        var originalFiles = new Dictionary<string, string?>();
        foreach (var old in managed.Where(m => replacing.Contains(m.ProjectId)))
        {
            token.ThrowIfCancellationRequested();
            var path = ContentPath(instance, old.RelativePath);
            if (File.Exists(path) && File.Exists(path + ".disabled"))
                throw new IOException("同一项目同时存在启用和禁用文件，请先处理：" + old.RelativePath);
            var actual = File.Exists(path) ? path : File.Exists(path + ".disabled") ? path + ".disabled" : null;
            originalFiles[old.ProjectId] = actual;
            if (actual == null)
                continue;
            if (!string.Equals(await Hash(actual, token), old.Sha512, StringComparison.OrdinalIgnoreCase))
                throw new IOException("已有文件被修改，未覆盖：" + old.RelativePath);
            if (actual.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                disabledProjects.Add(old.ProjectId);
        }

        CheckActiveDependencies(instance, nextManaged, added.ToDictionary(a => a.ProjectId, a => !disabledProjects.Contains(a.ProjectId)));
        foreach (var record in added)
        {
            var dest = ContentPath(instance, record.RelativePath);
            if (Directory.Exists(dest) || Directory.Exists(dest + ".disabled"))
                throw new IOException("目标位置被文件夹占用：" + record.RelativePath);
            var owner = managed.FirstOrDefault(m => m.RelativePath.Equals(record.RelativePath, StringComparison.OrdinalIgnoreCase));
            if ((File.Exists(dest) || File.Exists(dest + ".disabled")) && (owner == null || owner.ProjectId != record.ProjectId))
                throw new IOException("存在同名内容，请先在内容管理中处理：" + record.RelativePath);
        }

        var transaction = new ContentTransaction(instance);
        var stage = transaction.Stage;
        try
        {
            var completedFiles = 0;
            foreach (var x in files)
            {
                var record = added.Single(a => a.ProjectId == x.Version.ProjectId);
                if (!Uri.TryCreate(x.File.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "cdn.modrinth.com")
                    throw new IOException("下载地址不是 Modrinth 官方 HTTPS CDN。");
                var extension = plan.Types[record.ProjectId] == "mod" ? ".jar" : ".zip";
                if (!x.File.Filename.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("不支持的文件格式：" + x.File.Filename);
                progress?.Invoke("下载并校验 " + x.File.Filename);
                var temp = PackService.SafePath(stage, record.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(temp)!);
                token.ThrowIfCancellationRequested();
                var cache = Path.Combine(instance, ".blocklaunch", "downloads", record.Sha512.ToLowerInvariant());
                await ReliableDownloader.Download(http, uri, cache, record.Sha512, size: x.File.Size, progress: progress, token: token);
                File.Copy(cache, temp, true);
                overallProgress?.Invoke(95.0 * ++completedFiles / files.Count);
            }

            token.ThrowIfCancellationRequested();
            overallProgress?.Invoke(99);
            using var commit = OperationExecution.BeginCommit();
            if (expectedFingerprint != null && expectedFingerprint != await ContentUpdates.Fingerprint(instance, token))
                throw new IOException("下载期间内容已改变，请重新生成更新预览。");
            CheckActiveDependencies(instance, nextManaged, added.ToDictionary(a => a.ProjectId, a => !disabledProjects.Contains(a.ProjectId)));
            foreach (var old in managed.Where(m => replacing.Contains(m.ProjectId)))
            {
                var path = ContentPath(instance, old.RelativePath);
                var currentFile = File.Exists(path) ? path : File.Exists(path + ".disabled") ? path + ".disabled" : null;
                if (!string.Equals(currentFile, originalFiles[old.ProjectId], StringComparison.OrdinalIgnoreCase))
                    throw new IOException("下载期间内容文件或启用状态发生变化，请重新执行：" + old.RelativePath);
                if (File.Exists(path) && File.Exists(path + ".disabled"))
                    throw new IOException("下载期间内容启用状态发生变化，请重新执行：" + old.RelativePath);
                foreach (var actual in new[] {
                    path,
                    path + ".disabled"
                }.Where(File.Exists))
                {
                    if (!string.Equals(await Hash(actual, token), old.Sha512, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("已有文件被修改，未覆盖：" + old.RelativePath);
                    transaction.Backup(old.RelativePath + (actual.EndsWith(".disabled") ? ".disabled" : ""));
                }
            }

            foreach (var record in added)
            {
                transaction.Write(PackService.SafePath(stage, record.RelativePath), record.RelativePath + (disabledProjects.Contains(record.ProjectId) ? ".disabled" : ""));
            }

            var manifestStage = System.IO.Path.Combine(stage, "manifest.json");
            File.WriteAllText(manifestStage, JsonSerializer.Serialize(nextManaged, Json));
            transaction.Backup(".blocklaunch-content.json");
            transaction.Write(manifestStage, ".blocklaunch-content.json");
            transaction.Commit();
            return added.Count;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public static List<LocalContent> ListLocal(string instance)
    {
        var managed = ReadManaged(instance);
        var result = new List<LocalContent>();
        foreach (var folder in new[] {
            "mods",
            "resourcepacks",
            "shaderpacks",
            "datapacks"
        }

        )
        {
            var dir = System.IO.Path.Combine(instance, folder);
            if (!Directory.Exists(dir))
                continue;
            foreach (var path in Directory.GetFiles(dir).Where(f => f.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".zip.disabled", StringComparison.OrdinalIgnoreCase)))
            {
                var disabled = path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                var name = System.IO.Path.GetFileName(disabled ? path[..^9] : path);
                var record = managed.FirstOrDefault(m => m.RelativePath.Equals(folder + "/" + name, StringComparison.OrdinalIgnoreCase));
                var kind = folder switch
                {
                    "mods" => "模组",
                    "shaderpacks" => "光影包",
                    "datapacks" => "数据包",
                    _ => "资源包"
                };
                result.Add(new(path, record?.Name ?? name, $"{kind} · {(record == null ? "本地导入" : "Modrinth")} · {new FileInfo(path).Length / 1024.0:N0} KB", disabled ? "已禁用" : "已启用", record));
            }
        }

        return result.OrderBy(x => x.Name).ToList();
    }

    public async Task<int> ImportFiles(string instance, string child, IEnumerable<string> sources)
    {
        token.ThrowIfCancellationRequested();
        if (child is not ("mods" or "resourcepacks" or "shaderpacks" or "datapacks"))
            throw new IOException("导入目标无效。");
        Directory.CreateDirectory(instance);
        using var lease = LockInstance(instance);
        var transaction = new ContentTransaction(instance);
        var files = new List<(string Source, string Relative)>();
        try
        {
            foreach (var source in sources)
            {
                token.ThrowIfCancellationRequested();
                var name = SafeFile(Path.GetFileName(source));
                if (!name.EndsWith(child == "mods" ? ".jar" : ".zip", StringComparison.OrdinalIgnoreCase))
                    throw new IOException("导入文件格式不匹配。");
                var relative = child + "/" + name;
                var destination = ContentPath(instance, relative);
                if (File.Exists(destination) || File.Exists(destination + ".disabled") || files.Any(f => f.Relative.Equals(relative, StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (Directory.Exists(destination) || Directory.Exists(destination + ".disabled"))
                    throw new IOException("导入目标被文件夹占用：" + name);
                var staged = Path.Combine(transaction.Stage, name);
                await using (var input = File.OpenRead(source))
                await using (var output = File.Create(staged))
                    await input.CopyToAsync(output, token);
                files.Add((staged, relative));
            }

            token.ThrowIfCancellationRequested();
            using var commit = OperationExecution.BeginCommit();
            foreach (var item in files)
                transaction.Write(item.Source, item.Relative);
            transaction.Commit();
            return files.Count;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public static void Toggle(LocalContent content)
    {
        var instance = Directory.GetParent(System.IO.Path.GetDirectoryName(content.Path)!)!.FullName;
        using var instanceLock = LockInstance(instance);
        var target = content.Path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase) ? content.Path[..^9] : content.Path + ".disabled";
        if (File.Exists(target) || Directory.Exists(target))
            throw new IOException("目标已存在，请先处理同名启用/禁用文件。");
        var records = ReadManaged(instance);
        var record = records.FirstOrDefault(r => ContentPath(instance, r.RelativePath).Equals(content.Path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase) ? content.Path[..^9] : content.Path, StringComparison.OrdinalIgnoreCase));
        if (record != null)
            CheckActiveDependencies(instance, records, new Dictionary<string, bool> { [record.ProjectId] = content.Path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase) });
        File.Move(content.Path, target);
    }

    public static void MoveToBackup(string instance, LocalContent content)
    {
        using var instanceLock = LockInstance(instance);
        var counterpart = content.Path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase) ? content.Path[..^9] : content.Path + ".disabled";
        if (File.Exists(counterpart))
            throw new IOException("同一内容同时存在启用和禁用文件，请先处理重复文件再移除。");
        var records = ReadManaged(instance);
        var record = records.FirstOrDefault(r => ContentPath(instance, r.RelativePath).Equals(content.Path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase) ? content.Path[..^9] : content.Path, StringComparison.OrdinalIgnoreCase));
        var remaining = records.Where(r => r != record).ToList();
        CheckActiveDependencies(instance, remaining);
        var dir = System.IO.Path.Combine(instance, ".blocklaunch", "backups", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var backupFile = System.IO.Path.Combine(dir, System.IO.Path.GetFileName(content.Path));
        File.Move(content.Path, backupFile);
        try
        {
            WriteManaged(instance, remaining);
        }
        catch
        {
            File.Move(backupFile, content.Path);
            throw;
        }
    }
}
