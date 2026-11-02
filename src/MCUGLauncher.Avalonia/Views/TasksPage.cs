using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;

namespace MCUGLauncher.Avalonia.Views;
internal sealed class TasksPage : NativePage
{
    readonly StackPanel list = Ui.Stack();
    public TasksPage(LauncherFacade session) : base(session)
    {
        Title("下载任务", "任务独立于页面运行；取消保留已校验缓存，删除只清理历史记录。");
        Body.Children.Add(list);
        Draw();
        _ = Send(new
        {
            type = "requestTasks"
        });
    }

    void Draw()
    {
        list.Children.Clear();
        foreach (var task in Session.Tasks)
        {
            var actions = Ui.Row();
            var allowed = task.Committing ? Array.Empty<(string, string)>() : task.State switch
            {
                "Running" => new[]
                {
                    ("暂停", "pause"),
                    ("取消", "cancel")
                },
                "Queued" => new[]
                {
                    ("暂停", "pause"),
                    ("取消", "cancel")
                },
                "Paused" or "Failed" or "Interrupted" => new[]
                {
                    ("继续 / 重试", "resume"),
                    ("取消", "cancel")
                },
                "Pausing" or "Cancelling" => Array.Empty<(string, string)>(),
                _ => new[]
                {
                    ("删除记录", "delete")
                }
            };
            foreach (var (label, action) in allowed)
                actions.Children.Add(Ui.Button(label, () => Send(new { type = "taskAction", id = task.Id, action })));
            list.Children.Add(Ui.Card(Ui.Stack(
                        Ui.Text(task.Name + " · " + StateLabel(task.State), 16),
                        Ui.Text(task.Message),
                        new ProgressBar { Minimum = 0, Maximum = 100, Value = task.Percent ?? 0, IsIndeterminate = task.Percent == null, Height = 7 },
                        Ui.Text(task.Percent is { } percent ? $"{percent:0.0}%" : "正在准备…"),
                        actions
            )));
        }

        if (list.Children.Count == 0)
            list.Children.Add(Ui.Text("当前没有下载任务。"));
    }

    static string StateLabel(string state) => state switch
    {
        "Running" => "进行中",
        "Queued" => "排队中",
        "Paused" => "已暂停",
        "Pausing" => "正在暂停",
        "Cancelling" => "正在取消",
        "Cancelled" => "已取消",
        "Completed" => "已完成",
        "Failed" => "失败",
        "Interrupted" => "已中断",
        _ => state
    };

    protected override void Receive(NativeMessage message)
    {
        if (message.Type == "tasks")
            Draw();
    }
}

internal sealed class GamesPage : NativePage
{
    readonly StackPanel list = Ui.Stack();
    public GamesPage(LauncherFacade session) : base(session)
    {
        Title("运行中的游戏", "每个游戏独立管理。关闭前会确认，关闭启动器不关闭游戏。");
        Body.Children.Add(list);
        Draw();
        _ = Send(new
        {
            type = "requestGames"
        });
    }

    void Draw()
    {
        list.Children.Clear();
        foreach (var game in Session.Games)
            list.Children.Add(Ui.Card(Ui.Row(
                        Ui.Text(game.Version + " · " + game.Player),
                        Ui.Button("关闭这个游戏", () => Send(new { type = "stopGame", id = game.Id }))
            )));
        if (list.Children.Count == 0)
            list.Children.Add(Ui.Text("没有正在运行的游戏。"));
    }

    protected override void Receive(NativeMessage message)
    {
        if (message.Type == "games")
            Draw();
    }
}
