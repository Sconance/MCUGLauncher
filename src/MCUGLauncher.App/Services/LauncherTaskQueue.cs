using BlockLaunch;

namespace MCUGLauncher.Services;

// Owns persisted downloads. UI navigation never owns a transfer's lifetime.
internal sealed class LauncherTaskQueue : IDisposable
{
    readonly OperationCoordinator operations;
    readonly Func<OperationRecord, OperationContext, Task> execute;
    readonly Action changed;
    readonly Action<string> log;
    readonly object gate = new();
    Task completion = Task.CompletedTask;
    public Task Completion
    {
        get
        {
            lock (gate)
                return completion;
        }
    }
    bool pumping;
    bool stopped;
    DateTime lastProgress;

    public LauncherTaskQueue(string directory, Func<OperationRecord, OperationContext, Task> execute,
        Action changed, Action<string> log)
    {
        operations = new OperationCoordinator(directory);
        this.execute = execute;
        this.changed = changed;
        this.log = log;
        // Destructive work never resumes automatically after a launcher restart.
        foreach (var record in operations.History().Where(r => r.State == OperationState.Queued && r.Download?.Kind == "uninstall"))
            operations.ChangePending(record.Id, OperationState.Paused);
        operations.Changed += OnChanged;
    }

    public List<OperationRecord> History() => operations.History();

    void OnChanged(OperationRecord record)
    {
        if (record.State == OperationState.Running && DateTime.UtcNow - lastProgress < TimeSpan.FromMilliseconds(200))
            return;
        lastProgress = DateTime.UtcNow;
        changed();
    }

    public void Enqueue(string name, string root, string target, DownloadRequest request)
    {
        lock (gate)
        {
            if (stopped)
                throw new ObjectDisposedException(nameof(LauncherTaskQueue));
            operations.Enqueue(name, root, target, request);
            Start();
        }
    }

    public void Start()
    {
        lock (gate)
        {
            if (pumping || stopped)
                return;
            pumping = true;
            completion = PumpAsync();
        }
    }

    async Task PumpAsync()
    {
        await Task.Yield();
        try
        {
            while (true)
            {
                Task run;
                OperationRecord? next;
                lock (gate)
                {
                    next = stopped ? null : operations.History().Where(r => r.State == OperationState.Queued && r.Download != null)
                        .OrderBy(r => r.QueueOrder).FirstOrDefault();
                    if (next == null)
                    {
                        pumping = false;
                        return;
                    }
                    run = operations.Run(next.Name, next.Root, next.Target, ctx => execute(next, ctx), next);
                }
                try
                {
                    await run;
                }
                catch (Exception ex) { log(ex.ToString()); }
                changed();
            }
        }
        catch (Exception ex)
        {
            lock (gate)
                pumping = false;
            log(ex.ToString());
        }
    }

    public void RequireLaunchAvailable(string root, string key, string directory)
    {
        var record = operations.Current?.Record;
        var request = record?.Download;
        if (record == null || request == null)
            return;
        static bool Same(string left, string right) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(left))
            .Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)), StringComparison.OrdinalIgnoreCase);
        var installsGame = Same(record.Root, root) && request.Kind is "game" or "pack";
        var maintainsInstance = request.Instance != null && (Same(request.Instance.Directory, directory)
            || Same(record.Root, root) && request.Instance.Key == key);
        var writesContent = request.Kind is "content" or "update" && Same(record.Target, directory);
        if (installsGame || maintainsInstance || writesContent)
            throw new IOException("目标实例或其游戏文件正在维护，请等待任务完成或暂停后再启动。");
    }

    public void Change(string id, string action)
    {
        bool accepted;
        if (action == "delete")
            accepted = operations.DeleteHistory(id);
        else if (operations.Current?.Record.Id == id)
            accepted = action is "pause" or "cancel" && operations.Cancel(pause: action == "pause");
        else
            accepted = operations.ChangePending(id, action switch
            {
                "pause" => OperationState.Paused,
                "cancel" => OperationState.Cancelled,
                "resume" => OperationState.Queued,
                _ => throw new ArgumentException("未知任务操作。")
            });
        if (!accepted)
            throw new InvalidOperationException("任务状态已变化，请刷新后重试。");
        changed();
        Start();
    }

    public void Dispose()
    {
        lock (gate)
            stopped = true;
        operations.Cancel(requeueOnStop: true);
        operations.Changed -= OnChanged;
    }
}
