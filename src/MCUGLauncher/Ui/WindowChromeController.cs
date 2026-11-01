using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MCUGLauncher.Ui;

/// <summary>无边框窗口行为：圆角、移动、窗口命令与默认尺寸。</summary>
/// <remarks>由窗口宿主在 UI 线程调用。沿用 codex 的 Windows 原生移动循环，不恢复 fix 的鼠标轮询。</remarks>
internal sealed class WindowChromeController(Window window)
{
    [DllImport("user32.dll")]
    static extern bool ReleaseCapture();
    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>窗口句柄创建后请求 Windows 11 圆角；旧系统忽略失败。</summary>
    public void Initialize()
    {
        var preference = 2;
        DwmSetWindowAttribute(new WindowInteropHelper(window).Handle, 33, ref preference, sizeof(int));
    }

    /// <summary>交由 Windows 处理移动、贴边与跨显示器边界。</summary>
    public void BeginDrag()
    {
        ReleaseCapture();
        SendMessage(new WindowInteropHelper(window).Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
    }

    /// <summary>处理标题栏命令；不识别的动作忽略。</summary>
    public void Handle(string action)
    {
        switch (action)
        {
            case "close":
                window.Close();
                break;
            case "minimize":
                window.WindowState = WindowState.Minimized;
                break;
            case "maximize":
                window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                break;
        }
    }

    /// <summary>恢复默认尺寸；配置持久化仍由窗口生命周期负责。</summary>
    public void Reset()
    {
        window.Width = 1200;
        window.Height = 780;
        window.WindowState = WindowState.Normal;
    }
}
