namespace BlockLaunch;

// Flows the current commit boundary through asynchronous service calls.
/// <summary>
/// 提供 OperationExecution 相关功能。
/// </summary>
public static class OperationExecution
{
    internal static readonly AsyncLocal<OperationContext?> Ambient = new();

    /// <summary>
    /// 执行 BeginCommit。
    /// </summary>
    public static IDisposable BeginCommit() => Ambient.Value?.BeginCommit() ?? new Empty();

    sealed class Empty : IDisposable
    {
        /// <summary>
        /// 执行 Dispose。
        /// </summary>
        public void Dispose()
        {
        }
    }
}
