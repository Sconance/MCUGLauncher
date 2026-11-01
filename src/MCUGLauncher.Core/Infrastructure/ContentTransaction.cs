using System.Security.Cryptography;

namespace BlockLaunch;

/// <summary>
/// 提供 TransactionMove 相关功能。
/// </summary>
public class TransactionMove
{
    /// <summary>
    /// 获取或设置 Relative。
    /// </summary>
    public string Relative { get; set; } = "";
    /// <summary>
    /// 获取或设置 Hash。
    /// </summary>
    public string Hash { get; set; } = "";
    public bool Backup
    {
        get;
        set;
    }
}

/// <summary>
/// 提供 TransactionJournal 相关功能。
/// </summary>
public class TransactionJournal
{
    /// <summary>
    /// 获取或设置 状态。
    /// </summary>
    public string State { get; set; } = "Preparing";
    /// <summary>
    /// 获取或设置 Moves。
    /// </summary>
    public List<TransactionMove> Moves { get; set; } = [];
}

// The per-instance operation.lock must be held by the caller.
/// <summary>
/// 提供 内容Transaction 相关功能。
/// </summary>
public sealed class ContentTransaction
{
    readonly string instance, directory, journalPath;
    readonly TransactionJournal journal;

    public string Stage
    {
        get;
    }

    public ContentTransaction(string instance)
    {
        this.instance = Path.GetFullPath(instance);
        directory = Path.Combine(instance, ".blocklaunch", "backups", "transaction-" + Guid.NewGuid().ToString("N"));
        Stage = Path.Combine(directory, "stage");
        Directory.CreateDirectory(Stage);
        journalPath = Path.Combine(directory, "transaction.json");
        journal = new();
        Save();
    }

    ContentTransaction(string instance, string directory, TransactionJournal journal)
    {
        this.instance = instance;
        this.directory = directory;
        this.journal = journal;
        Stage = Path.Combine(directory, "stage");
        journalPath = Path.Combine(directory, "transaction.json");
    }

    string Destination(string relative)
    {
        if (relative != ".blocklaunch-content.json" && !(relative.StartsWith("mods/", StringComparison.Ordinal) || relative.StartsWith("resourcepacks/", StringComparison.Ordinal) || relative.StartsWith("shaderpacks/", StringComparison.Ordinal) || relative.StartsWith("datapacks/", StringComparison.Ordinal)))
            throw new IOException("事务目标不在内容目录中。");
        return PackService.SafePath(instance, relative);
    }

    void Save() => AtomicFile.WriteJson(journalPath, journal);

    /// <summary>
    /// 判断是否包含。
    /// </summary>
    public static string Hash(string file)
    {
        using var input = File.OpenRead(file);
        return Convert.ToHexString(SHA512.HashData(input));
    }

    /// <summary>
    /// 备份。
    /// </summary>
    public void Backup(string relative)
    {
        var file = Destination(relative);
        if (!File.Exists(file))
            return;
        journal.Moves.Add(new()
        {
            Relative = relative,
            Backup = true,
            Hash = Hash(file)
        });
        Save();
        var backup = PackService.SafePath(Path.Combine(directory, "files"), relative);
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        File.Move(file, backup);
    }

    /// <summary>
    /// 写入。
    /// </summary>
    public void Write(string source, string relative)
    {
        var target = Destination(relative);
        if (File.Exists(target))
            throw new IOException("事务不能覆盖未备份的文件。");
        journal.Moves.Add(new()
        {
            Relative = relative,
            Hash = Hash(source)
        });
        Save();
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Move(source, target);
    }

    /// <summary>
    /// 执行 Commit。
    /// </summary>
    public void Commit()
    {
        journal.State = "Committed";
        Save();
        try
        {
            CleanupStage();
        }
        catch (IOException)
        {
        }
    }

    void CleanupStage()
    {
        if (Directory.Exists(Stage))
            Directory.Delete(Stage, true);
    }

