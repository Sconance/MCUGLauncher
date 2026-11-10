using System.Diagnostics;
using System.Text.Json.Serialization;

namespace BlockLaunch;

// Persisted task models. Keep property names and enum values compatible with existing queues.
[JsonConverter(typeof(JsonStringEnumConverter))]
/// <summary>
/// 定义 Operation状态 的取值。
/// </summary>
public enum OperationState
{
    Running,
    Cancelling,
    Completed,
    Cancelled,
    Failed,
    Interrupted,
    Queued,
    Pausing,
    Paused
}

// Optional instance maintenance payload; older queue records remain compatible.
public sealed class InstanceOperationRequest
{
    public string Key { get; set; } = "";
    public string Directory { get; set; } = "";
    public string Name { get; set; } = "";
    public PackRequirements Requirements { get; set; } = new("", "vanilla", "");
    public LaunchSettings Settings { get; set; } = new();
    public CloneOptions Clone { get; set; } = new();
    public ExportOptions Export { get; set; } = new(true, true, true, true, false);
    public UninstallPlan? Uninstall
    {
        get; set;
    }
}

/// <summary>
/// 提供 下载Request 相关功能。
/// </summary>
public sealed class DownloadRequest
{
    public InstanceOperationRequest? Instance
    {
        get; set;
    }
    /// <summary>
    /// 获取或设置 Kind。
    /// </summary>
    public string Kind { get; set; } = "game";
    /// <summary>
    /// 获取或设置自定义文件的下载地址（仅 Kind 为 file 时使用）。
    /// </summary>
    public string Url { get; set; } = "";
    /// <summary>
    /// 获取或设置 版本。
    /// </summary>
    public string Version { get; set; } = "";
    /// <summary>
    /// 获取或设置 Loader。
    /// </summary>
    public string Loader { get; set; } = "vanilla";
    /// <summary>
    /// 获取或设置 Loader版本。
    /// </summary>
    public string LoaderVersion { get; set; } = "";
    /// <summary>
    /// 获取或设置 Java。
    /// </summary>
    public string Java { get; set; } = "";
    /// <summary>
    /// 获取或设置 Destination。
    /// </summary>
    public string Destination { get; set; } = "";
    /// <summary>
    /// 获取或设置 文件。
    /// </summary>
    public string File { get; set; } = "";
    /// <summary>
    /// 获取或设置 SourceHash。
    /// </summary>
    public string SourceHash { get; set; } = "";
    /// <summary>
    /// 获取或设置 Pack实例。
    /// </summary>
    public string PackInstance { get; set; } = "pack-" + Guid.NewGuid().ToString("N")[..12];
    /// <summary>
    /// 获取或设置 PackDisplay名称。
    /// </summary>
    public string PackDisplayName { get; set; } = "";
    /// <summary>
    /// 获取或设置 Game版本。
    /// </summary>
    public string GameVersion { get; set; } = "";
    public bool Optional
    {
        get;
        set;
    }
    public ContentVersion? Content
    {
        get;
        set;
    }
    public InstallPlan? PreparedPlan
    {
        get;
        set;
    }
    public string? ContentFingerprint
    {
        get;
        set;
    }
    /// <summary>
    /// 获取或设置 ResourceKind。
    /// </summary>
    public string ResourceKind { get; set; } = "mod";
    /// <summary>
    /// 获取或设置 备份实例Directory。
    /// </summary>
    public string BackupInstanceDirectory { get; set; } = "";
}

/// <summary>
/// 提供 OperationRecord 相关功能。
/// </summary>
public class OperationRecord
{
    /// <summary>
    /// 获取或设置 ID。
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    /// <summary>
    /// 获取或设置 根目录。
    /// </summary>
    public string Root { get; set; } = "";
    /// <summary>
    /// 获取或设置 Target。
    /// </summary>
    public string Target { get; set; } = "";
    /// <summary>
    /// 获取或设置 消息。
    /// </summary>
    public string Message { get; set; } = "";
    public OperationState State
    {
        get;
        set;
    }
    /// <summary>
    /// 获取或设置 Started。
    /// </summary>
    public DateTimeOffset Started { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// 获取或设置 Updated。
    /// </summary>
    public DateTimeOffset Updated { get; set; } = DateTimeOffset.UtcNow;
    public double? Percent
    {
        get;
        set;
    }
    /// <summary>
    /// 获取或设置 ProcessID。
    /// </summary>
    public int ProcessId { get; set; } = Environment.ProcessId;
    /// <summary>
    /// 获取或设置 ProcessStartTicks。
    /// </summary>
    public long ProcessStartTicks { get; set; } = Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks;
    public bool Committing
    {
        get;
        set;
    }
    public DownloadRequest? Download
    {
        get;
        set;
    }
    public long QueueOrder
    {
        get;
        set;
    }

    public string StateLabel => State switch
    {
        OperationState.Running => "进行中",
        OperationState.Cancelling => "正在取消",
        OperationState.Pausing => "正在暂停",
        OperationState.Paused => "已暂停",
        OperationState.Queued => "排队中",
        OperationState.Completed => "已完成",
        OperationState.Cancelled => "已取消",
        OperationState.Failed => "失败",
        _ => "上次中断"
    };

    public string Summary => $"{Name} · {StateLabel} · {Started.ToLocalTime():MM-dd HH:mm:ss}";

    public string Detail => $"{Target}  {Message}";
}
