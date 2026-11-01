using System.Text.Json;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Wpf;

namespace MCUGLauncher.Ui;

/// <summary>C# 到 WebView2 的消息出口，统一 camelCase 序列化与线程调度。</summary>
/// <remarks>可以从后台调用。窗口关闭后不再发送，跨线程使用异步调度避免阻塞任务线程。</remarks>
internal sealed class UiBridge(WebView2 browser, Dispatcher dispatcher, Func<bool> closed)
{
    static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>发送状态快照；浏览器未初始化或窗口已关闭时忽略。</summary>
    public void Send(object payload)
    {
        if (closed())
            return;
        if (!dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => Send(payload));
            return;
        }
        if (!closed())
            browser.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(payload, Options));
    }
}
