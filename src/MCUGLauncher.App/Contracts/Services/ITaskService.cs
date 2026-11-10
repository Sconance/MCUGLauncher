namespace MCUGLauncher.Contracts;

/// <summary>后台任务队列查询与操作。</summary>
public interface ITaskService
{
    event Action<IReadOnlyList<TaskInfo>>? TasksChanged;

    IReadOnlyList<TaskInfo> Tasks
    {
        get;
    }

    Task RequestTasksAsync();
    Task TaskActionAsync(string id, string action);
}
