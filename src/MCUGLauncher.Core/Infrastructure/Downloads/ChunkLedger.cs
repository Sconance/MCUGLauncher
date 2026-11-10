namespace BlockLaunch.Downloads;

/// <summary>
/// 分片续传台账，记录已完成的分片编号。
/// </summary>
/// <remarks>
/// 写入走 <see cref="AtomicFile"/>，进程在任意时刻退出都不会留下半个 JSON；分片数据与台账不匹配时整体重下。
/// </remarks>
internal sealed class ChunkLedger
{
    /// <summary>
    /// 获取或设置 台账格式版本。
    /// </summary>
    public int Version { get; set; } = 1;
    /// <summary>
    /// 获取或设置 期望哈希。
    /// </summary>
    public string Hash { get; set; } = "";
    /// <summary>
    /// 获取或设置 文件总大小。
    /// </summary>
    public long Size { get; set; }
    /// <summary>
    /// 获取或设置 分片大小。
    /// </summary>
    public int ChunkSize { get; set; }
    /// <summary>
    /// 获取或设置 已完成的分片编号。
    /// </summary>
    public List<int> Completed { get; set; } = [];
    /// <summary>
    /// 获取或设置 最后一次成功下载所用的源名称。
    /// </summary>
    public string Source { get; set; } = "";

    /// <summary>
    /// 判断台账是否可用于当前任务。
    /// </summary>
    public bool Matches(string hash, long size) => Version == 1 && Size == size && Hash.Equals(hash, StringComparison.OrdinalIgnoreCase) && ChunkSize > 0;

    /// <summary>
    /// 读取台账；缺失或损坏时返回 null。
    /// </summary>
    public static ChunkLedger? Load(string path)
    {
        try
        {
            return File.Exists(path) ? AtomicFile.ReadJson<ChunkLedger>(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 原子写入台账。
    /// </summary>
    public static void Save(string path, ChunkLedger ledger) => AtomicFile.WriteJson(path, ledger);
}
