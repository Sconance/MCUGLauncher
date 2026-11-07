namespace BlockLaunch;

/// <summary>
/// 表示 PackCommitRecord 数据。
/// </summary>
public record PackCommitRecord(string Alias, string State);

/// <summary>
/// 提供 InstallationRecovery 相关功能。
/// </summary>
public static class InstallationRecovery
{
    /// <summary>
    /// 执行 Recover。
    /// </summary>
    public static List<string> Recover(string root)
    {
        var messages = new List<string>();
        var dataDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(root)
        };
        var instances = Path.Combine(root, "instances");
        if (Directory.Exists(instances))
            foreach (var instance in Directory.GetDirectories(instances))
                try
                {
                    messages.AddRange(ContentTransaction.Recover(instance));
                    var actual = new InstanceCatalog(root).DirectoryFor(Path.GetFileName(instance));
                    dataDirectories.Add(Path.GetFullPath(actual));
                    if (!Path.GetFullPath(actual).Equals(Path.GetFullPath(instance), StringComparison.OrdinalIgnoreCase))
                        messages.AddRange(ContentTransaction.Recover(actual));
                }
                catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
                {
                    messages.Add("实例暂时无法恢复，已保留数据：" + Path.GetFileName(instance) + "：" + ex.Message);
                }

        if (Directory.Exists(Path.Combine(root, ".blocklaunch", "backups")))
            messages.AddRange(ContentTransaction.Recover(root));
        foreach (var data in dataDirectories)
        {
            var saves = Path.Combine(data, "saves");
            if (!Directory.Exists(saves))
                continue;
            foreach (var world in Directory.GetDirectories(saves))
                if (Directory.Exists(Path.Combine(world, ".blocklaunch", "backups")))
                    try
                    {
                        messages.AddRange(ContentTransaction.Recover(world));
                    }
                    catch (IOException ex)
                    {
                        messages.Add("存档数据包恢复暂缓：" + ex.Message);
                    }
        }

        var staging = Path.Combine(root, ".blocklaunch-staging");
        if (!Directory.Exists(staging))
            return messages;
        foreach (var stage in Directory.GetDirectories(staging))
        {
            var file = Path.Combine(stage, "pack-operation.json");
            if (!File.Exists(file))
                continue;
            try
            {
                using var lease = new FileStream(Path.Combine(stage, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                var journal = AtomicFile.ReadJson<PackCommitRecord>(file);
                if (journal.State != "Preparing")
                    continue;
                var alias = ContentService.SafeFile(journal.Alias);
                if (!alias.StartsWith("pack-", StringComparison.Ordinal) || alias.Length != 17)
                    throw new IOException("整合包恢复记录无效。");
                var instance = Path.Combine(root, "instances", alias);
                var profile = Path.Combine(root, "versions", alias, alias + ".json");
                if (File.Exists(profile))
                {
                    AtomicFile.WriteJson(file, journal with
                    {
                        State = "Committed"
                    });
                    messages.Add("整合包提交已完成，已保留实例：" + alias);
                    continue;
                }

                if (Directory.Exists(instance))
                {
                    var record = AtomicFile.ReadJson<InstalledPack>(Path.Combine(instance, ".blocklaunch-pack.json"));
                    if (record.InstanceId != alias)
                        throw new IOException("孤立实例身份不匹配，已保留数据。");
                    Directory.Move(instance, Path.Combine(stage, "recovered-instance"));
                }

                AtomicFile.WriteJson(file, journal with
                {
                    State = "Interrupted"
                });
                messages.Add("已隔离未提交整合包，请重新导入：" + alias);
            }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
            {
                messages.Add("整合包暂存需要检查或正在使用：" + ex.Message);
            }
        }

        return messages;
    }
}
