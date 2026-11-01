namespace BlockLaunch;

/// <summary>
/// 表示 JavaSearchProgress 数据。
/// </summary>
public sealed record JavaSearchProgress(int Directories, int Candidates, string CurrentDirectory);

/// <summary>
/// 表示 JavaSearch结果 数据。
/// </summary>
public sealed record JavaSearchResult(IReadOnlyList<string> Paths, int Directories, bool LimitReached);

/// <summary>
/// 提供 JavaDiscovery 相关功能。
/// </summary>
public static partial class JavaDiscovery
{
    /// <summary>Opt-in, read-only discovery. Never follows junctions or executes files during traversal.</summary>
    public static JavaSearchResult SearchDirectories(IEnumerable<string> roots, CancellationToken token = default, IProgress<JavaSearchProgress>? progress = null, int maxDirectories = 200000, TimeSpan? budget = null)
    {
        var queue = new Queue<string>(roots.Where(p => !string.IsNullOrWhiteSpace(p)));
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var limit = budget ?? TimeSpan.FromMinutes(2);
        var visited = 0;
        var partial = false;
        while (queue.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            if (visited >= maxDirectories || clock.Elapsed > limit || found.Count >= 150)
            {
                partial = true;
                break;
            }

            var directory = queue.Dequeue();
            try
            {
                directory = Path.GetFullPath(directory);
                if (!seen.Add(directory) || !Directory.Exists(directory))
                    continue;
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                    continue;
                visited++;
                var console = Path.Combine(directory, "java.exe");
                var windowed = Path.Combine(directory, "javaw.exe");
                if (File.Exists(console))
                    found.Add(console);
                else if (File.Exists(windowed))
                    found.Add(windowed);
                if (visited % 100 == 0)
                    progress?.Report(new(visited, found.Count, directory));
                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    token.ThrowIfCancellationRequested();
                    if (queue.Count + seen.Count >= maxDirectories)
                    {
                        partial = true;
                        break;
                    }

                    queue.Enqueue(child);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
            {
            }
        }

        progress?.Report(new(visited, found.Count, ""));
        return new(found.Order(StringComparer.OrdinalIgnoreCase).ToList(), visited, partial);
    }

    /// <summary>
    /// 执行 FixedDriveRoots。
    /// </summary>
    public static IEnumerable<string> FixedDriveRoots() => DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady).Select(d => d.RootDirectory.FullName);
}
