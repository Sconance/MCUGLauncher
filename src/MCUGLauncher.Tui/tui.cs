using System.Text;
using MCUGLauncher.Contracts;
using MCUGLauncher.Services;
using MCUGLauncher.Tui.Hosting;

namespace MCUGLauncher.Tui;

/// <summary>终端组合入口：解析参数、装配已有应用服务并管理其生命周期。</summary>
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        var terminal = new Terminal(Console.In, Console.Out);
        try
        {
            if (args is ["--help"] or ["-h"])
            {
                terminal.Write("用法：MCUGLauncher.Tui [--data-directory <配置目录>]");
                terminal.Write("默认使用独立的 MCUGLauncher.Tui 配置目录；游戏目录仍由后端配置决定。");
                terminal.Write("输入编号选择菜单，0 返回，q 或输入结束退出。微软交互登录暂不支持。");
                return 0;
            }
            if (args.Length != 0 && (args.Length != 2 || args[0] != "--data-directory" || string.IsNullOrWhiteSpace(args[1])))
            {
                terminal.Write("参数错误。使用 --help 查看用法。");
                return 2;
            }

            var directory = args.Length == 2 ? Path.GetFullPath(args[1]) : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MCUGLauncher.Tui");
            var store = new ConfigStore(directory);
            var dispatchGate = new object();
            using ILauncherService service = new MCUGLauncher.UseCases.LauncherService(
                new TerminalDesktopHost(terminal),
                action => { lock (dispatchGate) { action(); } },
                _ => new TerminalBrowser(),
                _ => "",
                store);
            using var ui = new TerminalUi(service, terminal);
            terminal.Write($"配置目录：{directory}");
            terminal.Write("正在初始化现有应用服务…");
            await service.StartAsync();
            await ui.RunAsync();
            return 0;
        }
        catch (EndOfStreamException)
        {
            terminal.Write("终端已退出。");
            return 0;
        }
        catch (Exception error)
        {
            terminal.Write($"无法启动 TUI：{error.Message}");
            return 1;
        }
    }
}
