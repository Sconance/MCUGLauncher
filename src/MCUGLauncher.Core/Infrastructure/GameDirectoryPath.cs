namespace BlockLaunch;

/// <summary>规范化游戏根目录并提供与运行平台一致的路径比较规则；不读写应用配置。</summary>
public static class GameDirectoryPath
{
    public static StringComparer Comparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path.Trim()))
            throw new ArgumentException("游戏目录必须是绝对路径。");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));
    }
}
