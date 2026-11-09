using BlockLaunch;
using MCUGLauncher.Contracts;
using MCUGLauncher.Services;

namespace MCUGLauncher.UseCases;

/// <summary>生成冻结目录的卸载预览并在确认后排队，不绕过原生确认。</summary>
/// <remarks>B 类。依赖 LauncherContext.Config/Games.UsesDirectory(string)/Token/Tasks.Enqueue(string,string,string,DownloadRequest)，
/// InstanceSnapshot.Capture(AppConfig,string,string)/Request()、PreviewCache.For/Add/Get；Core.InstanceUninstaller.Plan 完成预览。
/// 详细依赖见 docs/components/GameDirectories.md，不依赖 UI 或当前页面。</remarks>
internal sealed class UninstallCommands(LauncherContext context)
{
    sealed record Confirmation(InstanceSnapshot Instance, UninstallPlan Plan);
    readonly PreviewCache<Confirmation> previews = PreviewCache<Confirmation>.For(context);

    public async Task<UninstallPreviewInfo> PreviewAsync(string version, string? gameRoot = null)
    {
        var instance = InstanceSnapshot.Capture(context.Config, version, gameRoot);
        if (context.Games.UsesDirectory(instance.Directory))
            throw new IOException("请先关闭此实例的游戏。");
        var plan = await Task.Run(() => InstanceUninstaller.Plan(instance.Root, instance.Key, instance.Settings), context.Token);
        var token = previews.Add(new(instance, plan));
        return new(
            token,
            version,
            plan.Name,
            "只删除列出的路径；默认保留共享游戏根目录数据。",
            plan.Paths,
            plan.SharedData);
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
