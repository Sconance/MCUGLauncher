using System.Security.Cryptography;
using System.Text.Json;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

/// <summary>预览本地拖入文件，确认后冻结目标并提交现有任务队列。</summary>
/// <remarks>
/// B 类。LauncherContext.Config.Root/LastInstance 提供目标；Token 取消识别和哈希；
/// Games.UsesDirectory(string)/Tasks.RequireLaunchAvailable(string,string,string) 检查占用；
/// Tasks.Enqueue(string,string,string,DownloadRequest)/Send(object) 入队并返回结果。
/// InstanceSnapshot.Capture(AppConfig,string).Root/Key/Name/Directory/Requirements.Loader 冻结目标并核对加载器；PreviewCache.Add/Get 绑定预览。
/// 识别、复制和存档解压复用 Core，不依赖窗口。详见 docs/components/LocalImportCommands.md。
/// </remarks>
internal sealed class LocalImportCommands(LauncherContext context, Action<string, string, string, DownloadRequest>? enqueue = null)
{
    sealed record Choice(InstanceSnapshot Target, string File, string Hash, string Kind);
    readonly PreviewCache<Choice> choices = new();
    public async Task Preview(JsonElement message)
    {
        var file = Path.GetFullPath(ReadString(message, "path"));
        var detection = await Task.Run(() => LocalImportClassifier.Inspect(file), context.Token);
        var kind = detection.Kind;
        if (kind == LocalImportKind.Modpack)
        {
            context.Send(new
            {
                type = "localImportPreview",
                requestId = ReadString(message, "requestId"),
                kind = "modpack",
                path = file
            });
            return;
        }
        var selectedKind = ReadString(message, "resourceKind");
        if (kind == LocalImportKind.AmbiguousPack)
        {
            if (selectedKind.Length == 0)
            {
                context.Send(new
                {
                    type = "localImportPreview",
                    requestId = ReadString(message, "requestId"),
                    kind = "ambiguous",
                    path = file,
                    canImport = false,
                    choices = detection.Choices ?? [],
                    warnings = new[] { "内部结构存在歧义，请明确选择用途；数据包仍需手动安装到具体存档" }
                });
                return;
            }
            if (!(detection.Choices ?? []).Contains(selectedKind))
                throw new IOException("所选导入用途与文件结构不匹配");
            kind = selectedKind == "shaderpacks" ? LocalImportKind.ShaderPack : LocalImportKind.ResourcePack;
        }
        var child = kind switch
        {
            LocalImportKind.Mod => "mods",
            LocalImportKind.ResourcePack => "resourcepacks",
            LocalImportKind.ShaderPack => "shaderpacks",
            LocalImportKind.World => "world",
            LocalImportKind.DataPack => throw new IOException("这是数据包，请安装到具体存档的 datapacks 目录；当前导入入口暂不支持选择存档"),
            LocalImportKind.AmbiguousPack => throw new IOException("包内同时包含多种资源或缺少明确目录，无法安全决定安装位置；请拆分或手动确认包的用途"),
            _ => throw new IOException("未识别到支持的文件内容；JAR 必须包含模组标识，ZIP 必须包含整合包、资源包、光影或存档结构")
        };
        var instance = ReadString(message, "instance");
        var target = InstanceSnapshot.Capture(context.Config, string.IsNullOrEmpty(instance) ? context.Config.LastInstance : instance);
        RequireAvailable(target);
        await using var input = File.OpenRead(file);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, context.Token));
        var warnings = new List<string>();
        if (kind == LocalImportKind.Mod)
        {
            var loader = target.Requirements.Loader.ToLowerInvariant();
            if (!detection.Loaders.Contains(loader) && !(loader == "quilt" && detection.Loaders.Contains("fabric")))
                throw new IOException("模组需要 " + string.Join(" / ", detection.Loaders) + "，所选实例加载器为 " + loader + "；请选择兼容实例");
            warnings.Add("已检查加载器类型；游戏版本范围、前置依赖和不同文件名的重复 Mod ID 尚需核对");
        }
        var destination = Path.Combine(target.Directory, child, Path.GetFileName(file));
        var conflict = child != "world" && (File.Exists(destination) || File.Exists(destination + ".disabled") || Directory.Exists(destination) || Directory.Exists(destination + ".disabled"));
        if (conflict)
            warnings.Add("同名文件或禁用文件已存在，请先处理原文件；不会覆盖");
        if (kind == LocalImportKind.World)
            warnings.Add("存档将创建独立的新目录，不覆盖已有存档");
        var token = conflict ? "" : choices.Add(new(target, file, hash, child));
        context.Send(new
        {
            type = "localImportPreview",
            requestId = ReadString(message, "requestId"),
            token,
            kind = child,
            path = file,
            name = target.Name,
            target = target.Directory,
            canImport = !conflict,
            warnings = warnings.ToArray()
        });
    }
    public void Confirm(JsonElement message)
    {
        var token = ReadString(message, "token");
        var choice = choices.Get(token);
        RequireAvailable(choice.Target);
        (enqueue ?? context.Tasks.Enqueue)("导入 " + Path.GetFileName(choice.File), choice.Target.Root, choice.Target.Directory,
            new DownloadRequest { Kind = "local", Version = choice.Target.Key, File = choice.File, SourceHash = choice.Hash, ResourceKind = choice.Kind });
        choices.Get(token, true);
        context.Send(new
        {
            type = "localImportQueued",
            requestId = ReadString(message, "requestId"),
            token
        });
    }
    void RequireAvailable(InstanceSnapshot target)
    {
        if (!File.Exists(Path.Combine(target.Root, "versions", target.Key, target.Key + ".json")))
            throw new IOException("导入目标实例已被移除，请重新选择。 ");
        context.Tasks.RequireLaunchAvailable(target.Root, target.Key, target.Directory);
        if (context.Games.UsesDirectory(target.Directory))
            throw new IOException("请先关闭此实例的游戏再导入。");
    }
}
