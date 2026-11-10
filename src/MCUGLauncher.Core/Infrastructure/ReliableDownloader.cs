using System.Security.Cryptography;
using BlockLaunch.Downloads;

namespace BlockLaunch;

/// <summary>
/// 可校验、可断点续传的下载器。
/// </summary>
/// <remarks>
/// 保留原有公开签名与线上行为（请求序列、异常文案、.part 命名、.lock 互斥）；实际传输由 <see cref="SingleStreamDownload"/> 执行。
/// 分片下载只对 CmlLib 版本文件启用，本入口固定单流，因此 Mod、整合包与安装器 jar 的下载路径不变。
/// </remarks>
public static class ReliableDownloader
{
    /// <summary>
    /// 执行 Partial路径。
    /// </summary>
    public static string PartialPath(string target, string hash) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(target))!, ".blocklaunch-" + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(target) + "\n" + hash.ToLowerInvariant()))).ToLowerInvariant() + ".part");

    /// <summary>
    /// 执行 下载。
    /// </summary>
    /// <param name="http">HTTP 客户端</param>
    /// <param name="uri">下载地址</param>
    /// <param name="target">目标文件</param>
    /// <param name="hash">期望的校验值</param>
    /// <param name="sha1">true 表示 SHA1，否则 SHA512</param>
    /// <param name="size">可选的文件大小</param>
    /// <param name="progress">进度回调</param>
    /// <param name="token">取消令牌</param>
    public static async Task Download(HttpClient http, Uri uri, string target, string hash, bool sha1 = false, long? size = null, Action<string>? progress = null, CancellationToken token = default)
    {
        try
        {
            var download = new SingleStreamDownload(http, uri, target, hash, sha1 ? HashAlgorithmName.SHA1 : HashAlgorithmName.SHA512, size, SingleStreamPolicy.Legacy, progress);
            await download.RunAsync(token).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw DownloadErrors.Translate(ex);
        }
        catch (IOException ex) when (DownloadErrors.IsPermanentFileError(ex))
        {
            throw DownloadErrors.Translate(ex);
        }
    }
}
