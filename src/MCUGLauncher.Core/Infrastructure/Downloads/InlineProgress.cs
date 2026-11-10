namespace BlockLaunch.Downloads;

/// <summary>
/// 同步回调式 <see cref="IProgress{T}"/>，避免 <see cref="Progress{T}"/> 把进度投递到线程池后丢失时序。
/// </summary>
public sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}
