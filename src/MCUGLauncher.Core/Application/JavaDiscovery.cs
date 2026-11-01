using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BlockLaunch;

/// <summary>
/// Java 查找与探测。
/// </summary>
/// <remarks>
/// 依赖本机已安装的 Java、实例版本 JSON 与 javaw.exe 探测；结果按主版本缓存，避免重复启动进程。
/// </remarks>
public static partial class JavaDiscovery
{
    static readonly Dictionary<int, string?> Cache = new();

    /// <summary>
    /// 执行 RequiredMajor。
    /// </summary>
    /// <param name="root">游戏根目录</param>
    /// <param name="profile">实例名</param>
    /// <returns>该实例要求的主版本号</returns>
    public static int RequiredMajor(string root, string profile)
    {
        var seen = new HashSet<string>();
        while (seen.Count < 16 && seen.Add(profile))
        {
            ContentService.SafeFile(profile);
            var file = Path.Combine(root, "versions", profile, profile + ".json");
            if (!File.Exists(file))
                return 0;
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var json = doc.RootElement;
            if (json.TryGetProperty("javaVersion", out var java) && java.TryGetProperty("majorVersion", out var major))
                return major.GetInt32();
            if (!json.TryGetProperty("inheritsFrom", out var parent))
            {
                var minecraft = ProfileInspector.Read(root, profile).Minecraft;
                var release = Regex.Match(minecraft, @"^1\.(?<minor>\d+)(?:\.(?<patch>\d+))?$");
                if (!release.Success || !int.TryParse(release.Groups["minor"].Value, out var minor) || minor > 21)
                    return 0;
                int.TryParse(release.Groups["patch"].Value, out var patch);
                return minor == 21 || minor == 20 && patch >= 5 ? 21 : minor >= 18 ? 17 : minor == 17 ? 16 : 8;
            }

            profile = parent.GetString()!;
        }

        return 0;
    }

    /// <summary>
    /// 查找Compatible。
    /// </summary>
    /// <param name="root">游戏根目录</param>
    /// <param name="profile">实例名</param>
    /// <param name="token">取消令牌</param>
    /// <returns>兼容的 Java 路径；未找到时为 null</returns>
    public static async Task<string?> FindCompatible(string root, string profile, CancellationToken token)
    {
        var required = RequiredMajor(root, profile);
        if (required == 0)
            return null;
        return (await Scan(root, Array.Empty<string>(), token)).FirstOrDefault(j => j.Major == required && j.Error.Length == 0)?.Path;
    }

    /// <summary>
    /// 查找Major。
    /// </summary>
    /// <param name="required">要求的主版本号</param>
    /// <param name="token">取消令牌</param>
    /// <returns>匹配的 Java 路径；未找到时为 null</returns>
    public static async Task<string?> FindMajor(int required, CancellationToken token)
    {
        if (required == 0)
            return null;
        lock (Cache)
            if (Cache.TryGetValue(required, out var cached) && cached != null && File.Exists(cached))
                return cached;
        var match = (await Scan("", Array.Empty<string>(), token)).FirstOrDefault(j => j.Major == required && j.Error.Length == 0);
        if (match != null)
        {
            lock (Cache)
                Cache[required] = match.Path;
            return match.Path;
        }

        return null;
    }

    /// <summary>
    /// 执行 ProbeMajor。
    /// </summary>
    /// <param name="java">Java 可执行文件路径</param>
    /// <param name="token">取消令牌</param>
    /// <returns>探测到的主版本号；失败时为 0</returns>
    public static async Task<int> ProbeMajor(string java, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (Path.GetFileName(java).Equals("javaw.exe", StringComparison.OrdinalIgnoreCase))
        {
            var console = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(java))!, "java.exe");
            if (File.Exists(console))
                java = console;
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(java)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            }
        };
        process.StartInfo.ArgumentList.Add("-version");
        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new IOException("无法运行指定 Java，请重新选择 java.exe 或 javaw.exe。", ex);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            var text = await error + await output;
            var match = Regex.Match(text, @"version\s+""(?<major>\d+)(?:\.(?<minor>\d+))?");
            if (process.ExitCode != 0 || !match.Success)
                throw new IOException("指定程序未返回有效 Java 版本，请重新选择 Java。");
            var major = int.Parse(match.Groups["major"].Value);
            return major == 1 ? int.Parse(match.Groups["minor"].Value) : major;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(true);
            await process.WaitForExitAsync();
            try
            {
                await Task.WhenAll(error, output);
            }
            catch (OperationCanceledException)
            {
            }

            token.ThrowIfCancellationRequested();
            throw new IOException("检测 Java 超时，请检查指定的可执行文件。");
        }
    }
}
