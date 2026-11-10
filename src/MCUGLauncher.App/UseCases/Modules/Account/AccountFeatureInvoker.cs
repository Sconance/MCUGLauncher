namespace MCUGLauncher.UseCases;

/// <summary>账号模块的 feature 调用入口；由组合根提供，不依赖 LauncherService。</summary>
internal interface IAccountFeatureInvoker
{
    Task<TResponse> InvokeAsync<TResponse>(string operation, object arguments);
}

internal sealed class AccountFeatureInvoker(LauncherApplication application) : IAccountFeatureInvoker
{
    public Task<TResponse> InvokeAsync<TResponse>(string operation, object arguments) =>
        application.ExecuteFeatureAsync<TResponse>(operation, arguments);
}
