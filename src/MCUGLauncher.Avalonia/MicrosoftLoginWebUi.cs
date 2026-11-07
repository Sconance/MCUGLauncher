using Avalonia.Threading;
using BlockLaunch;
using XboxAuthNet.OAuth.CodeFlow;

namespace MCUGLauncher.Avalonia;

/// <summary>Avalonia 版 IWebUI 适配器。</summary>
internal sealed class MicrosoftLoginWebUi : IWebUI
{
    public Task<CodeFlowAuthorizationResult> DisplayDialogAndInterceptUri(Uri uri, ICodeFlowUrlChecker checker, CancellationToken token) =>
        DisplayAsync(uri, checker, token);

    public async Task DisplayDialogAndNavigateUri(Uri uri, CancellationToken token) =>
        await DisplayAsync(uri, null, token);

    Task<CodeFlowAuthorizationResult> DisplayAsync(Uri uri, ICodeFlowUrlChecker? checker, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (uri.Scheme != Uri.UriSchemeHttps)
            throw new AccountLoginException("登录地址无效，已停止授权。");
        Task<CodeFlowAuthorizationResult> Show() => new MicrosoftLoginWindow(uri, checker).RunAsync(token);
        return Dispatcher.UIThread.CheckAccess()
            ? Show()
            : Dispatcher.UIThread.InvokeAsync(Show);
    }
}
