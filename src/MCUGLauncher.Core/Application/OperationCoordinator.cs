using System.Diagnostics;

namespace MCUGLauncher;

// Serializes task state changes and persists recoverable queue history.
/// <summary>
/// 提供 Operation协调器 相关功能。
/// </summary>
public sealed class OperationCoordinator
{
    readonly string directory;
    internal readonly object Gate = new();
    DateTime lastPersist;

    public OperationContext? Current
    {
        get;
        private set;
    }

    public event Action<OperationRecord>? Changed;

    public OperationCoordinator(string directory)
    {
        this.directory = directory;
        Directory.CreateDirectory(directory);
        Recover();
    }

    string FileFor(OperationRecord record) => Path.Combine(directory, record.Id + ".json");

    /// <summary>
    /// 执行 Persist。
    /// </summary>
    internal void Persist(OperationContext context)
    {
        context.Record.Updated = DateTimeOffset.UtcNow;
        AtomicFile.WriteJson(FileFor(context.Record), context.Record);
        Changed?.Invoke(Snapshot(context.Record));
    }

    static OperationRecord Snapshot(OperationRecord record) => System.Text.Json.JsonSerializer.Deserialize<OperationRecord>(System.Text.Json.JsonSerializer.Serialize(record))!;

    /// <summary>
    /// 执行 Report。
    /// </summary>
    internal void Report(OperationContext context, string message, double? percent)
    {
        lock (Gate)
        {
            if (Current != context || context.Record.State is not (OperationState.Running or OperationState.Cancelling))
                return;
            if (context.Record.State == OperationState.Cancelling)
                return;
            context.Record.Message = message;
            context.Record.Percent = percent;
            if ((DateTime.UtcNow - lastPersist).TotalMilliseconds < 200)
                return;
            lastPersist = DateTime.UtcNow;
            try
            {
                Persist(context);
            }
            catch (IOException)
            {
                context.Record.Message = "进度记录暂时无法保存，请检查磁盘。";
                Changed?.Invoke(Snapshot(context.Record));
            }
        }
    }

    /// <summary>
    /// 判断能否。
    /// </summary>
    public bool Cancel(bool pause = false, bool requeueOnStop = false)
    {
        lock (Gate)
        {
            var context = Current;
            if (context == null || context.Record.Committing || context.Record.State != OperationState.Running || pause && context.Record.Download == null)
                return false;
            context.RequeueOnStop = requeueOnStop;
            context.Record.State = pause ? OperationState.Pausing : OperationState.Cancelling;
            context.Record.Message = pause ? "正在暂停并保存进度…" : "正在取消，请等待清理完成…";
            Persist(context);
            context.Cancellation.Cancel();
            return true;
        }
    }

    /// <summary>
    /// 执行 Enqueue。
    /// </summary>
    public OperationRecord Enqueue(string name, string root, string target, DownloadRequest request)
    {
        lock (Gate)
        {
            var record = new OperationRecord
            {
                Name = name,
                Root = Path.GetFullPath(root),
                Target = target,
                Download = request,
                State = OperationState.Queued,
                Message = "等待执行",
                QueueOrder = Math.Max(DateTimeOffset.UtcNow.UtcTicks, History().Select(r => r.QueueOrder).DefaultIfEmpty().Max() + 1)
            };
            AtomicFile.WriteJson(FileFor(record), record);
            Changed?.Invoke(Snapshot(record));
            return record;
        }
    }

    /// <summary>
    /// 执行 ChangePending。
    /// </summary>
    public bool ChangePending(string id, OperationState state)
    {
        lock (Gate)
        {
            var record = History().FirstOrDefault(r => r.Id == id);
            if (record?.Download == null || record.Id == Current?.Record.Id)
                return false;
            var allowed = state switch
            {
                OperationState.Queued => record.State is OperationState.Paused or OperationState.Failed or OperationState.Interrupted,
                OperationState.Paused => record.State == OperationState.Queued,
                OperationState.Cancelled => record.State is OperationState.Queued or OperationState.Paused or OperationState.Failed or OperationState.Interrupted,
                _ => false
            };
            if (!allowed)
                return false;
            record.State = state;
            record.Message = state == OperationState.Queued ? "等待继续 / 重试" : state == OperationState.Paused ? "已暂停，保留下载进度" : "已取消";
            record.Updated = DateTimeOffset.UtcNow;
            AtomicFile.WriteJson(FileFor(record), record);
            Changed?.Invoke(Snapshot(record));
            return true;
        }
    }

