namespace BlockLaunch.Downloads;

/// <summary>
/// 重试等待策略。
/// </summary>
internal static class DownloadBackoff
{
    /// <summary>
    /// 指数退避加 ±30% 抖动，避免多个分片同时重试造成尖峰。
    /// </summary>
    public static TimeSpan Exponential(int attempt)
    {
        var seconds = Math.Pow(2, Math.Clamp(attempt, 0, 5));
        var jitter = 0.7 + Random.Shared.NextDouble() * 0.6;
        return TimeSpan.FromSeconds(Math.Max(0.25, seconds * jitter));
    }
}
