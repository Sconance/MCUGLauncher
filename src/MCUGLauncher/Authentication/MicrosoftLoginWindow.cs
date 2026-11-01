using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using XboxAuthNet.OAuth.CodeFlow;

namespace BlockLaunch;

// Owns only the native login view and browser lifetime. OAuth validation remains in the library.
internal sealed class MicrosoftLoginWindow : Window
{
    static readonly TimeSpan InitializationTimeout = TimeSpan.FromSeconds(45);
    readonly string profileDirectory;
    readonly Uri loginUri;
    readonly ICodeFlowUrlChecker? checker;
    readonly WebView2 browser = new();
    readonly TextBlock status = new() { Text = "正在加载 Microsoft 官方登录页…", Margin = new Thickness(12), TextWrapping = TextWrapping.Wrap };
    readonly Button retry = new() { Content = "重新加载", Margin = new Thickness(12, 4, 12, 10), Visibility = Visibility.Collapsed };
    readonly TaskCompletionSource<CodeFlowAuthorizationResult> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly CancellationTokenSource lifetime = new();
    bool closed;

    public MicrosoftLoginWindow(string directory, Uri loginUri, ICodeFlowUrlChecker? checker)
    {
        profileDirectory = Path.Combine(directory, "login-webview");
        this.loginUri = loginUri;
        this.checker = checker;
        Title = "Microsoft 正版账号登录 · MCUG Launcher";
        Width = 580;
        Height = 760;
        MinWidth = 420;
        MinHeight = 500;
        Owner = Application.Current.MainWindow;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brushes.White;
        Content = CreateLayout();
        Closed += OnClosed;
    }

    DockPanel CreateLayout()
    {
        var layout = new DockPanel();
        DockPanel.SetDock(status, Dock.Top);
        DockPanel.SetDock(retry, Dock.Top);
        layout.Children.Add(status);
        layout.Children.Add(retry);
        layout.Children.Add(browser);
        return layout;
    }

    public async Task<CodeFlowAuthorizationResult> RunAsync(CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        using var cancelled = token.Register(() => Dispatcher.BeginInvoke(new Action(() => Cancel(token))));
        try
        {
            Show();
            await InitializeBrowserAsync(linked.Token);
            if (!closed)
                browser.CoreWebView2.Navigate(loginUri.AbsoluteUri);
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
            browser.Dispose();
            lifetime.Dispose();
        }
    }

    async Task InitializeBrowserAsync(CancellationToken token)
    {
        // This cache is isolated from both launcher pages and the library's WinForms profile.
        var environment = await CoreWebView2Environment.CreateAsync(null, profileDirectory).WaitAsync(InitializationTimeout, token);
        if (closed)
            return;
        await browser.EnsureCoreWebView2Async(environment).WaitAsync(InitializationTimeout, token);
        if (closed)
            return;
        var core = browser.CoreWebView2;
        core.Settings.IsWebMessageEnabled = false;
        core.Settings.AreHostObjectsAllowed = false;
        core.Settings.AreDevToolsEnabled = false;
        core.NavigationStarting += OnNavigationStarting;
        core.NavigationCompleted += OnNavigationCompleted;
        core.NewWindowRequested += OnNewWindowRequested;
        core.DownloadStarting += (_, e) => e.Cancel = true;
        retry.Click += (_, _) => Reload();
    }

    void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var next))
        {
            e.Cancel = true;
            return;
        }
        var authorization = checker?.GetAuthCodeResult(next);
        if (authorization.HasValue && !authorization.Value.IsEmpty)
        {
            e.Cancel = true;
            result.TrySetResult(authorization.Value);
            CloseIfOpen();
            return;
        }
        if (next.Scheme != Uri.UriSchemeHttps)
        {
            e.Cancel = true;
            return;
        }
        status.Text = "正在连接 " + next.Host + "…";
    }

    void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        status.Text = e.IsSuccess ? "请在下方 Microsoft 页面完成登录。" : "登录页加载失败（" + e.WebErrorStatus + "）。请检查网络或代理后重试。";
        retry.Visibility = Visibility.Visible;
    }

    void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var next) && next.Scheme == Uri.UriSchemeHttps)
            browser.CoreWebView2.Navigate(next.AbsoluteUri);
    }

    void Reload()
    {
        status.Text = "正在重新加载 Microsoft 登录页…";
        retry.Visibility = Visibility.Collapsed;
        browser.CoreWebView2.Navigate(loginUri.AbsoluteUri);
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

    // Never expose OAuth URLs, response bodies or raw exception messages in the launcher.
    internal static AccountLoginException DescribeFailure(Exception error) => error switch
    {
        AccountLoginException known => known,
        WebView2RuntimeNotFoundException => new("未检测到 WebView2 运行库，请安装后重新登录。"),
        TimeoutException => new("登录窗口初始化超时，请检查 WebView2 运行库后重试。"),
        UnauthorizedAccessException => new("无法访问登录缓存目录，请检查本地目录权限。"),
        IOException => new("无法读写登录缓存，请检查磁盘和缓存目录。"),
        COMException => new("登录浏览器组件初始化失败，请更新 WebView2 运行库后重试。"),
        _ => new("登录窗口发生异常，请重新打开启动器后重试。")
    };
}
