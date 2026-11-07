namespace BlockLaunch;

/// <summary>
/// 表示 ResourceBookmark 数据。
/// </summary>
public record ResourceBookmark(ContentProject Project, string Kind, string[] Labels, DateTimeOffset Added)
{
    /// <summary>
    /// 获取或设置 文件夹。
    /// </summary>
    public string Folder { get; init; } = "默认收藏夹";
}

/// <summary>
/// 提供 ResourceBookmarkDocument 相关功能。
/// </summary>
public class ResourceBookmarkDocument
{
    /// <summary>
    /// 获取或设置 Schema版本。
    /// </summary>
    public int SchemaVersion { get; set; } = 1;
    /// <summary>
    /// 获取或设置 项目。
    /// </summary>
    public List<ResourceBookmark> Items { get; set; } = [];
    /// <summary>
    /// 获取或设置 Folders。
    /// </summary>
    public List<string> Folders { get; set; } = ["默认收藏夹"];
}

/// <summary>
/// 提供 ResourceBookmarkStore 相关功能。
/// </summary>
public sealed class ResourceBookmarkStore(string directory)
{
    string FileName => Path.Combine(directory, "resource-bookmarks.json");

    /// <summary>
    /// 读取。
    /// </summary>
    public List<ResourceBookmark> Read()
    {
        if (!File.Exists(FileName))
            return [];
        var document = AtomicFile.ReadJson<ResourceBookmarkDocument>(FileName);
        if (document.SchemaVersion != 1 || document.Items == null)
            throw new IOException("资源收藏版本无效，已保留原文件。");
        if (document.Items.Any(i => i.Project == null || string.IsNullOrWhiteSpace(i.Project.Id) || i.Labels == null || string.IsNullOrWhiteSpace(i.Folder) || i.Folder.Length > 40) || document.Items.Select(i => i.Project.Id).Distinct().Count() != document.Items.Count || document.Folders?.Any(f => string.IsNullOrWhiteSpace(f) || f.Length > 40) == true)
            throw new IOException("资源收藏数据无效，已保留原文件。");
        return document.Items;
    }

    /// <summary>
    /// 执行 Labels。
    /// </summary>
    public static string[] Labels(string text)
    {
        var labels = text.Split([',', '，', ';', '；', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (labels.Length > 20 || labels.Any(t => t.Length > 40))
            throw new ArgumentException("最多 20 个标签，每个不超过 40 个字符。");
        return labels;
    }

    /// <summary>
    /// 保存。
    /// </summary>
    public void Save(ContentProject project, string kind, string labels, string? folder = null)
    {
        if (kind is not ("mod" or "modpack" or "resourcepack" or "shader" or "datapack") || string.IsNullOrWhiteSpace(project.Id))
            throw new ArgumentException("资源类型无效。");
        var parsed = Labels(labels);
        Mutate(items =>
        {
            var old = items.FirstOrDefault(i => i.Project.Id == project.Id);
            items.RemoveAll(i => i.Project.Id == project.Id);
            items.Add(new(project with
            {
                IsFavorite = false,
                CustomTags = ""
            }, kind, parsed, old?.Added ?? DateTimeOffset.UtcNow)
            {
                Folder = FolderName(folder ?? old?.Folder ?? "默认收藏夹")
            });
        });
    }

    static string FolderName(string name)
    {
        name = name.Trim();
        if (name.Length is 0 or > 40)
            throw new ArgumentException("收藏夹名称需为 1–40 个字符。");
        return name;
    }

    /// <summary>
    /// 执行 Folders。
    /// </summary>
    public List<string> Folders()
    {
        var items = Read();
        var doc = File.Exists(FileName) ? AtomicFile.ReadJson<ResourceBookmarkDocument>(FileName) : new();
        return new[] {
            "默认收藏夹"
        }.Concat(doc.Folders ?? []).Concat(items.Select(i => i.Folder)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// 创建文件夹。
    /// </summary>
    public void CreateFolder(string name)
    {
        name = FolderName(name);
        Directory.CreateDirectory(directory);
        using var lease = new FileStream(FileName + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Read();
        var doc = File.Exists(FileName) ? AtomicFile.ReadJson<ResourceBookmarkDocument>(FileName) : new();
        doc.Folders ??= [];
        if (!doc.Folders.Contains(name, StringComparer.OrdinalIgnoreCase))
            doc.Folders.Add(name);
        AtomicFile.WriteJson(FileName, doc);
    }

    /// <summary>
    /// 移除。
    /// </summary>
    public void Remove(string projectId) => Mutate(items => items.RemoveAll(i => i.Project.Id == projectId));

    void Mutate(Action<List<ResourceBookmark>> action)
    {
        Directory.CreateDirectory(directory);
        using var lease = new FileStream(FileName + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var items = Read();
        var doc = File.Exists(FileName) ? AtomicFile.ReadJson<ResourceBookmarkDocument>(FileName) : new();
        action(items);
        doc.Items = items;
        AtomicFile.WriteJson(FileName, doc);
    }

    /// <summary>
    /// 执行 Query。
    /// </summary>
    public List<ResourceBookmark> Query(string query = "", string? label = null, string? kind = null)
    {
        var words = query.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return Read().Where(i => kind == null || i.Kind == kind).Where(i => label == null || i.Labels.Contains(label, StringComparer.OrdinalIgnoreCase)).Where(i => words.All(w => (i.Project.Title + " " + i.Project.Description + " " + i.Project.Author + " " + string.Join(' ', i.Labels)).Contains(w, StringComparison.OrdinalIgnoreCase))).OrderByDescending(i => i.Added).ToList();
    }

    /// <summary>
    /// 执行 Prioritize。
    /// </summary>
    public List<ContentProject> Prioritize(IEnumerable<ContentProject> projects)
    {
        var items = Read().ToDictionary(i => i.Project.Id);
        return projects.Select(p => items.TryGetValue(p.Id, out var item) ? p with { IsFavorite = true, CustomTags = string.Join(" · ", item.Labels) } : p with { IsFavorite = false, CustomTags = "" }).OrderByDescending(p => p.IsFavorite).ToList();
    }
}

/// <summary>
/// 提供 Local内容Search 相关功能。
/// </summary>
public static class LocalContentSearch
{
    /// <summary>
    /// 执行 Filter。
    /// </summary>
    public static List<LocalContent> Filter(IEnumerable<LocalContent> items, string query, string kind = "全部", string state = "全部")
    {
        var words = query.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return items.Where(i => state == "全部" || (state == "已禁用") == i.Path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)).Where(i => kind == "全部" || Path.GetFileName(Path.GetDirectoryName(i.Path)) == kind).Where(i => words.All(w => (i.Name + " " + Path.GetFileName(i.Path) + " " + i.Detail + " " + i.Managed?.ProjectId).Contains(w, StringComparison.OrdinalIgnoreCase))).OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
