using XboxAuthNet.OAuth.CodeFlow;

namespace MCUGLauncher.Tui.Hosting;

/// <summary>明确拒绝终端尚未实现的交互授权，避免意外启用另一套微软登录流程。</summary>
internal sealed class TerminalBrowser : IWebUI
{
    const string Message = "TUI 暂不支持微软交互授权，请使用 Avalonia 登录窗口。";

    public Task<CodeFlowAuthorizationResult> DisplayDialogAndInterceptUri(
        Uri uri, ICodeFlowUrlChecker checker, CancellationToken token) =>
        Task.FromException<CodeFlowAuthorizationResult>(new NotSupportedException(Message));

    public Task DisplayDialogAndNavigateUri(Uri uri, CancellationToken token) =>
        Task.FromException(new NotSupportedException(Message));
}