    /// <summary>
    /// 执行 Rollback。
    /// </summary>
    public void Rollback()
    {
        foreach (var move in journal.Moves.AsEnumerable().Reverse())
        {
            var target = Destination(move.Relative);
            if (!move.Backup)
            {
                if (journal.Moves.Any(m => m.Backup && m.Relative == move.Relative) && !File.Exists(PackService.SafePath(Path.Combine(directory, "files"), move.Relative)))
                    continue;
                if (!File.Exists(target))
                    continue;
                if (!Hash(target).Equals(move.Hash, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("恢复遇到已被用户修改的文件，已保留：" + move.Relative);
                File.Delete(target);
            }
            else
            {
                var backup = PackService.SafePath(Path.Combine(directory, "files"), move.Relative);
                if (!File.Exists(backup))
                    continue;
                if (File.Exists(target))
                    throw new IOException("恢复目标已存在，已保留备份：" + move.Relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(backup, target);
            }
        }

        journal.State = "RolledBack";
        Save();
        CleanupStage();
    }

    /// <summary>
    /// 执行 LatestCommitted。
    /// </summary>
    public static string? LatestCommitted(string instance)
    {
        var directory = Path.Combine(instance, ".blocklaunch", "backups");
        if (!Directory.Exists(directory))
            return null;
        return Directory.GetDirectories(directory, "transaction-*").Where(d => File.Exists(Path.Combine(d, "transaction.json"))).OrderByDescending(d => File.GetLastWriteTimeUtc(Path.Combine(d, "transaction.json"))).FirstOrDefault(d =>
        {
            var journal = AtomicFile.ReadJson<TransactionJournal>(Path.Combine(d, "transaction.json"));
            return journal.State == "Committed" && journal.Moves.Count > 0;
        });
    }

    /// <summary>
    /// 恢复Latest。
    /// </summary>
    public static void RestoreLatest(string instance, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        using var lease = ContentService.LockInstance(instance);
        var directory = LatestCommitted(instance) ?? throw new IOException("没有可回退的内容安装记录。");
        var journal = AtomicFile.ReadJson<TransactionJournal>(Path.Combine(directory, "transaction.json"));
        var transaction = new ContentTransaction(instance, directory, journal);
        // Validate the entire restore before touching any file. A later edit must never be lost.
        foreach (var move in journal.Moves)
        {
            token.ThrowIfCancellationRequested();
            var target = transaction.Destination(move.Relative);
            if (!move.Backup)
            {
                if (!File.Exists(target) || !Hash(target).Equals(move.Hash, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("安装后的文件已改变，不能自动回退：" + move.Relative);
            }
            else
            {
                var backup = PackService.SafePath(Path.Combine(directory, "files"), move.Relative);
                if (!File.Exists(backup) || !Hash(backup).Equals(move.Hash, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("回退备份缺失或已改变：" + move.Relative);
                if ((File.Exists(target) || Directory.Exists(target)) && !journal.Moves.Any(m => !m.Backup && m.Relative == move.Relative))
                    throw new IOException("回退目标被占用：" + move.Relative);
            }
        }

        using var commit = OperationExecution.BeginCommit();
        journal.State = "Restoring";
        transaction.Save();
        transaction.Rollback();
    }

    /// <summary>
    /// 执行 Recover。
    /// </summary>
    public static List<string> Recover(string instance)
    {
        var messages = new List<string>();
        var directory = Path.Combine(instance, ".blocklaunch", "backups");
        if (!Directory.Exists(directory))
            return messages;
        var lockPath = Path.Combine(instance, ".blocklaunch", "operation.lock");
        using var lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        foreach (var transaction in Directory.GetDirectories(directory, "transaction-*"))
        {
            var file = Path.Combine(transaction, "transaction.json");
            if (!File.Exists(file))
                continue;
            try
            {
                var record = AtomicFile.ReadJson<TransactionJournal>(file);
                if (record.State is not ("Preparing" or "Restoring"))
                    continue;
                new ContentTransaction(instance, transaction, record).Rollback();
                messages.Add("已回滚中断的内容安装：" + Path.GetFileName(instance));
            }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
            {
                messages.Add("内容安装需要检查备份：" + ex.Message);
            }
        }

        return messages;
    }
}
