using System.Net;
using System.Security.Cryptography;
using CmlLib.Core;
using CmlLib.Core.Files;
using CmlLib.Core.Installers;

namespace BlockLaunch.Downloads;

/// <summary>
/// 把 CmlLib 的 GameFile 列表映射成 <see cref="DownloadTask"/>，交给高速引擎下载，同时保留 Queued/Done 与字节进度事件。
/// </summary>
/// <remarks>
/// 复刻 <see cref="ParallelGameInstaller"/> 的行为：用基类 <c>NeedUpdate</c> 判定 SHA1，下载后执行
/// <see cref="GameFile.ExecuteUpdateTask"/>（原生库解压等附加任务不能漏），否则实例会缺文件。
/// 与并行安装器的差异是并发由引擎统一限制，并且分片下载带哈希校验与断点续传。
/// </remarks>
internal sealed class FastGameInstaller : GameInstallerBase
{
    readonly DownloadEngine engine;
    readonly DownloadOptions options;
    readonly Action<string>? message;

    public FastGameInstaller(HttpClient http, DownloadOptions options, Action<string>? message = null, Action<string>? log = null)
        : base(http)
    {
        this.options = options;
        engine = new(http, options, log);
        this.message = message;
    }

    /// <summary>
    /// 下载并落盘全部版本文件。
    /// </summary>
    protected override async ValueTask Install(IEnumerable<GameFile> gameFiles, CancellationToken cancellationToken)
    {
        var files = gameFiles
            .Where(file => !string.IsNullOrEmpty(file.Path) && !IsExcludedPath(file.Path!))
            .ToList();
        var total = files.Count;
        var totalBytes = files.Sum(file => Math.Max(0, file.Size));
        foreach (var file in files)
            FireFileProgress(total, 0, file.Name, InstallerEventType.Queued);

        var downloads = new List<(GameFile File, DownloadTask Task)>();
        foreach (var file in files)
        {
            if (!NeedUpdate(file) || string.IsNullOrEmpty(file.Url))
                continue;
            downloads.Add((file, new()
            {
                Id = file.Name,
                Kind = DownloadKind.Game,
                TargetPath = file.Path!,
                Sources = SourcesFor(file.Url),
                Hash = file.Hash,
                Algorithm = HashAlgorithmName.SHA1,
                Size = file.Size,
                Priority = file.Name.StartsWith("assets/", StringComparison.OrdinalIgnoreCase) ? 0 : 1
            }));
        }

        var changed = new HashSet<string>(downloads.Select(item => item.Task.TargetPath), StringComparer.OrdinalIgnoreCase);
        var completed = 0;
        var finishedBytes = files.Where(file => !changed.Contains(file.Path!)).Sum(file => Math.Max(0, file.Size));
        var reporter = new InlineProgress<DownloadProgress>(update =>
        {
            if (update.Phase == DownloadPhase.Completed)
            {
                var finished = Interlocked.Increment(ref completed);
                Interlocked.Add(ref finishedBytes, Math.Max(0, update.TotalBytes));
                FireFileProgress(total, finished, Path.GetFileName(update.FilePath), InstallerEventType.Done);
            }

            var progressed = update.Phase == DownloadPhase.Downloading
                ? Volatile.Read(ref finishedBytes) + Math.Max(0, update.CompletedBytes)
                : Volatile.Read(ref finishedBytes);
            FireByteProgress(new ByteProgress(totalBytes, Math.Min(totalBytes, Math.Max(0, progressed))));
            message?.Invoke(Describe(update, Volatile.Read(ref completed), total));
        });

        if (downloads.Count > 0)
            await engine.DownloadAllAsync(downloads.Select(item => item.Task).ToList(), reporter, cancellationToken);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await file.ExecuteUpdateTask(cancellationToken);
            if (!changed.Contains(file.Path!))
            {
                // 已存在的文件不需要下载，但 CmlLib 仍要求报告 Done，否则总进度永远差几个文件。
                var finished = Interlocked.Increment(ref completed);
                FireFileProgress(total, finished, file.Name, InstallerEventType.Done);
            }
        }
    }

    IReadOnlyList<DownloadSource> SourcesFor(string url)
    {
        var uri = new Uri(url);
        var mirror = NetworkPolicyHandler.Mirror(uri);
        if (mirror is null)
            return [DownloadSource.Official(uri)];
        return options.Preference == DownloadSourcePreference.Mirror
            ? [DownloadSource.Mirror("BMCLAPI", mirror), DownloadSource.Official(uri)]
            : [DownloadSource.Official(uri), DownloadSource.Mirror("BMCLAPI", mirror)];
    }

    static string Describe(DownloadProgress update, int completed, int total)
    {
        var parts = new List<string>
        {
            Path.GetFileName(update.FilePath)
        };
        if (update.Phase == DownloadPhase.Downloading && update.TotalBytes > 0)
            parts.Add($"{DownloadProgress.FormatBytes(update.CompletedBytes)} / {DownloadProgress.FormatBytes(update.TotalBytes)}");
        if (update.Phase == DownloadPhase.Downloading && update.BytesPerSecond > 0)
            parts.Add(DownloadProgress.FormatSpeed(update.BytesPerSecond));
        if (update.Phase == DownloadPhase.Downloading && update.Remaining is { } remaining && remaining.TotalSeconds is > 0 and < 86400)
            parts.Add("剩余 " + DownloadProgress.FormatDuration(remaining));
        if (update.Phase == DownloadPhase.Completed)
            parts.Add("完成");
        parts.Add($"{completed}/{total}");
        if (!string.IsNullOrEmpty(update.Source))
            parts.Add(update.Source);
        if (update.Retry > 0)
            parts.Add($"重试 {update.Retry}");
        return string.Join(" · ", parts);
    }
}
