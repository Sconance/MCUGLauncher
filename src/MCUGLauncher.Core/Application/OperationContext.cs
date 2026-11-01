namespace BlockLaunch;

// One execution's cancellation, progress and commit boundary; owned by OperationCoordinator.
/// <summary>
/// 提供 OperationContext 相关功能。
/// </summary>
public sealed class OperationContext
{
    long transferredBytes;

    public long TransferredBytes => Interlocked.Read(ref transferredBytes);

    /// <summary>
    /// 执行 AddTransferredBytes。
    /// </summary>
    internal void AddTransferredBytes(int bytes)
    {
        if (bytes > 0)
            Interlocked.Add(ref transferredBytes, bytes);
    }

    /// <summary>
    /// 执行 Report文件Progress。
    /// </summary>
    internal void ReportFileProgress(long current, long total)
    {
        if (Record.Download?.Kind == "resource" && total > 0)
            owner.Report(this, Record.Message, Math.Clamp(current * 100.0 / total, 0, 100));
    }

    readonly OperationCoordinator owner;
    internal readonly CancellationTokenSource Cancellation = new();
    internal bool RequeueOnStop;

    public CancellationToken Token => Cancellation.Token;

    public OperationRecord Record
    {
        get;
    }

    internal OperationContext(OperationCoordinator owner, OperationRecord record)
    {
        this.owner = owner;
        Record = record;
    }

    /// <summary>
    /// 执行 Report。
    /// </summary>
    public void Report(string message, double? percent = null) => owner.Report(this, message, percent);

    /// <summary>
    /// 执行 BeginCommit。
    /// </summary>
    public IDisposable BeginCommit()
    {
        lock (owner.Gate)
        {
            Token.ThrowIfCancellationRequested();
            Record.Committing = true;
            owner.Persist(this);
        }

        return new CommitScope(this);
    }

    sealed class CommitScope(OperationContext context) : IDisposable
    {
        /// <summary>
        /// 执行 Dispose。
        /// </summary>
        public void Dispose()
        {
            lock (context.owner.Gate)
            {
                context.Record.Committing = false;
                context.owner.Persist(context);
            }
        }
    }
}
