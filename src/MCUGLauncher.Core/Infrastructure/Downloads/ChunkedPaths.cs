namespace BlockLaunch.Downloads;

/// <summary>
/// 分片下载的临时文件命名与清理。
/// </summary>
/// <remarks>
/// 分片数据使用 .part 之外的独立后缀，避免与 <see cref="SingleStreamDownload"/> 的顺序续传文件互相误读：
/// 分片内容在文件中是离散的，若被当作顺序前缀续传会生成损坏的文件。
/// </remarks>
internal static class ChunkedPaths
{
    /// <summary>
    /// 分片数据文件路径。
    /// </summary>
    public static string Data(string target, string hash) => ReliableDownloader.PartialPath(target, hash) + ".chunked";

    /// <summary>
    /// 分片续传台账路径。
    /// </summary>
    public static string Ledger(string data) => data + ".json";

    /// <summary>
    /// 删除分片数据与台账；文件被占用时静默忽略，避免清理失败掩盖真正的下载错误。
    /// </summary>
    public static void Delete(string target, string hash)
    {
        var data = Data(target, hash);
        TryDelete(Ledger(data));
        TryDelete(data);
    }

    static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
