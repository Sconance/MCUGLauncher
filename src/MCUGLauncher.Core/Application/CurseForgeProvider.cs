namespace BlockLaunch;

// Reserved provider boundary: never scrape pages or silently send an unlicensed key.
/// <summary>
/// 提供 CurseForgeProvider 相关功能。
/// </summary>
public sealed class CurseForgeProvider : IResourceProvider
{
    static Exception Missing() => new NotSupportedException("CurseForge 需要官方 API Key，当前未启用。请使用 Modrinth 或手动下载后导入。");
    /// <summary>
    /// 搜索。
    /// </summary>
    public Task<SearchPage> Search(string query, string game, string loader, string type, int offset = 0) => Task.FromException<SearchPage>(Missing());
    /// <summary>
    /// 执行 Versions。
    /// </summary>
    public Task<List<ContentVersion>> Versions(string project, string game, string loader, string type) => Task.FromException<List<ContentVersion>>(Missing());
    /// <summary>
    /// 解析。
    /// </summary>
    public Task<InstallPlan> Resolve(ContentVersion version, string game, string loader) => Task.FromException<InstallPlan>(Missing());
}
