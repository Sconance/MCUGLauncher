namespace BlockLaunch.Downloads;

/// <summary>
/// 一个字节区间（含首尾）。
/// </summary>
internal readonly record struct ChunkRange(int Index, long Start, long End)
{
    /// <summary>
    /// 获取区间字节数。
    /// </summary>
    public long Length => End - Start + 1;
}

/// <summary>
/// 分片切分规则。
/// </summary>
internal static class ChunkPlan
{
    const long MaximumChunkSize = 8L * 1024 * 1024;

    /// <summary>
    /// 选择分片大小：约为总大小除以并发数的两倍，并限制在阈值与 8 MiB 之间。
    /// </summary>
    public static int ChooseChunkSize(long size, int concurrency, int minimum)
    {
        var target = size / Math.Max(1, concurrency * 2);
        var chunk = Math.Clamp(target, minimum, MaximumChunkSize);
        if ((size + chunk - 1) / chunk > DownloadOptions.MaxChunksPerFile)
            chunk = (size + DownloadOptions.MaxChunksPerFile - 1) / DownloadOptions.MaxChunksPerFile;
        return (int)Math.Max(minimum, chunk);
    }

    /// <summary>
    /// 按固定的分片大小切分区间。
    /// </summary>
    public static IReadOnlyList<ChunkRange> Build(long size, int chunkSize)
    {
        var ranges = new List<ChunkRange>();
        for (var start = 0L; start < size; start += chunkSize)
        {
            var end = Math.Min(size - 1, start + chunkSize - 1);
            ranges.Add(new(ranges.Count, start, end));
        }

        return ranges;
    }
}
