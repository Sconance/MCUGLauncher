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
/// InstanceSnapshot.Capture(AppConfig,string) 冻结目录；PreviewCache.Add/Get 绑定预览。
/// 识别、复制和存档解压复用 Core，不依赖窗口。详见 docs/components/LocalImportCommands.md。
/// </remarks>
internal sealed class LocalImportCommands(LauncherContext context, Action<string, string, string, DownloadRequest>? enqueue = null)
{
    sealed record Choice(InstanceSnapshot Target, string File, string Hash, string Kind);
    readonly PreviewCache<Choice> choices = new();
    public async Task Preview(JsonElement message)
    {
        var file = Path.GetFullPath(ReadString(message, "path"));
        var kind = await Task.Run(() => LocalImportClassifier.Identify(file), context.Token);
        if (kind == LocalImportKind.Modpack)
        {
            context.Send(new
            {
                type = "localImportPreview",
                kind = "modpack",
                path = file
            });
            return;
        }
        var child = kind switch
        {
            LocalImportKind.Mod => "mods",
            LocalImportKind.ResourcePack => "resourcepacks",
            LocalImportKind.ShaderPack => "shaderpacks",
            LocalImportKind.World => "world",
            _ => throw new IOException("无法导入此文件。数据包请安装到具体存档的 datapacks 目录。")
        };
        var target = InstanceSnapshot.Capture(context.Config, context.Config.LastInstance);
        RequireAvailable(target);
        await using var input = File.OpenRead(file);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, context.Token));
        var token = choices.Add(new(target, file, hash, child));
        context.Send(new
        {
            type = "localImportPreview",
            token,
            kind = child,
            path = file,
            name = target.Name,
            target = target.Directory
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
