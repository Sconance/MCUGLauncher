using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using BlockLaunch;
using Microsoft.Web.WebView2.Core;
using XboxAuthNet.OAuth.CodeFlow;

namespace MCUGLauncher.Avalonia;

/// <summary>Avalonia 微软登录窗口，拦截授权回调 URL。</summary>
internal sealed class MicrosoftLoginWindow : Window
{
    readonly Uri loginUri;
    readonly ICodeFlowUrlChecker? checker;
    readonly TextBlock status = new() { Text = "正在加载 Microsoft 官方登录页…", TextWrapping = TextWrapping.Wrap };
    readonly Button retry = new() { Content = "重新加载", IsVisible = false };
    readonly TaskCompletionSource<CodeFlowAuthorizationResult> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly CancellationTokenSource lifetime = new();
    readonly WebView2 browser = new();
    bool closed;

    public MicrosoftLoginWindow(Uri loginUri, ICodeFlowUrlChecker? checker)
    {
        this.loginUri = loginUri;
        this.checker = checker;
        Title = "Microsoft 正版账号登录 · MCUG Launcher";
        Width = 580;
        Height = 760;
        MinWidth = 420;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brushes.White;
        Content = Layout();
        Closed += OnClosed;
        retry.Click += (_, _) => Reload();
    }

    Control Layout()
    {
        var panel = new DockPanel();
        DockPanel.SetDock(status, Dock.Top);
        DockPanel.SetDock(retry, Dock.Top);
        status.Margin = new Thickness(12);
        retry.Margin = new Thickness(12, 4, 12, 10);
        retry.HorizontalAlignment = HorizontalAlignment.Left;
        panel.Children.Add(status);
        panel.Children.Add(retry);
        panel.Children.Add(browser);
        return panel;
    }

    public async Task<CodeFlowAuthorizationResult> RunAsync(CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        using var registration = token.Register(() => Dispatcher.UIThread.Post(() => Cancel(token)));
        try
        {
            Show();
            browser.Source = loginUri;
            browser.DOMContentLoaded += OnDomContentLoaded;
            return await result.Task;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error)
        {
            throw DescribeFailure(error);
        }
        finally
        {
            CloseIfOpen();
            lifetime.Dispose();
        }
    }

    void OnDomContentLoaded(object? sender, CoreWebView2DOMContentLoadedEventArgs e)
    {
        var core = browser.CoreWebView2;
        if (core is null)
            return;
        core.Settings.AreDevToolsEnabled = false;
        core.NavigationStarting += OnNavigationStarting;
        core.NavigationCompleted += OnNavigationCompleted;
        core.NewWindowRequested += OnNewWindowRequested;
        // A cached session can redirect before the first navigation event is observed.
        if (Uri.TryCreate(core.Source, UriKind.Absolute, out var current))
            CompleteAuthorization(current);
    }

    void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var next))
        {
            e.Cancel = true;
            return;
        }
        if (CompleteAuthorization(next))
        {
            e.Cancel = true;
            return;
        }
        if (next.Scheme != Uri.UriSchemeHttps)
        {
            e.Cancel = true;
            return;
        }
        status.Text = "正在连接 " + next.Host + "…";
    }

    bool CompleteAuthorization(Uri uri)
    {
        var authorization = checker?.GetAuthCodeResult(uri);
        if (!authorization.HasValue || authorization.Value.IsEmpty)
            return false;
        result.TrySetResult(authorization.Value);
        CloseIfOpen();
        return true;
    }

    void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        status.Text = e.IsSuccess ? "请在下方 Microsoft 页面完成登录。" : "登录页加载失败。请检查网络或代理后重试。";
        retry.IsVisible = true;
    }

    void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var next) && next.Scheme == Uri.UriSchemeHttps)
            browser.CoreWebView2?.Navigate(next.AbsoluteUri);
    }

    void Reload()
    {
        status.Text = "正在重新加载 Microsoft 登录页…";
        retry.IsVisible = false;
        browser.CoreWebView2?.Navigate(loginUri.AbsoluteUri);
    }

    void OnClosed(object? sender, EventArgs e)
    {
        closed = true;
        lifetime.Cancel();
        result.TrySetCanceled();
    }

    void Cancel(CancellationToken token)
    {
        result.TrySetCanceled(token);
        CloseIfOpen();
    }

    void CloseIfOpen()
    {
        if (!closed)
            Close();
    }

    internal static AccountLoginException DescribeFailure(Exception error) => error switch
    {
        AccountLoginException known => known,
        TimeoutException => new("登录窗口初始化超时，请检查 WebView2 运行库后重试。"),
        UnauthorizedAccessException => new("无法访问登录缓存目录，请检查本地目录权限。"),
        IOException => new("无法读写登录缓存，请检查磁盘和缓存目录。"),
        _ => new("登录窗口发生异常，请重新打开启动器后重试。")
    };
}
