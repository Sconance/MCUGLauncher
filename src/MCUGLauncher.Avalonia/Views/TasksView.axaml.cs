using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MCUGLauncher.Avalonia.Views;

internal partial class TasksView : UserControl
{
    LauncherFacade? facade;

    public TasksView()
    {
        InitializeComponent();
        RefreshButton.Click += async (_, _) => await facade!.RequestTasksAsync();
    }

    public TasksView(LauncherFacade facade) : this()
    {
        Attach(facade);
    }

    public void Attach(LauncherFacade value)
    {
        facade = value;
        facade.TasksChanged += Render;
        facade.ToastReceived += info => StatusText.Text = info.Message;
        facade.ErrorReceived += message => StatusText.Text = "操作失败：" + message;
        Render(facade.Tasks);
    }

    public void Activate() => _ = facade?.RequestTasksAsync();

    void Render(IReadOnlyList<TaskInfo> tasks)
    {
        TasksPanel.Children.Clear();
        if (tasks.Count == 0)
        {
            TasksPanel.Children.Add(new Border
            {
                Classes = { "card" },
                Child = new TextBlock
                {
                    Text = "当前没有排队或运行中的任务。",
                    Classes = { "muted" }
                }
            });
            return;
        }
        foreach (var task in tasks)
        {
            var title = new TextBlock
            {
                Text = task.Name,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };
            var state = new TextBlock
            {
                Text = $"{task.State} · {task.Percent:0}%" + (task.Message.Length > 0 ? " · " + task.Message : ""),
                Classes = { "muted" },
                TextWrapping = TextWrapping.Wrap
            };
            var progress = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = Math.Clamp(task.Percent, 0, 100),
                Height = 6,
                Margin = new Thickness(0, 4, 0, 0)
            };
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            foreach (var (label, action) in new[]
                     {
                         ("暂停", "pause"), ("继续", "resume"), ("取消", "cancel"), ("删除", "delete")
                     })
            {
                var button = new Button { Content = label, Classes = { "ghost" } };
                var taskId = task.Id;
                var taskAction = action;
                button.Click += async (_, _) =>
                {
                    await facade!.TaskActionAsync(taskId, taskAction);
                    await facade.RequestTasksAsync();
                };
                actions.Children.Add(button);
            }
            var copy = new StackPanel { Spacing = 3 };
            copy.Children.Add(title);
            copy.Children.Add(state);
            copy.Children.Add(progress);
            copy.Children.Add(actions);
            TasksPanel.Children.Add(new Border
            {
                Classes = { "card" },
                Padding = new Thickness(14),
                Child = copy
            });
        }
    }

}
