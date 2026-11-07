using System.IO;
using BlockLaunch;
using MCUGLauncher.Services;

internal static class QueueScenarios
{
    public static async Task Run(string fixture)
    {
        var coordinator = new OperationCoordinator(Path.Combine(fixture, "overall-progress"));
        var finishedProgress = await coordinator.Run("install", fixture, fixture, task =>
        {
            var progress = new InstallationProgress(task);
            progress.Advance(35);
            progress.Message("next file");
            if (task.Record.Percent != 35)
                throw new Exception("file message reset overall progress");
            progress.Advance(2);
            if (task.Record.Percent != 35)
                throw new Exception("new phase moved progress backwards");
            progress.Advance(100);
            if (task.Record.Percent != 99)
                throw new Exception("installation reached 100 before success");
            return Task.CompletedTask;
        });
        if (finishedProgress.Percent != 100)
            throw new Exception("successful installation did not reach 100");
        Console.WriteLine("PASS installation progress survives file changes, never regresses and completes only on success");
        var directory = Path.Combine(fixture, "queue");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<string>();
        using var queue = new LauncherTaskQueue(directory, async (record, task) =>
        {
            lock (order)
                order.Add(record.Name);
            if (record.Name == "first")
            {
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, task.Token);
            }
            task.Report("verified", 100);
        }, () => { }, _ => { });
        var root = Path.Combine(fixture, "game");
        queue.Enqueue("first", root, "snapshot-a", new DownloadRequest { Version = "fixture-a" });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        queue.Enqueue("second", root, "snapshot-b", new DownloadRequest { Version = "fixture-b" });
        var first = queue.History().Single(r => r.Name == "first");
        var second = queue.History().Single(r => r.Name == "second");
        queue.Change(second.Id, "pause");
        queue.Change(first.Id, "cancel");
        await Until(() => queue.History().Single(r => r.Id == first.Id).State == OperationState.Cancelled);
        if (queue.History().Single(r => r.Id == second.Id).State != OperationState.Paused || order.Count != 1)
            throw new Exception("paused queued task executed");
        queue.Change(second.Id, "resume");
        await Until(() => queue.History().Single(r => r.Id == second.Id).State == OperationState.Completed);
        if (queue.History().Single(r => r.Id == second.Id).Target != "snapshot-b")
            throw new Exception("target snapshot changed");
        queue.Change(first.Id, "delete");
        if (queue.History().Any(r => r.Id == first.Id))
            throw new Exception("history not removed");
        Console.WriteLine("PASS real persisted queue serializes, cancels, pauses, resumes and deletes records");
        var recoveryDir = Path.Combine(fixture, "recovery-queue");
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        new OperationCoordinator(recoveryDir).Enqueue("recover", root, "snapshot", new DownloadRequest { Version = "fixture" });
        using (var next = new LauncherTaskQueue(recoveryDir, (_, _) => { recovered.TrySetResult(); return Task.CompletedTask; }, () => { }, _ => { }))
        {
            next.Start();
            await recovered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Until(() => next.History().Single().State == OperationState.Completed);
        }
        Console.WriteLine("PASS persisted queued operation resumes in a fresh queue owner");
        var destructive = Path.Combine(fixture, "uninstall-recovery");
        new OperationCoordinator(destructive).Enqueue("uninstall", root, "fixture", new DownloadRequest { Kind = "uninstall" });
        var executed = false;
        using var guarded = new LauncherTaskQueue(destructive, (_, _) => { executed = true; return Task.CompletedTask; }, () => { }, _ => { });
        guarded.Start();
        await guarded.Completion;
        if (executed || guarded.History().Single().State != OperationState.Paused)
            throw new Exception("uninstall auto-resumed");
        Console.WriteLine("PASS queued uninstall requires new confirmation after restart");

    }
    static async Task Until(Func<bool> done)
    {
        var end = DateTime.UtcNow.AddSeconds(5);
        while (!done())
        {
            if (DateTime.UtcNow > end)
                throw new TimeoutException("queue did not settle");
            await Task.Delay(20);
        }
    }
}
