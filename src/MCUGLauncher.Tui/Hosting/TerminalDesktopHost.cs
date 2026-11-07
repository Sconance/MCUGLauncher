using MCUGLauncher.UseCases;

namespace MCUGLauncher.Tui.Hosting;

/// <summary>C 类桌面端口适配：使用终端确认及手动路径输入，不实现业务操作。</summary>
internal sealed class TerminalDesktopHost(Terminal terminal) : IDesktopHost
{
    public bool ConfirmStopGame() => terminal.Confirm("确定关闭所选游戏？");
    public bool ConfirmUninstall(string name) => terminal.Confirm($"确定卸载 {name}？");
    public string? PickFolder(string target) => Pick("目录", target);
    public string? PickFile(string target) => Pick("文件", target);
    public string? PickSaveFile(string target) => Pick("保存文件", target);

    string? Pick(string kind, string target)
    {
        var path = terminal.Read($"{target}：输入{kind}完整路径（留空取消）：");
        return path.Length == 0 ? null : path;
    }

    public void Window(string action) => terminal.Write("TUI 无图形窗口操作；退出请在菜单选择 0。");
    public void Drag()
    {
    }
    public void ResetWindow() => terminal.Write("TUI 无窗口尺寸设置。");
}
