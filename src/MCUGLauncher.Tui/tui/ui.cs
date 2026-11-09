using System.Collections.Concurrent;
using MCUGLauncher.Contracts;
using MCUGLauncher.Tui.Pages;

namespace MCUGLauncher.Tui;

/// <summary>终端导航及通知展示；页面生命周期不控制后台任务生命周期。</summary>
internal sealed class TerminalUi : IDisposable
{
    readonly ILauncherService service;
    readonly Terminal terminal;
    readonly TerminalPage[] pages;
    readonly ConcurrentQueue<string> notices = new();

    public TerminalUi(ILauncherService service, Terminal terminal)
    {
        this.service = service;
        this.terminal = terminal;
        pages = [new InstancesPage(service, terminal), new AccountsPage(service, terminal),
            new ServersPage(service, terminal), new DownloadsPage(service, terminal),
            new TasksPage(service, terminal), new SettingsPage(service, terminal),
            new DiagnosticsPage(service, terminal), new GameDirectoriesPage(service, terminal)];
        service.ErrorReceived += OnError;
        service.ToastReceived += OnToast;
    }

    void OnError(string message) => notices.Enqueue("操作失败：" + message);
    void OnToast(ToastInfo toast) => notices.Enqueue(toast.Message);

    public async Task RunAsync()
    {
        while (true)
        {
            while (notices.TryDequeue(out var notice))
                terminal.Write(notice);
            if (service.State is { } state)
                terminal.Write($"MCUG Launcher {state.AppVersion} | {state.Player} ({state.AccountType}) | 实例：{state.SelectedInstance}\n目录：{state.Root}");
            var selected = terminal.Select("主菜单", pages.Select(page => page.Title).ToArray());
            if (selected < 0)
                return;
            try
            {
                await pages[selected].ShowAsync();
            }
            catch (EndOfStreamException) { return; }
            catch (Exception error)
            {
                terminal.Write("操作失败：" + error.Message);
                terminal.Pause();
            }
        }
    }

    public void Dispose()
    {
        service.ErrorReceived -= OnError;
        service.ToastReceived -= OnToast;
    }
}
