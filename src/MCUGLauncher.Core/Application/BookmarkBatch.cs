namespace BlockLaunch;

/// <summary>
/// 表示 Bookmark下载 数据。
/// </summary>
public record BookmarkDownload(ResourceBookmark Bookmark, ContentVersion? Version, string Error);

/// <summary>
/// 提供 BookmarkBatch 相关功能。
/// </summary>
public static class BookmarkBatch
{
    /// <summary>
    /// 解析。
    /// </summary>
    public static async Task<List<BookmarkDownload>> Resolve(IResourceProvider provider, IEnumerable<ResourceBookmark> items, string game, string loader, CancellationToken token)
    {
        var result = new List<BookmarkDownload>();
        foreach (var item in items.DistinctBy(i => i.Project.Id))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (item.Kind == "mod" && loader == "vanilla")
                    throw new IOException("当前实例为纯原版，请先选择模组加载器版本。");
                var versions = await provider.Versions(item.Project.Id, item.Kind == "modpack" ? "" : game, item.Kind == "modpack" ? "" : item.Kind == "datapack" ? "datapack" : loader, item.Kind);
                token.ThrowIfCancellationRequested();
                var version = versions.FirstOrDefault(v => v.VersionType == "release") ?? versions.FirstOrDefault() ?? throw new IOException("没有兼容版本。");
                result.Add(new(item, version, ""));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                result.Add(new(item, null, ex.Message));
            }
        }

        return result;
    }
}
