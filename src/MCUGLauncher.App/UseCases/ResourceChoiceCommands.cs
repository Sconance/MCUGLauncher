using System.Text.Json;
using BlockLaunch;
using MCUGLauncher.Contracts;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

// Freeze search filters and target before the user confirms an exact resource version.
internal sealed class ResourceChoiceCommands(LauncherContext context, HttpClient? browseHttp = null, Action<string, string, string, DownloadRequest>? enqueue = null)
{
    sealed record Choice(string Root, string Instance, string Target, string Category, string Game, string Loader, List<ContentVersion> Versions);
    readonly PreviewCache<Choice> choices = PreviewCache<Choice>.For(context);

    public async Task<ResourceVersionsResultInfo> ListAsync(JsonElement message)
    {
        try
        {
            var root = context.Config.Root;
            var category = ReadString(message, "category");
            if (category is not ("mod" or "modpack" or "resourcepack" or "shader" or "datapack"))
                throw new ArgumentException("未知资源分类。");
            var instance = ReadString(message, "instance");
            var target = "";
            var game = ReadString(message, "game");
            var loader = ReadString(message, "loader");
            if (instance.Length > 0)
            {
                var snapshot = InstanceSnapshot.Capture(context.Config, instance);
                target = snapshot.Directory;
                game = snapshot.Requirements.Minecraft;
                loader = snapshot.Requirements.Loader;
            }
            if (loader.Length > 0 && loader is not ("vanilla" or "fabric" or "forge" or "neoforge" or "quilt"))
                throw new ArgumentException("未知加载器。");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var versions = (await new ContentService(browseHttp ?? context.Http, timeout.Token)
                .Versions(ReadString(message, "id"), game, loader, category)).Take(200).ToList();
            var token = choices.Add(new(root, instance, target, category, game, loader, versions));
            return ResourceVersionsResultInfo.Success(new(
                ReadString(message, "requestId"),
                token,
                game,
                loader,
                versions.Select(version => new ResourceVersionInfo(
                    version.Id,
                    version.Name,
                    version.Files.Select(file => file.Filename).ToArray())).ToArray()));
        }
        catch (Exception error)
        {
            return ResourceVersionsResultInfo.Failure(error.Message);
        }
    }

    public void Queue(JsonElement message)
    {
        var token = ReadString(message, "token");
        var choice = choices.Get(token);
        var version = choice.Versions.SingleOrDefault(v => v.Id == ReadString(message, "versionId")) ?? throw new ArgumentException("请选择列表中的资源版本。");
        var local = ReadString(message, "mode") == "local";
        if (!local && (choice.Target.Length == 0 || choice.Category is "modpack" or "datapack"))
            throw new ArgumentException("此内容请选择自定义下载目录。");
        var target = local ? Path.GetFullPath(ReadString(message, "path")) : choice.Target;
        if (local && !Directory.Exists(target))
            throw new ArgumentException("请选择已存在的下载目录。");
        if (!local && !File.Exists(InstancePath(choice.Root, choice.Instance) + Path.DirectorySeparatorChar + choice.Instance + ".json"))
            throw new IOException("目标实例已被移除。");
        choices.Get(token, true);
        var request = new DownloadRequest
        {
            Kind = local ? "resource" : "content",
            Content = version,
            GameVersion = choice.Game,
            Loader = choice.Loader,
            ResourceKind = choice.Category,
            Destination = target
        };
        (enqueue ?? context.Tasks.Enqueue)("下载 " + version.Name, choice.Root, target, request);
        context.Send(new
        {
            type = "resourceQueued",
            token
        });
    }
}
