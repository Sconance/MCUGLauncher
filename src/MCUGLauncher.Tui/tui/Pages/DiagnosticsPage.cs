using MCUGLauncher.Contracts;

namespace MCUGLauncher.Tui.Pages;

/// <summary>通过应用接口查看日志、系统信息并测试数据源。</summary>
internal sealed class DiagnosticsPage(ILauncherService service, Terminal terminal) : TerminalPage(service, terminal)
{
    public override string Title => "诊断与数据源";

    public override async Task ShowAsync()
    {
        var action = IO.Select(Title, ["查看启动器日志末尾", "系统信息", "测试当前数据源", "刷新状态"]);
        switch (action)
        {
            case 0:
                var log = await Service.ReadLogAsync();
                foreach (var line in log.Split('\n').TakeLast(60))
                    IO.Write(line);
                break;
            case 1:
                var info = await Service.SystemInfoAsync();
                IO.Write($"应用：{info.App} | .NET：{info.Dotnet} | 系统：{info.Os}");
                IO.Write($"游戏目录：{info.Root}\n配置目录：{info.Data}");
                break;
            case 2:
                var sources = State.DataSources;
                var urls = new[] { sources.Announcements, sources.Servers, sources.Recommendations, sources.Quotes };
                var names = new[] { "announcements", "servers", "recommendations", "quotes" };
                var selected = IO.Select("数据源", ["公告", "服务器", "推荐", "语录"]);
                if (selected >= 0)
                {
                    IO.Write(urls[selected]);
                    var result = await Service.TestSourceAsync(names[selected], urls[selected]);
                    IO.Write($"{(result.Ok ? "成功" : "失败")}：{result.Message} ({result.Ms} ms)");
                }
                break;
            case 3:
                await Service.RequestStateAsync();
                IO.Write("状态刷新请求已处理。");
                break;
            default:
                return;
        }
        IO.Pause();
    }
}
