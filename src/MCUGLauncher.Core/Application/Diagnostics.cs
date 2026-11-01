using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace BlockLaunch;

/// <summary>
/// 表示 DiagnosticContext 数据。
/// </summary>
public record DiagnosticContext(string Directory, string Profile, string ProcessLog = "", DateTime? StartedUtc = null, GameExit? Exit = null, string[]? Secrets = null);

/// <summary>
/// 表示 DiagnosticSource 数据。
/// </summary>
public record DiagnosticSource(string Name, string Text);

/// <summary>
/// 表示 DiagnosticFinding 数据。
/// </summary>
public record DiagnosticFinding(string Title, string Advice, string Evidence, string Source);

/// <summary>
/// 表示 DiagnosticSnapshot 数据。
/// </summary>
public record DiagnosticSnapshot(DateTime CreatedUtc, string Summary, IReadOnlyList<DiagnosticFinding> Findings, IReadOnlyList<DiagnosticSource> Logs, string Mods, string Environment, IReadOnlyList<string> Notices);

/// <summary>
/// 表示 FeedbackOptions 数据。
/// </summary>
public record FeedbackOptions(bool Logs = true, bool Mods = true, bool Environment = true);

/// <summary>
/// 表示 FeedbackDocument 数据。
/// </summary>
public record FeedbackDocument(string Name, string Text);

/// <summary>
/// 诊断快照与反馈包。
/// </summary>
/// <remarks>
/// 依赖进程日志、实例 mods 列表、环境信息与可选密钥脱敏；输出结构化 DiagnosticSnapshot 与可附带的反馈文档。
/// </remarks>
public static class Diagnostics
{
    const int MaxLogBytes = 1024 * 1024;
    static readonly TimeSpan RegexLimit = TimeSpan.FromMilliseconds(250);

    static string Replace(string text, string pattern, string replacement) => Regex.Replace(text, pattern, replacement, RegexOptions.IgnoreCase, RegexLimit);

