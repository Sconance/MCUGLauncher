using System.Windows;
using XboxAuthNet.OAuth.CodeFlow;

namespace BlockLaunch;

// UI adapter only; the composition root injects it into authentication.
internal sealed class MicrosoftLoginWebUi(string directory) : IWebUI
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
        var dispatcher = Application.Current?.Dispatcher
            ?? throw new AccountLoginException("登录窗口尚未就绪，请重新打开启动器。");
        Task<CodeFlowAuthorizationResult> Show() => new MicrosoftLoginWindow(directory, uri, checker).RunAsync(token);
        return dispatcher.CheckAccess() ? Show() : dispatcher.InvokeAsync(Show).Task.Unwrap();
    }
}
