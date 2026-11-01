using System.Security.Cryptography;

namespace BlockLaunch;

/// <summary>
/// 资源文件下载。
/// </summary>
/// <remarks>
/// 依赖 ContentVersion、HttpClient 与 ReliableDownloader；按资源类型选择目录并复用校验下载，支持进度回调。
/// </remarks>
public static class ResourceDownloads
{
    /// <summary>
    /// 执行 路径For。
    /// </summary>
    /// <param name="directory">实例或资源根目录</param>
    /// <param name="kind">资源类型，如 mod/resourcepack</param>
    /// <param name="version">资源版本信息</param>
    /// <returns>资源应保存的完整路径</returns>
    public static string PathFor(string directory, string kind, ContentVersion version)
    {
        var folder = kind switch
        {
            "mod" => "Mods",
            "modpack" => "Modpacks",
            "resourcepack" => "Resourcepacks",
            "shader" => "Shaders",
            "datapack" => "Datapacks",
            _ => throw new IOException("未知资源类型。")
        };
        var file = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files.FirstOrDefault() ?? throw new IOException("此版本没有下载文件。");
        if (string.IsNullOrWhiteSpace(version.Id) || string.IsNullOrWhiteSpace(version.ProjectId))
            throw new IOException("资源版本标识缺失。");
        ContentService.SafeFile(version.ProjectId);
        ContentService.SafeFile(version.Id);
        ContentService.SafeFile(file.Filename);
        if (!Uri.TryCreate(file.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "cdn.modrinth.com" || uri.UserInfo.Length > 0)
            throw new IOException("资源地址不是 Modrinth 官方 HTTPS CDN。");
        var extension = kind == "mod" ? ".jar" : kind == "modpack" ? ".mrpack" : ".zip";
        if (!file.Filename.EndsWith(extension, StringComparison.OrdinalIgnoreCase) || file.Size < 0 || file.Size > 1536L * 1024 * 1024)
            throw new IOException("资源文件格式或大小无效。");
        if (!file.Hashes.TryGetValue("sha512", out var hash) || hash.Length != 128 || hash.Any(c => !Uri.IsHexDigit(c)))
            throw new IOException("资源缺少有效 SHA-512。");
        return Path.Combine(Path.GetFullPath(directory), folder, version.ProjectId, version.Id, file.Filename);
    }

    /// <summary>
    /// 执行 下载。
    /// </summary>
    /// <param name="http">HTTP 客户端</param>
    /// <param name="directory">保存根目录</param>
    /// <param name="kind">资源类型</param>
    /// <param name="version">资源版本信息</param>
    /// <param name="progress">进度回调</param>
    /// <param name="token">取消令牌</param>
    /// <returns>下载文件的完整路径</returns>
    public static async Task<string> Download(HttpClient http, string directory, string kind, ContentVersion version, Action<string>? progress = null, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var path = PathFor(directory, kind, version);
        for (var parent = Path.GetDirectoryName(path); parent != null; parent = Path.GetDirectoryName(parent))
            if (Directory.Exists(parent) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("下载路径包含链接，请选择普通目录。");
        var file = version.Files.FirstOrDefault(f => f.Primary) ?? version.Files.First();
        var hash = file.Hashes["sha512"];
        if (File.Exists(path))
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("目标文件是链接。");
            await using var input = File.OpenRead(path);
            if (!Convert.ToHexString(await SHA512.HashDataAsync(input, token)).Equals(hash, StringComparison.OrdinalIgnoreCase))
                throw new IOException("下载目录已有同名不同内容的文件，请先移走原文件后重试。");
        }

        await ReliableDownloader.Download(http, new Uri(file.Url), path, hash, size: file.Size, progress: progress, token: token);
        return path;
    }
}
