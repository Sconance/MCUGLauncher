using MCUGLauncher.Contracts;

namespace MCUGLauncher.Tui.Pages;

/// <summary>目录列表、添加、刷新和注销入口；路径与持久化规则交给应用接口。</summary>
internal sealed class GameDirectoriesPage(ILauncherService service, Terminal terminal) : TerminalPage(service, terminal)
{
    public override string Title => "游戏目录管理";

    public override async Task ShowAsync()
    {
        await Service.RequestStateAsync();
        while (true)
        {
            var directories = await Service.ScanGameDirectoriesAsync();
            IO.Write("当前游戏目录：" + State.Root);
            var choices = new[] { "添加游戏目录", "重新扫描所有目录" }
                .Concat(directories.Select(item => Label(item, State.Root))).ToArray();
            var selected = IO.Select(Title, choices);
            if (selected < 0)
                return;
            if (selected == 0)
            {
                var path = IO.Read("游戏根目录完整路径（包含 versions 的目录；留空取消）：");
                if (path.Length == 0)
                    continue;
                var select = IO.Confirm("同时设为当前游戏目录？");
                IO.Write((await Service.AddGameDirectoryAsync(path, select)).Message);
            }
            else if (selected == 1)
            {
                await Service.RequestStateAsync();
                IO.Write("已请求重新扫描所有目录。");
            }
            else
            {
                var directory = directories[selected - 2];
                IO.Write("游戏目录：" + directory.Root);
                if (!directory.Available)
                    IO.Write("目录不可用：" + directory.Error);
                switch (IO.Select("目录操作", ["查看此目录的实例", "设为当前游戏目录", "移除目录记录（不删除游戏文件）"]))
                {
                    case 0:
                        await new InstancesPage(Service, IO).ShowDirectoryAsync(directory);
                        break;
                    case 1:
                        IO.Write((await Service.AddGameDirectoryAsync(directory.Root, select: true)).Message);
                        break;
                    case 2:
                        IO.Write("只取消管理此目录，不删除游戏文件；已排队任务继续使用原目标。");
                        if (IO.Confirm("移除目录记录 " + directory.Root + "？"))
                            IO.Write((await Service.RemoveGameDirectoryAsync(directory.Root)).Message);
                        break;
                }
            }
        }
    }

    public static string Label(GameDirectoryInfo directory, string current) =>
        (string.Equals(directory.Root, current, StringComparison.OrdinalIgnoreCase) ? "[当前] " : "") +
        directory.Root + (directory.Available ? $" | {directory.Instances.Count} 个实例" : " | 不可用：" + directory.Error);
}
