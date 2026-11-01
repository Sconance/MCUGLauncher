using BlockLaunch;

namespace MCUGLauncher.Services;

internal sealed class InstanceOperationExecutor(Func<string, bool>? inUse)
{
    public Task ExecuteAsync(OperationRecord record, OperationContext task) =>
        Task.Run(() => ExecuteCoreAsync(record, task), task.Token);

    async Task ExecuteCoreAsync(OperationRecord record, OperationContext task)
    {
        var request = record.Download ?? throw new IOException("缺少维护请求。");
        var instance = request.Instance ?? throw new IOException("缺少实例快照。");
        if (inUse?.Invoke(instance.Directory) == true)
            throw new IOException("该实例正在运行，请关闭游戏后重试。");
        switch (request.Kind)
        {
            case "clone":
                await InstanceCloner.Copy(record.Root, instance.Key, instance.Directory,
                    instance.Requirements.Minecraft, instance.Name, instance.Settings, instance.Clone,
                    message => task.Report(message), task.Token);
                break;
            case "export":
                await InstanceManagement.Export(instance.Directory, request.Destination, instance.Name,
                    instance.Requirements, instance.Export, task.Token, overwrite: false);
                break;
            case "uninstall":
                task.Token.ThrowIfCancellationRequested();
                InstanceUninstaller.Delete(instance.Uninstall ?? throw new IOException("缺少已确认的卸载计划。"), instance.Settings);
                break;
            default:
                throw new IOException("未知实例维护操作。");
        }
    }
}