    /// <summary>
    /// 执行 Run。
    /// </summary>
    public async Task<OperationRecord> Run(string name, string root, string target, Func<OperationContext, Task> action, OperationRecord? queued = null)
    {
        OperationContext context;
        lock (Gate)
        {
            if (Current != null)
                throw new InvalidOperationException("已有任务正在进行。");
            var record = queued == null ? new OperationRecord
            {
                Name = name,
                Root = root,
                Target = target
            } : AtomicFile.ReadJson<OperationRecord>(FileFor(queued));
            if (queued != null && record.State != OperationState.Queued)
                throw new InvalidOperationException("此任务已不在队列中。");
            record.State = OperationState.Running;
            record.ProcessId = Environment.ProcessId;
            record.ProcessStartTicks = Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks;
            record.Percent = null;
            context = new(this, record);
            Current = context;
            try
            {
                Persist(context);
            }
            catch
            {
                Current = null;
                context.Cancellation.Dispose();
                throw;
            }
        }

        OperationExecution.Ambient.Value = context;
        try
        {
            await action(context);
            lock (Gate)
            {
                context.Record.Percent = 100;
                context.Record.State = OperationState.Completed;
                context.Record.Message = "完成";
            }
        }
        catch (OperationCanceledException) when (context.Token.IsCancellationRequested)
        {
            lock (Gate)
            {
                var paused = context.Record.State == OperationState.Pausing;
                context.Record.State = context.RequeueOnStop ? OperationState.Queued : paused ? OperationState.Paused : OperationState.Cancelled;
                context.Record.Message = context.RequeueOnStop ? "等待下次启动继续" : paused ? "已暂停；继续时校验已有文件并恢复下载" : "已取消，已校验缓存保留";
            }
        }
        catch (Exception ex)
        {
            lock (Gate)
            {
                context.Record.State = OperationState.Failed;
                context.Record.Message = LogPrivacy.Redact(ex.Message);
            }

            throw;
        }
        finally
        {
            OperationExecution.Ambient.Value = null;
            lock (Gate)
            {
                context.Record.Committing = false;
                try
                {
                    Persist(context);
                }
                finally
                {
                    Current = null;
                    context.Cancellation.Dispose();
                }
            }
        }

        return context.Record;
    }

    /// <summary>
    /// 移动Pending。
    /// </summary>
    public bool MovePending(string id, int direction)
    {
        lock (Gate)
        {
            var pending = History().Where(r => r.Download != null && r.State == OperationState.Queued).OrderBy(r => r.QueueOrder == 0 ? r.Started.UtcTicks : r.QueueOrder).ThenBy(r => r.Started).ToList();
            var index = pending.FindIndex(r => r.Id == id);
            var other = index + Math.Sign(direction);
            if (index < 0 || other < 0 || other >= pending.Count || direction == 0)
                return false;
            (pending[index], pending[other]) = (pending[other], pending[index]);
            var start = DateTimeOffset.UtcNow.UtcTicks;
            for (var i = 0; i < pending.Count; i++)
            {
                pending[i].QueueOrder = start + i;
                AtomicFile.WriteJson(FileFor(pending[i]), pending[i]);
                Changed?.Invoke(Snapshot(pending[i]));
            }

            return true;
        }
    }

    /// <summary>
    /// 判断能否DeleteHistory。
    /// </summary>
    public static bool CanDeleteHistory(OperationRecord record) => !record.Committing &&
        record.State is OperationState.Completed or OperationState.Cancelled or OperationState.Failed or OperationState.Interrupted;

    /// <summary>
    /// 删除History。
    /// </summary>
    public bool DeleteHistory(string id)
    {
        lock (Gate)
        {
            if (!Guid.TryParseExact(id, "N", out _) || Current?.Record.Id == id)
                return false;
            var record = History().FirstOrDefault(item => item.Id == id);
            if (record == null || !CanDeleteHistory(record))
                return false;
            File.Delete(Path.Combine(directory, id + ".json"));
            return true;
        }
    }

    /// <summary>
    /// 执行 ClearFinishedDownloads。
    /// </summary>
    public int ClearFinishedDownloads()
    {
        lock (Gate)
            return History().Where(record => record.Download != null && CanDeleteHistory(record))
                .Count(record => DeleteHistory(record.Id));
    }

    /// <summary>
    /// 执行 History。
    /// </summary>
    public List<OperationRecord> History()
    {
        lock (Gate)
        {
            var result = new List<OperationRecord>();
            foreach (var file in Directory.GetFiles(directory, "*.json"))
                try
                {
                    var record = AtomicFile.ReadJson<OperationRecord>(file);
                    if (!Guid.TryParseExact(record.Id, "N", out _) || Path.GetFileNameWithoutExtension(file) != record.Id)
                        continue;
                    result.Add(record);
                }
                catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
                {
                }

            return result.OrderByDescending(r => r.Started).ToList();
        }
    }

    void Recover()
    {
        foreach (var record in History().Where(r => r.State is OperationState.Running or OperationState.Cancelling or OperationState.Pausing))
        {
            var alive = false;
            try
            {
                using var process = Process.GetProcessById(record.ProcessId);
                alive = process.StartTime.ToUniversalTime().Ticks == record.ProcessStartTicks && !process.HasExited;
            }
            catch
            {
            }

            if (alive)
                continue;
            record.State = record.State == OperationState.Pausing ? OperationState.Paused : record.State == OperationState.Cancelling ? OperationState.Cancelled : record.Download != null ? OperationState.Queued : OperationState.Interrupted;
            record.Committing = false;
            record.Message = record.Download != null ? "已恢复上次任务；执行时重新校验文件" : "上次进程异常结束；请重新执行原操作以校验或修复。";
            record.Updated = DateTimeOffset.UtcNow;
            AtomicFile.WriteJson(FileFor(record), record);
        }
    }
}
