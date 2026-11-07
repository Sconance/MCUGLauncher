using MCUGLauncher.Contracts;

namespace MCUGLauncher.Tui.Pages;

/// <summary>页面共有的终端与应用接口引用；业务处理仍由应用服务完成。</summary>
internal abstract class TerminalPage(ILauncherService service, Terminal terminal)
{
    protected ILauncherService Service { get; } = service;
    protected Terminal IO { get; } = terminal;
    protected LauncherState State => Service.State ?? throw new InvalidOperationException("应用状态尚未就绪。");
    public abstract string Title
    {
        get;
    }
    public abstract Task ShowAsync();
}
