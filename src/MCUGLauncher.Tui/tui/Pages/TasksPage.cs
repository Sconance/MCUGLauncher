using MCUGLauncher.Contracts;

namespace MCUGLauncher.Tui.Pages;

/// <summary>按需刷新任务和游戏会话，转发任务操作；退出页面不取消任务。</summary>
internal sealed class TasksPage(ILauncherService service, Terminal terminal) : TerminalPage(service, terminal)
{
    public override string Title => "任务与运行中的游戏";

    public override async Task ShowAsync()
    {
        var action = IO.Select(Title, ["查看 / 操作下载任务", "查看 / 关闭游戏"]);
        if (action == 0)
        {
            await Service.RequestTasksAsync();
            var tasks = Service.Tasks.ToArray();
            var selected = IO.Select("任务（重新进入本页刷新）", tasks.Select(item =>
                $"{item.Name} | {item.State} | {item.Percent:F1}% | {item.Message}").ToArray());
            if (selected < 0)
                return;
            var task = tasks[selected];
            if (task.Committing)
            {
                IO.Write("任务正在提交文件，请等待完成后再操作。");
                IO.Pause();
                return;
            }
            var operation = IO.Select(task.Name, ["暂停", "继续", "取消", "删除历史记录"]);
            var actions = new[] { "pause", "resume", "cancel", "delete" };
            if (operation >= 0 && (operation < 2 || IO.Confirm("确认" + (operation == 2 ? "取消任务" : "删除历史记录") + "？")))
            {
                await Service.TaskActionAsync(task.Id, actions[operation]);
                IO.Write("操作请求已提交；应用服务会校验任务状态，重新进入列表查看结果。");
            }
        }
        else if (action == 1)
        {
            await Service.RequestGamesAsync();
            var games = Service.Games.ToArray();
            var selected = IO.Select("游戏会话（选择后确认关闭）", games.Select(item =>
                $"{item.Version} | {item.Player} | PID {item.Pid}").ToArray());
            if (selected >= 0)
            {
                await Service.StopGameAsync(games[selected].Id);
                IO.Write("关闭请求已处理；重新进入列表查看会话状态。");
            }
        }
        else
            return;
        IO.Pause();
    }
}
