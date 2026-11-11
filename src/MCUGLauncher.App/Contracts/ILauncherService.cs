using System.Text.Json;

namespace MCUGLauncher.Contracts;

/// <summary>Marks an interface operation that returns its result directly as Task&lt;T&gt;.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class FeatureOperationAttribute : Attribute
{
}

/// <summary>Associates a feature handler with a marked service operation.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class FeatureHandlerAttribute(string operation) : Attribute
{
    public string Operation { get; } = operation;
}

internal interface IFeatureRequestInvoker
{
    Task<TResponse> InvokeFeatureAsync<TResponse>(string operation, object arguments);
}

internal interface IFeatureHandler<TResponse>
{
    Task<TResponse> HandleAsync(JsonElement arguments, CancellationToken token);
}

internal static class FeatureRequest
{
    public static Task<TResponse> InvokeAsync<TResponse>(
        object service,
        string operation,
        object arguments) =>
        ((IFeatureRequestInvoker)service).InvokeFeatureAsync<TResponse>(operation, arguments);
}

/// <summary>
/// UI 与业务之间的聚合入口，保留给现有页面与组合根使用。
/// 新代码应依赖 <see cref="ILauncherCoreService"/>、<see cref="IAccountService"/> 等领域接口，
/// 不要继续向本接口添加成员。
/// </summary>
public interface ILauncherService :
    IDisposable,
    ILauncherCoreService,
    IAccountService,
    IInstanceService,
    IInstanceMaintenanceService,
    IDownloadService,
    ITaskService,
    IGameService,
    ISettingsService,
    ICommunityService,
    IDiagnosticService,
    IWindowService
{
}
