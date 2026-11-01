namespace BlockLaunch;

/// <summary>
/// 提供 GameDefaults 相关功能。
/// </summary>
public static class GameDefaults
{
    /// <summary>
    /// 解析根目录。
    /// </summary>
    public static string ResolveRoot(string input, bool requireExisting = false)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new IOException("请选择游戏文件夹。");
        var root = Path.GetFullPath(input.Trim().Trim('"'));
        if (File.Exists(root))
            throw new IOException("请选择游戏文件夹，不是文件。");
        if (requireExisting && !Directory.Exists(root))
            throw new IOException("游戏文件夹不存在，请重新选择。");
        if (Path.GetFileName(root).Equals("versions", StringComparison.OrdinalIgnoreCase))
            root = Path.GetDirectoryName(root)!;
        if (!Directory.Exists(Path.Combine(root, "versions")) && Directory.Exists(Path.Combine(root, ".minecraft")))
            root = Path.Combine(root, ".minecraft");
        return root;
    }

    // Only seed a missing language; a player's later language choice survives launches and repairs.
    /// <summary>
    /// 确保Chinese。
    /// </summary>
    public static void EnsureChinese(string directory, string minecraft, bool overwrite = false)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "options.txt");
        var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
        if (!overwrite && lines.Any(line => line.StartsWith("lang:", StringComparison.Ordinal)))
            return;
        lines.RemoveAll(line => line.StartsWith("lang:", StringComparison.Ordinal));
        var legacy = Version.TryParse(minecraft, out var version) && version < new Version(1, 11);
        lines.Add("lang:" + (legacy ? "zh_CN" : "zh_cn"));
        var temporary = path + ".blocklaunch-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllLines(temporary, lines, new System.Text.UTF8Encoding(false));
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
