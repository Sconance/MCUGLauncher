using System.Security.Cryptography;
using BlockLaunch;

namespace MCUGLauncher.Services;

// Uses the existing Core installers, dependency resolver and verified downloader.
internal sealed class DownloadExecutor(HttpClient http, Func<string, bool>? inUse = null, Func<string, bool>? preparingRoot = null)
{
    public async Task ExecuteAsync(OperationRecord record, OperationContext task)
    {
        var progress = new InstallationProgress(task);
        var request = record.Download ?? throw new IOException("任务缺少下载信息。");
        if (request.Instance != null)
        {
            await new InstanceOperationExecutor(inUse).ExecuteAsync(record, task);
            return;
        }
        if (request.Kind == "update")
        {
            if (inUse?.Invoke(record.Target) == true)
                throw new IOException("请先关闭该实例的游戏再更新。");
            await new ContentService(http, task.Token).Install(
                request.PreparedPlan ?? throw new IOException("缺少更新预览。"), record.Target,
                progress.Message, request.ContentFingerprint ?? throw new IOException("缺少内容指纹。"), progress.Advance);
            return;
        }
        if (request.Kind is "game" or "pack" && preparingRoot?.Invoke(record.Root) == true)
            throw new IOException("此游戏目录有会话正在准备启动，请稍后重试安装。");
        if (request.Kind == "game")
        {
            if (inUse?.Invoke(VersionCatalog.InstanceDirectory(record.Root, request.Version)) == true)
                throw new IOException("该实例正在运行，请关闭游戏后重试安装。");
            var installer = new GameInstaller(http, record.Root, request.Java, progress.Message, task.Token, overallProgress: progress.Advance);
            await installer.Install(new PackRequirements(request.Version, request.Loader, request.LoaderVersion));
            return;
        }
        if (request.Kind == "pack")
        {
            await using (var input = File.OpenRead(request.File))
                if (!Convert.ToHexString(await SHA256.HashDataAsync(input, task.Token)).Equals(request.SourceHash, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("整合包文件在排队后发生变化，请重新导入。");
            var installer = new GameInstaller(http, record.Root, request.Java, progress.Message, task.Token, overallProgress: value => progress.Advance(40 + value * .59));
            await new PackService(http, task.Token).Import(request.File, record.Root, installer.Install, false,
                progress.Message, request.PackInstance, progress.Advance);
            return;
        }
        var version = request.Content ?? throw new IOException("任务缺少资源版本。");
        if (request.Kind == "resource")
        {
            await ResourceDownloads.Download(http, request.Destination, request.ResourceKind, version,
                text => task.Report(text), task.Token);
            return;
        }
        if (request.Kind != "content")
            throw new IOException("未知任务类型。");
        if (inUse?.Invoke(record.Target) == true)
            throw new IOException("该实例正在运行，请关闭游戏后重试安装。");
        var service = new ContentService(http, task.Token);
        var plan = await service.Resolve(version, request.GameVersion, request.Loader);
        progress.Message("已解析必需依赖，准备安装…");
        await service.Install(plan, record.Target, progress.Message, overallProgress: progress.Advance);
    }
}
