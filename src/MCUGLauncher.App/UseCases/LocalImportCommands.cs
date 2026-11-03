using System.Security.Cryptography;
using BlockLaunch;
using MCUGLauncher.Contracts;
using MCUGLauncher.Services;

namespace MCUGLauncher.UseCases;

/// <summary>预览本地文件，确认后把不可变目标提交到现有任务队列。</summary>
/// <remarks>
/// 组件级别：B。
/// 依赖：
/// - LauncherContext.Config、Token、Tasks、Games、IsRootPreparing、IsInstanceInUse、Send；
/// - InstanceSnapshot.Capture(AppConfig, string)、Root、Key、Directory、Name、Requirements；
/// - LauncherTaskQueue.Enqueue(string, string, string, DownloadRequest)、RequireLaunchAvailable(string, string, string)；
/// - RunningGameRegistry.UsesDirectory(string)；
/// - PreviewCache&lt;T&gt;.Add(T)、Get(string, bool)；
/// - LocalImportClassifier.Inspect(string)、ContentService.SafeFile(string)；
/// - Core ContentService.ImportFiles(string, string, IEnumerable&lt;string&gt;, Action&lt;double&gt;?)、WorldArchives.Import(string, string, string?, Action&lt;string&gt;?, CancellationToken)。
/// 不依赖 View、窗口控件、LauncherService 或 LauncherApplication。
/// </remarks>
internal sealed class LocalImportCommands(LauncherContext context, Action<string, string, string, DownloadRequest>? enqueue = null)
{
    sealed record Choice(InstanceSnapshot Target, string File, string Hash, string Kind);

    readonly PreviewCache<Choice> choices = new();

    public async Task<LocalImportPreviewInfo> PreviewAsync(string path, string instance, string resourceKind)
    {
        var file = Path.GetFullPath(path);
        if (!File.Exists(file))
            throw new FileNotFoundException("导入文件不存在。", file);
        if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("不支持通过符号链接导入文件。");

        var detection = await Task.Run(() => LocalImportClassifier.Inspect(file), context.Token);
        if (detection.Kind == LocalImportKind.Modpack)
            return new("", "modpack", file, Path.GetFileName(file), "", false, [], []);
        if (detection.Kind == LocalImportKind.Unknown)
            throw new IOException("未识别到支持的文件内容；JAR 必须包含模组标识，ZIP 必须包含整合包、资源包、光影或存档结构。");

        var kind = detection.Kind;
        if (kind == LocalImportKind.AmbiguousPack)
        {
            if (resourceKind.Length == 0)
                return new("", "ambiguous", file, "", "", false, detection.Choices ?? [], ["包内结构存在歧义，请选择导入用途。"]);
            if (!(detection.Choices ?? []).Contains(resourceKind, StringComparer.Ordinal))
                throw new IOException("所选导入用途与文件结构不匹配。");
            kind = resourceKind == "shaderpacks" ? LocalImportKind.ShaderPack : LocalImportKind.ResourcePack;
        }

        if (kind == LocalImportKind.DataPack)
            return new("", "datapacks", file, "", "", false, [], ["这是数据包，请手动安装到具体存档的 datapacks 目录。"]);

        var child = kind switch
        {
            LocalImportKind.Mod => "mods",
            LocalImportKind.ResourcePack => "resourcepacks",
            LocalImportKind.ShaderPack => "shaderpacks",
            LocalImportKind.World => "world",
            _ => throw new IOException("此文件类型暂不支持导入。")
        };
        var selected = string.IsNullOrWhiteSpace(instance) ? context.Config.LastInstance : instance;
        var target = InstanceSnapshot.Capture(context.Config, selected);
        RequireAvailable(target);

        await using var input = File.OpenRead(file);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, context.Token));
        var warnings = new List<string>();
        if (kind == LocalImportKind.Mod)
        {
            var loader = target.Requirements.Loader.ToLowerInvariant();
            if (!detection.Loaders.Contains(loader, StringComparer.Ordinal)
                && !(loader == "quilt" && detection.Loaders.Contains("fabric", StringComparer.Ordinal)))
                throw new IOException("模组需要 " + string.Join(" / ", detection.Loaders)
                    + "，所选实例加载器为 " + loader + "；请选择兼容实例。");
            warnings.Add("已检查加载器类型；游戏版本范围、前置依赖和不同文件名的重复 Mod ID 尚需核对。");
        }

        var name = ContentService.SafeFile(Path.GetFileName(file));
        var destination = Path.Combine(target.Directory, child, name);
        var conflict = child != "world" && (File.Exists(destination)
            || File.Exists(destination + ".disabled")
            || Directory.Exists(destination)
            || Directory.Exists(destination + ".disabled"));
        if (conflict)
            warnings.Add("同名文件或禁用文件已存在，请先处理原文件；不会覆盖。");
        if (kind == LocalImportKind.World)
            warnings.Add("存档将创建独立的新目录，不覆盖已有存档。");

        var token = conflict ? "" : choices.Add(new(target, file, hash, child));
        return new(token, child, file, target.Name, target.Directory, !conflict, [], warnings);
    }

    public Task<SavedInfo> ConfirmAsync(string token)
    {
        var choice = choices.Get(token);
        RequireAvailable(choice.Target);
        (enqueue ?? context.Tasks.Enqueue)(
            "导入 " + Path.GetFileName(choice.File),
            choice.Target.Root,
            choice.Target.Directory,
            new DownloadRequest
            {
                Kind = "local",
                Version = choice.Target.Key,
                File = choice.File,
                SourceHash = choice.Hash,
                ResourceKind = choice.Kind
            });
        choices.Get(token, true);
        return Task.FromResult(new SavedInfo("本地文件导入请求已提交，请查看任务列表。"));
    }

    void RequireAvailable(InstanceSnapshot target)
    {
        if (!File.Exists(Path.Combine(target.Root, "versions", target.Key, target.Key + ".json")))
            throw new IOException("导入目标实例已被移除，请重新选择。");
        context.Tasks.RequireLaunchAvailable(target.Root, target.Key, target.Directory);
        if (context.IsInstanceInUse(target.Directory) || context.IsRootPreparing(target.Root))
            throw new IOException("实例正在运行或准备启动，暂不能导入。");
    }
}
