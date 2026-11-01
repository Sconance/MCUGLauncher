using System.Text.RegularExpressions;

namespace BlockLaunch;

/// <summary>
/// 表示 JavaInstallation 数据。
/// </summary>
public record JavaInstallation(string Path, int Major, string Architecture, string Vendor, string Error)
{
    public string Label => $"Java {(Major == 0 ? "?" : Major)} · {Architecture} · {Vendor}\n{Path}" + (Error.Length > 0 ? "\n" + Error : "");
}

/// <summary>
/// 提供 JavaDiscovery 相关功能。
/// </summary>
public static partial class JavaDiscovery
{
    /// <summary>
    /// 执行 Architecture。
    /// </summary>
    public static string Architecture(string file)
    {
        try
        {
            using var input = File.OpenRead(file);
            using var reader = new BinaryReader(input);
            if (reader.ReadUInt16() != 0x5a4d)
                return "未知";
            input.Position = 0x3c;
            var offset = reader.ReadInt32();
            if (offset < 0 || offset > input.Length - 6)
                return "未知";
            input.Position = offset;
            if (reader.ReadUInt32() != 0x4550)
                return "未知";
            return reader.ReadUInt16() switch
            {
                0x8664 => "x64",
                0x14c => "x86",
                0xaa64 => "ARM64",
                _ => "未知"
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "未知";
        }
    }

    /// <summary>
    /// 扫描。
    /// </summary>
    public static async Task<List<JavaInstallation>> Scan(string gameRoot, IEnumerable<string> extras, CancellationToken token = default)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string path)
        {
            try
            {
                if (File.Exists(path))
                    paths.Add(System.IO.Path.GetFullPath(path));
            }
            catch (ArgumentException)
            {
            }
        }

        foreach (var path in extras.Where(p => !string.IsNullOrWhiteSpace(p)))
            Add(path);
        foreach (var home in new[] {
            Environment.GetEnvironmentVariable("JAVA_HOME"),
            Environment.GetEnvironmentVariable("JDK_HOME")
        }

        )
            if (!string.IsNullOrWhiteSpace(home))
                Add(System.IO.Path.Combine(home, "bin", "java.exe"));
        foreach (var path in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(System.IO.Path.PathSeparator))
            if (path.Length > 0)
                Add(System.IO.Path.Combine(path, "java.exe"));
        var roots = new[] {
            System.IO.Path.Combine(gameRoot, "runtime"),
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".jdks")
        }.ToList();
        foreach (var basis in new[] {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        }

        )
            foreach (var vendor in new[] {
                "Java",
                "Microsoft",
                "Eclipse Adoptium",
                "AdoptOpenJDK",
                "Zulu",
                "BellSoft",
                "Amazon Corretto"
            }

            )
                roots.Add(System.IO.Path.Combine(basis, vendor));
        var visited = 0;
        void Walk(string folder, int depth)
        {
            token.ThrowIfCancellationRequested();
            if (depth > 5 || visited++ > 3000 || !Directory.Exists(folder))
                return;
            try
            {
                if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
                    return;
                Add(System.IO.Path.Combine(folder, "java.exe"));
                foreach (var child in Directory.EnumerateDirectories(folder))
                    Walk(child, depth + 1);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        roots.Add(System.IO.Path.Combine(roaming, ".minecraft", "runtime"));
        roots.Add(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", "Microsoft.4297127D64EC6_8wekyb3d8bbwe", "LocalCache", "Local", "runtime"));
        foreach (var folder in roots)
        {
            visited = 0;
            Walk(folder, 0);
        }

        return await InspectCandidates(paths, token);
    }

    /// <summary>
    /// 执行 InspectCandidates。
    /// </summary>
    public static async Task<List<JavaInstallation>> InspectCandidates(IEnumerable<string> candidates, CancellationToken token = default)
    {
        var result = new List<JavaInstallation>();
        foreach (var path in candidates.Distinct(StringComparer.OrdinalIgnoreCase).Take(150))
        {
            token.ThrowIfCancellationRequested();
            var vendor = "未知发行商";
            try
            {
                var release = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(path))!, "release");
                if (File.Exists(release) && new FileInfo(release).Length < 65536)
                    vendor = Regex.Match(File.ReadAllText(release), "(?m)^IMPLEMENTOR=\"([^\"]+)\"").Groups[1].Value;
                result.Add(new(path, await ProbeMajor(path, token), Architecture(path), vendor, ""));
            }
            catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                result.Add(new(path, 0, Architecture(path), vendor, ex.Message));
            }
        }

        return result.OrderByDescending(j => j.Architecture == "x64").ThenByDescending(j => j.Major).ToList();
    }
}
