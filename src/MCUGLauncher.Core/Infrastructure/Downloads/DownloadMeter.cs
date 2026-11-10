namespace BlockLaunch.Downloads;

/// <summary>
/// 批次级速率统计：把各文件、各分片的增量字节汇总成整体速度，供进度文案显示 MB/s 与剩余时间。
/// </summary>
internal sealed class DownloadMeter
{
    readonly object gate = new();
    long total;
    long lastBytes;
    long lastTicks = Environment.TickCount64;
    double speed;

    public long TotalBytes
    {
        get
        {
            lock (gate)
                return total;
        }
    }

    public double Speed
    {
        get
        {
            lock (gate)
                return speed;
        }
    }

    public void Add(long bytes)
    {
        if (bytes <= 0)
            return;
        lock (gate)
        {
            total += bytes;
            var now = Environment.TickCount64;
            var elapsed = now - lastTicks;
            if (elapsed < 250)
                return;
            var instant = (total - lastBytes) * 1000.0 / elapsed;
            speed = speed <= 0 ? instant : speed * 0.7 + instant * 0.3;
            lastBytes = total;
            lastTicks = now;
        }
    }

    public void Reset()
    {
        lock (gate)
        {
            total = 0;
            lastBytes = 0;
            lastTicks = Environment.TickCount64;
            speed = 0;
        }
    }
}
