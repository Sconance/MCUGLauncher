using BlockLaunch;
using MCUGLauncher.Services;

namespace MCUGLauncher.UseCases;

internal sealed class UninstallCommands(LauncherContext context)
{
    sealed record Confirmation(InstanceSnapshot Instance, UninstallPlan Plan);
    readonly PreviewCache<Confirmation> previews = new();

    public async Task PreviewAsync(string version)
    {
        var instance = InstanceSnapshot.Capture(context.Config, version);
        if (context.Games.UsesDirectory(instance.Directory))
            throw new IOException("请先关闭此实例的游戏。");
        var plan = await Task.Run(() => InstanceUninstaller.Plan(instance.Root, instance.Key, instance.Settings), context.Token);
        var token = previews.Add(new(instance, plan));
        context.Send(new
        {
            type = "uninstallPreview",
            token,
            version,
            name = plan.Name,
            paths = plan.Paths,
            shared = plan.SharedData,
            notice = "只删除列出的路径；默认保留共享游戏根目录数据。"
        });
    }

    public string Name(string token) => previews.Get(token).Instance.Name;

    public void Apply(string token)
    {
        var preview = previews.Get(token, consume: true);
        var request = preview.Instance.Request();
        request.Uninstall = preview.Plan;
        context.Tasks.Enqueue("卸载 " + preview.Instance.Name, preview.Instance.Root, preview.Instance.Directory,
            new DownloadRequest { Kind = "uninstall", Instance = request });
    }
}
