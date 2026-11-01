using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using XboxAuthNet.OAuth.CodeFlow;

namespace BlockLaunch;

// Authentication-only browser adapter. No launcher bridge or game logic is exposed here.
internal sealed class MicrosoftLoginWebUi(string directory) : IWebUI
{
    public Task<CodeFlowAuthorizationResult> DisplayDialogAndInterceptUri(Uri uri, ICodeFlowUrlChecker checker, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return OnUiThread(() => ShowAsync(uri, checker, token));
    }

    public async Task DisplayDialogAndNavigateUri(Uri uri, CancellationToken token) =>
        await OnUiThread(() => ShowAsync(uri, null, token));

    static Task<T> OnUiThread<T>(Func<Task<T>> action)
    {
        var dispatcher = Application.Current?.Dispatcher
            ?? throw new AccountLoginException("登录窗口尚未就绪，请重新打开启动器。");
        return dispatcher.CheckAccess() ? action() : dispatcher.InvokeAsync(action).Task.Unwrap();
    }

    async Task<CodeFlowAuthorizationResult> ShowAsync(Uri uri, ICodeFlowUrlChecker? checker, CancellationToken token)
    {
        if (uri.Scheme != Uri.UriSchemeHttps)
            throw new AccountLoginException("登录地址无效，已停止授权。");
        token.ThrowIfCancellationRequested();
        var result = new TaskCompletionSource<CodeFlowAuthorizationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var status = new TextBlock { Text = "正在加载 Microsoft 官方登录页…", Margin = new Thickness(12), TextWrapping = TextWrapping.Wrap };
        var retry = new Button { Content = "重新加载", Margin = new Thickness(12, 4, 12, 10), Visibility = Visibility.Collapsed };
        var web = new WebView2();
        var layout = new DockPanel();
        DockPanel.SetDock(status, Dock.Top);
        DockPanel.SetDock(retry, Dock.Top);
        layout.Children.Add(status);
        layout.Children.Add(retry);
        layout.Children.Add(web);
        var window = new Window
        {
            Title = "Microsoft 正版账号登录 · MCUG Launcher",
            Width = 580,
            Height = 760,
            MinWidth = 420,
            MinHeight = 500,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current.MainWindow,
            Background = Brushes.White,
            Content = layout
        };
        var closed = false;
        window.Closed += (_, _) =>
        {
            closed = true;
            result.TrySetCanceled();
        };
        using var cancellation = token.Register(() => window.Dispatcher.BeginInvoke(new Action(() =>
        {
            result.TrySetCanceled(token);
            if (!closed)
                window.Close();
        })));
        window.Show();
        try
        {
            // Separate profile avoids the library's shared WinForms cache and UI-thread initialization.
            var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(directory, "login-webview")).WaitAsync(TimeSpan.FromSeconds(45), token);
            if (closed)
                return await result.Task;
            await web.EnsureCoreWebView2Async(environment).WaitAsync(TimeSpan.FromSeconds(45), token);
            if (closed)
                return await result.Task;
            var settings = web.CoreWebView2.Settings;
            settings.IsWebMessageEnabled = false;
            settings.AreHostObjectsAllowed = false;
            settings.AreDevToolsEnabled = false;
            web.CoreWebView2.NavigationStarting += (_, e) =>
            {
                if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var next))
                {
                    e.Cancel = true;
                    return;
                }
                if (checker != null)
                {
                    var authorization = checker.GetAuthCodeResult(next);
                    if (!authorization.IsEmpty)
                    {
                        e.Cancel = true;
                        result.TrySetResult(authorization);
                        window.Close();
                        return;
                    }
                }
                if (next.Scheme != Uri.UriSchemeHttps)
                {
                    e.Cancel = true;
                    return;
                }
                status.Text = "正在连接 " + next.Host + "…";
            };
            web.CoreWebView2.NavigationCompleted += (_, e) =>
            {
                status.Text = e.IsSuccess ? "请在下方 Microsoft 页面完成登录。" : "登录页加载失败（" + e.WebErrorStatus + "）。请检查网络或代理后重试。";
                retry.Visibility = Visibility.Visible;
            };
            web.CoreWebView2.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var next) && next.Scheme == Uri.UriSchemeHttps)
                    web.CoreWebView2.Navigate(next.AbsoluteUri);
            };
            web.CoreWebView2.DownloadStarting += (_, e) => e.Cancel = true;
            retry.Click += (_, _) =>
            {
                status.Text = "正在重新加载 Microsoft 登录页…";
                retry.Visibility = Visibility.Collapsed;
                web.CoreWebView2.Navigate(uri.AbsoluteUri);
            };
            web.CoreWebView2.Navigate(uri.AbsoluteUri);
            return await result.Task;
        }
        catch (OperationCanceledException) { throw; }
        catch (TimeoutException)
        {
            throw new AccountLoginException("登录窗口加载超时，请检查 WebView2 运行库、网络和代理后重试。");
        }
        catch (Exception)
        {
            throw new AccountLoginException("微软登录窗口初始化失败，请检查 WebView2 运行库和本地目录权限后重试。");
        }
        finally
        {
            if (!closed)
                window.Close();
            web.Dispose();
        }
    }
}