    /// <summary>
    /// 执行 Sanitize。
    /// </summary>
    public static string Sanitize(string text, IEnumerable<string>? secrets = null)
    {
        text = Replace(text, @"\bBearer\s+[A-Za-z0-9._~+/=-]+", "Bearer [已隐藏]");
        text = Replace(text, @"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+", "[令牌已隐藏]");
        text = Replace(text, @"((?:access[_-]?token|refresh[_-]?token|authorization|x-api-key|api[_-]?key|password|client_secret)[""']?\s*[:=]\s*)(?:""[^""\r\n]*""|'[^'\r\n]*')", "$1[已隐藏]");
        text = LogPrivacy.Redact(text, secrets);
        text = Replace(text, @"(--(?:username|uuid|userProperties|clientId|xuid|accessToken|clientToken)\s+)(?:""[^""]*""|\S+)", "$1[已隐藏]");
        text = Replace(text, @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", "[邮箱已隐藏]");
        text = Replace(text, @"\b(?:\d{1,3}\.){3}\d{1,3}(?::\d{1,5})?\b", "[地址已隐藏]");
        text = Replace(text, @"(?:[A-Z]:[\\/](?:Users|Documents and Settings)[\\/]|/home/|/Users/)[^\\/\s""']+", "[用户目录]");
        text = Replace(text, @"(?<=://)[^/\s:@]+:[^/\s@]+@", "[凭据已隐藏]@");
        return text;
    }

    static void NoLink(string path)
    {
        for (var item = new DirectoryInfo(Path.GetFullPath(path)); item != null; item = item.Parent)
            if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("不读取链接或联接路径。");
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("不读取链接文件。");
    }

    /// <summary>
    /// 执行 Capture。
    /// </summary>
    public static DiagnosticSnapshot Capture(DiagnosticContext context, CancellationToken token = default)
    {
        var root = Path.GetFullPath(context.Directory);
        NoLink(root);
        var logs = new List<DiagnosticSource>();
        var notices = new List<string>();
        string Clean(string text) => Sanitize(text.Replace(root, "[实例目录]", StringComparison.OrdinalIgnoreCase), context.Secrets);
        void Read(string file, string name)
        {
            token.ThrowIfCancellationRequested();
            if (!File.Exists(file))
                return;
            try
            {
                NoLink(file);
                if (context.StartedUtc is
                    {
                    } started && File.GetLastWriteTimeUtc(file) < started.AddSeconds(-2))
                {
                    notices.Add(name + " 早于本次启动，已跳过。");
                    return;
                }

                using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var offset = Math.Max(0, input.Length - MaxLogBytes);
                input.Seek(offset, SeekOrigin.Begin);
                var buffer = new byte[MaxLogBytes];
                var count = 0;
                while (count < buffer.Length)
                {
                    token.ThrowIfCancellationRequested();
                    var read = input.Read(buffer, count, buffer.Length - count);
                    if (read == 0)
                        break;
                    count += read;
                }

                var text = Encoding.UTF8.GetString(buffer, 0, count).TrimStart('\uFEFF');
                if (offset > 0)
                {
                    var newline = text.IndexOf('\n');
                    text = newline < 0 ? "" : text[(newline + 1)..];
                    notices.Add(name + " 仅保留末尾 1 MiB。");
                }

                token.ThrowIfCancellationRequested();
                logs.Add(new(name, Clean(text)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                notices.Add(name + " 无法读取：" + Clean(ex.Message));
            }
        }

        Read(Path.Combine(root, "logs", "latest.log"), "latest.log");
        if (context.ProcessLog.Length > 0)
            Read(context.ProcessLog, "process.log");
        void Recent(string directory, string pattern, int take, string prefix)
        {
            if (!System.IO.Directory.Exists(directory))
                return;
            try
            {
                NoLink(directory);
                var files = System.IO.Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly).Take(101).ToList();
                if (files.Count > 100)
                    notices.Add(prefix + " 候选过多，仅检查前 100 个文件。");
                var index = 0;
                foreach (var file in files.Take(100).OrderByDescending(File.GetLastWriteTimeUtc).Take(take))
                    Read(file, prefix + (++index) + ".log");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                notices.Add(prefix + " 目录无法读取：" + Clean(ex.Message));
            }
        }

        Recent(Path.Combine(root, "crash-reports"), "crash-*.txt", 3, "crash-");
        Recent(root, "hs_err_pid*.log", 2, "jvm-");
        var mods = new List<string>();
        var modDir = Path.Combine(root, "mods");
        if (System.IO.Directory.Exists(modDir))
            try
            {
                NoLink(modDir);
                var candidates = System.IO.Directory.EnumerateFiles(modDir).Take(2001).ToList();
                if (candidates.Count > 2000)
                    notices.Add("模组目录候选过多，仅检查前 2000 个文件。");
                foreach (var file in candidates.Take(2000))
                {
                    token.ThrowIfCancellationRequested();
                    if (mods.Count == 2000)
                    {
                        notices.Add("模组清单已截断至 2000 个文件。");
                        break;
                    }

                    if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                        continue;
                    if (file.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase))
                        mods.Add(Clean(Path.GetFileName(file)));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                notices.Add("模组目录无法读取：" + Clean(ex.Message));
            }

        var summary = context.Exit is
        {
            Requested: true
        } ? "玩家主动请求关闭游戏，不自动判定为崩溃。" : context.Exit is
        {
            Code: 0
        } ? "游戏正常退出；下面的规则命中仅供排查参考。" : context.Exit is
        {
        } exit ? $"游戏异常退出，退出码 {exit.Code}。以下为日志规则提示，并非确定原因。" : "手动分析已有日志；日志可能来自较早的游戏运行，规则提示并非确定原因。";
        var findings = context.Exit is
        {
            Requested: true
        } ? new List<DiagnosticFinding>() : Analyze(logs);
        if (logs.Count == 0)
            notices.Add("没有找到可读取的本次日志，不能据此确定退出原因。");
        if (findings.Count == 0)
            notices.Add("未命中已知规则。请查看日志证据，不能仅凭 ERROR 或退出码断定模组冲突。");
        var environment = $"MCUG Launcher {ProductInfo.Version}\n系统：{RuntimeInformation.OSDescription}\n系统架构：{RuntimeInformation.OSArchitecture}\n启动器架构：{RuntimeInformation.ProcessArchitecture}\n.NET：{RuntimeInformation.FrameworkDescription}\n游戏配置：{Clean(context.Profile)}\n退出码：{context.Exit?.Code.ToString() ?? "未知"}\n主动关闭：{context.Exit?.Requested.ToString() ?? "未知"}\n";
        return new(DateTime.UtcNow, summary, findings, logs, string.Join('\n', mods.Order(StringComparer.OrdinalIgnoreCase)), environment, notices);
    }

    /// <summary>
    /// 执行 Analyze。
    /// </summary>
    public static List<DiagnosticFinding> Analyze(IEnumerable<DiagnosticSource> sources)
    {
        var rules = new (string Pattern, string Title, string Advice)[] {
            (@"OutOfMemoryError|Could not reserve enough space|There is insufficient memory for the Java Runtime", "可能内存不足", "关闭占用内存的程序；检查分配内存是否过大或过小，以及 Java 是否为 64 位。"),
            (@"UnsupportedClassVersionError|class file version \d+.*(?:up to|recognizes)|requires Java \d+", "可能 Java 版本不匹配", "在实例启动设置中使用与游戏和加载器要求匹配的 Java，再重试。"),
            (@"Incompatible mods found|Mod resolution encountered an incompatible mod set|Missing mandatory dependencies|requires .*(?:which is missing|not installed)", "可能缺少依赖或模组版本不兼容", "根据下方证据核对模组、依赖、游戏版本和加载器；备份后逐项调整，不要直接删除存档。"),
            (@"MixinApplyError|MixinTransformerError|InvalidMixinException|InjectionError", "可能模组注入失败", "查看证据附近的模组名称，核对兼容版本；此信号不能独自确定是哪两个模组冲突。"),
            (@"GLFW error 65542|GLFW error 65543|Failed to create (?:the )?OpenGL|does not support OpenGL", "可能显卡驱动或 OpenGL 不兼容", "从显卡厂商安装匹配的驱动，确认游戏使用正确的显卡；旧显卡可能不满足此版本要求。"),
            (@"ZipException.*(?:zip END header|error in opening zip|invalid)|Invalid or corrupt jarfile", "可能 JAR 文件损坏", "核对日志指向的文件，使用修复功能或重新下载该模组；先保留原文件以便回退。"),
            (@"No space left on device|There is not enough space on the disk|磁盘空间不足", "可能磁盘空间不足", "检查游戏目录和系统临时目录所在磁盘的可用空间，腾出空间后再重试。"),
            (@"EXCEPTION_ACCESS_VIOLATION|A fatal error has been detected by the Java Runtime", "JVM 或本机组件发生错误", "检查同一时间的 JVM 错误报告、Java 和显卡驱动；这条信号不足以确定具体根因。")
        };
        var result = new List<DiagnosticFinding>();
        foreach (var rule in rules)
        {
            foreach (var source in sources)
            {
                var line = source.Text.Split('\n').FirstOrDefault(line => Regex.IsMatch(line.Length > 8192 ? line[..8192] : line, rule.Pattern, RegexOptions.IgnoreCase, RegexLimit));
                if (line == null)
                    continue;
                result.Add(new(rule.Title, rule.Advice, Sanitize(line.Length > 600 ? line[..600] + "…" : line), source.Name));
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// 执行 Preview。
    /// </summary>
    public static IReadOnlyList<FeedbackDocument> Preview(DiagnosticSnapshot snapshot, FeedbackOptions options)
    {
        var findings = options.Logs ? string.Join("\n\n", snapshot.Findings.Select(f => $"{f.Title}\n{f.Advice}\n{f.Source}: {f.Evidence}")) : "未选日志，已省略诊断证据。";
        var documents = new List<FeedbackDocument>
        {
            new("diagnosis.txt", snapshot.Summary + "\n\n" + findings + "\n\n" + string.Join('\n', snapshot.Notices)),
            new("README.txt", "BlockLaunch 本地反馈包\n此 ZIP 不会自动上传。诊断为规则推测，请结合日志排查。\n日志为截断快照，不是完整原始文件；未收集账号缓存、配置、存档、服务器列表、模组本体或启动命令。\n已尽力过滤令牌、邮箱、IPv4 与用户目录，但不能保证完全脱敏。分享前请检查每一项文本；日志仍可能包含聊天、玩家名、服务器域名或其他私人信息。\n")
        };
        if (options.Logs)
            documents.AddRange(snapshot.Logs.Select(s => new FeedbackDocument("logs/" + s.Name, s.Text)));
        if (options.Mods)
            documents.Add(new("mods.txt", snapshot.Mods));
        if (options.Environment)
            documents.Add(new("environment.txt", snapshot.Environment));
        // These exact frozen texts are also shown by the UI before export.
        return documents;
    }

    /// <summary>
    /// 导出。
    /// </summary>
    public static async Task Export(IReadOnlyList<FeedbackDocument> documents, string target, CancellationToken token = default)
    {
        if (documents.Count > 16 || documents.Sum(d => (long)Encoding.UTF8.GetByteCount(d.Text)) > 16 * 1024 * 1024)
            throw new IOException("反馈快照超过 16 MiB 限制。");
        if (documents.Any(d => !Regex.IsMatch(d.Name, @"^(?:logs/)?[A-Za-z0-9_-]+\.(?:txt|log)$")) || documents.Select(d => d.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != documents.Count)
            throw new IOException("反馈文件名无效。");
        target = Path.GetFullPath(target);
        var directory = Path.GetDirectoryName(target)!;
        NoLink(directory);
        System.IO.Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, ".blocklaunch-feedback-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var zip = new ZipArchive(new FileStream(temp, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None), ZipArchiveMode.Create))
                foreach (var document in documents)
                {
                    token.ThrowIfCancellationRequested();
                    await using var output = zip.CreateEntry(document.Name, CompressionLevel.Optimal).Open();
                    var bytes = Encoding.UTF8.GetBytes(document.Text);
                    await output.WriteAsync(bytes, token);
                }

            token.ThrowIfCancellationRequested();
            File.Move(temp, target, true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }
}
