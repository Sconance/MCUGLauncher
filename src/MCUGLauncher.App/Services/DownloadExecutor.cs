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
            await VerifySource(request.File, request.SourceHash, task.Token, "整合包");
            var installer = new GameInstaller(http, record.Root, request.Java, progress.Message, task.Token, overallProgress: value => progress.Advance(InstallationStages.PackFiles + value * (InstallationStages.BeforeCommit - InstallationStages.PackFiles) / 100));
            await new PackService(http, task.Token).Import(request.File, record.Root, installer.Install, false,
                progress.Message, request.PackInstance, progress.Advance);
            return;
        }
        if (request.Kind == "local")
        {
            var key = ContentService.SafeFile(request.Version);
            if (!File.Exists(Path.Combine(record.Root, "versions", key, key + ".json")))
                throw new IOException("导入目标实例已被移除。");
            if (inUse?.Invoke(record.Target) == true || preparingRoot?.Invoke(record.Root) == true)
                throw new IOException("实例正在运行或准备启动，暂不能导入。");
            progress.Message("正在校验本地导入文件");
            await VerifySource(request.File, request.SourceHash, task.Token, "本地文件");
            progress.Advance(5);
            if (request.ResourceKind == "world")
                await WorldArchives.Import(record.Target, request.File, progress: progress.Message, token: task.Token);
            else
            {
                var count = await new ContentService(http, task.Token).ImportFiles(
                    record.Target,
                    request.ResourceKind,
                    [request.File],
                    value => progress.Advance(5 + value * .9));
                if (count == 0)
                    throw new IOException("同名文件已存在，未覆盖原文件。");
            }
            progress.Advance(95);
            progress.Message("本地文件导入完成");
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

    static async Task VerifySource(string file, string expectedHash, CancellationToken token, string name)
    {
        await using var input = File.OpenRead(file);
        if (!Convert.ToHexString(await SHA256.HashDataAsync(input, token))
                .Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new IOException(name + "在排队后发生变化，请重新导入。");
    }
}
