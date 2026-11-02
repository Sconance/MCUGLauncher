using MCUGLauncher;
using System.Text.Json;

var root = Path.Combine(Path.GetTempPath(), "MCUGLauncher-queue-" + Guid.NewGuid().ToString("N"));
var queue = new OperationCoordinator(root);
void Check(bool value, string name)
{
    if (!value)
        throw new Exception(name);
    Console.WriteLine("PASS " + name);
}

OperationRecord Add(string name = "test") => queue.Enqueue(name, root, "1.21.1", new()
{
    Version = "1.21.1",
    Destination = Path.Combine(root, "original"),
    Content = new()
    {
        Id = "fixed-version"
    }
});
var first = Add();
var second = Add("second");
Check(queue.History().Count == 2 && queue.History().All(r => r.State == OperationState.Queued), "durable queue creation");
Check(queue.ChangePending(second.Id, OperationState.Paused), "pause waiting task");
Check(!queue.ChangePending(second.Id, OperationState.Completed), "invalid transition rejected");
var entered = new TaskCompletionSource();
var running = queue.Run(first.Name, first.Root, first.Target, async c =>
{
    entered.SetResult();
    await Task.Delay(Timeout.Infinite, c.Token);
}, first);
await entered.Task;
Check(queue.Cancel(true), "pause active download");
Check((await running).State == OperationState.Paused && queue.Current == null, "pause waits for cleanup");
var restarted = new OperationCoordinator(root);
Check(restarted.History().All(r => r.State == OperationState.Paused), "manual pauses survive restart");
Check(queue.ChangePending(first.Id, OperationState.Queued), "continue paused download");
try
{
    await queue.Run(first.Name, root, first.Target, _ => throw new IOException("simulated offline"), first);
}
catch (IOException)
{
}

Check(queue.History().Single(r => r.Id == first.Id).State == OperationState.Failed, "failure persisted");
Check(queue.ChangePending(first.Id, OperationState.Queued), "retry failed download");
var committed = new TaskCompletionSource();
var release = new TaskCompletionSource();
running = queue.Run(first.Name, root, first.Target, async c =>
{
    using var scope = c.BeginCommit();
    committed.SetResult();
    await release.Task;
}, first);
await committed.Task;
Check(!queue.Cancel(true) && !queue.Cancel(), "pause and cancel blocked during commit");
release.SetResult();
Check((await running).State == OperationState.Completed, "commit completes");
Check(!queue.ChangePending(first.Id, OperationState.Queued), "completed pack cannot accidentally retry");
Check(queue.ChangePending(second.Id, OperationState.Cancelled), "cancel waiting task");
Check(!queue.ChangePending(second.Id, OperationState.Queued), "cancelled task stays cancelled");
var shutdown = Add("shutdown");
entered = new();
running = queue.Run(shutdown.Name, root, shutdown.Target, async c =>
{
    entered.SetResult();
    await Task.Delay(Timeout.Infinite, c.Token);
}, shutdown);
await entered.Task;
Check(queue.Cancel(true, true), "shutdown suspends active download");
Check((await running).State == OperationState.Queued, "normal shutdown resumes next startup");
var dead = Add("crash");
dead.State = OperationState.Running;
dead.ProcessId = int.MaxValue;
dead.ProcessStartTicks = 1;
AtomicFile.WriteJson(Path.Combine(root, dead.Id + ".json"), dead);
var deadPause = Add("crash during pause");
deadPause.State = OperationState.Pausing;
deadPause.ProcessId = int.MaxValue;
AtomicFile.WriteJson(Path.Combine(root, deadPause.Id + ".json"), deadPause);
var deadCancel = Add("crash during cancel");
deadCancel.State = OperationState.Cancelling;
deadCancel.ProcessId = int.MaxValue;
AtomicFile.WriteJson(Path.Combine(root, deadCancel.Id + ".json"), deadCancel);
restarted = new OperationCoordinator(root);
Check(restarted.History().Single(r => r.Id == dead.Id).State == OperationState.Queued, "unexpected process exit requeues download");
Check(restarted.History().Single(r => r.Id == deadPause.Id).State == OperationState.Paused, "crash during pause preserves intent");
Check(restarted.History().Single(r => r.Id == deadCancel.Id).State == OperationState.Cancelled, "crash during cancel preserves intent");
var frozen = restarted.History().Single(r => r.Id == dead.Id).Download!;
Check(frozen.Destination == Path.Combine(root, "original") && frozen.Content?.Id == "fixed-version", "restart preserves exact destination and content version");
// A committed pack must be returned rather than installed as a second instance after a crash.
var alias = "pack-123456789abc";
var instance = Path.Combine(root, "instances", alias);
Directory.CreateDirectory(instance);
var profile = Path.Combine(root, "versions", alias);
Directory.CreateDirectory(profile);
File.WriteAllText(Path.Combine(profile, alias + ".json"), "{}");
AtomicFile.WriteJson(Path.Combine(instance, ".mcuglauncher-pack.json"), new InstalledPack("pack", "1.21.1", "vanilla", "", "1.21.1", alias));
var calls = 0;
var pack = await new PackService(new HttpClient()).Import("does-not-exist", root, _ =>
{
    calls++;
    return Task.FromResult("1.21.1");
}, false, instanceId: alias);
Check(pack.InstanceId == alias && calls == 0, "committed pack recovery is idempotent");
Console.WriteLine("ALL_QUEUE_CHECKS_PASSED");
