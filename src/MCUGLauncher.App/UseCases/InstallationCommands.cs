using System.Security.Cryptography;
using System.Text.Json;
using MCUGLauncher;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

/// <summary>列出兼容加载器，预览并确认整合包导入。</summary>
/// <remarks>B 类。LauncherContext.Config.Root/Java、Token、Http 提供目标和网络；Tasks.Enqueue(string,string,string,DownloadRequest)/Send(object) 入队并回推；PreviewCache.Add/Get 保存导入令牌。Core LoaderCatalog.Get、PackService.ReadOverview 负责领域解析。 无窗口/视图依赖；完整成员说明见 docs/components/InstallationCommands.md。</remarks>
internal sealed class InstallationCommands(LauncherContext context, Action<string, string, string, DownloadRequest>? enqueue = null)
{
    public async Task LoadersAsync(JsonElement root)
    {
        var game = ReadString(root, "version");
        InstancePath(context.Config.Root, game);
        var loader = ReadString(root, "loader");
        var versions = (await LoaderCatalog.Get(context.Http, game, loader, context.Token)).Select(v => v.Version).ToList();
        context.Send(new
        {
            type = "loaderList",
            requestId = ReadString(root, "requestId"),
            version = game,
            loader,
            versions
        });
    }

    public void InstallLoader(JsonElement root)
    {
        var game = ReadString(root, "version");
        var loader = ReadString(root, "loader");
        var release = ReadString(root, "loaderVersion");
        InstancePath(context.Config.Root, game);
        if (loader is not ("fabric" or "quilt" or "forge" or "neoforge") || string.IsNullOrWhiteSpace(release))
            throw new ArgumentException("请先选择加载器版本。");
        context.Tasks.Enqueue("安装 " + game + " / " + loader + " " + release, context.Config.Root, game,
            new DownloadRequest { Kind = "game", Version = game, Loader = loader, LoaderVersion = release, Java = context.Config.Java });
        context.Send(new
        {
            type = "loaderQueued",
            requestId = ReadString(root, "requestId")
        });
    }

    sealed record ImportChoice(string Root, string File, string Hash, PackOverview Overview);
    readonly PreviewCache<ImportChoice> imports = new();

    public async Task PreviewPackAsync(JsonElement message)
    {
        var root = context.Config.Root;
        var file = Path.GetFullPath(ReadString(message, "path"));
        var overview = await Task.Run(() => PackService.ReadOverview(file), context.Token);
        await using var input = File.OpenRead(file);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, context.Token));
        var token = imports.Add(new(root, file, hash, overview));
        context.Send(new
        {
            type = "packPreview",
            token,
            name = overview.Name,
            root,
            game = overview.Requirements.Minecraft,
            loader = overview.Requirements.Loader
        });
    }

    public void ConfirmPack(JsonElement message)
    {
        var token = ReadString(message, "token");
        var preview = imports.Get(token);
        var name = ReadString(message, "name").Trim();
        ContentService.SafeFile(name);
        if (name is "." or ".." || name.Length == 0)
            throw new ArgumentException("请输入有效实例名称。");
        var root = Path.GetFullPath(ReadString(message, "directory"));
        if (!Directory.Exists(root))
            throw new ArgumentException("请选择已存在的游戏目录。");
        if (Directory.Exists(Path.Combine(root, "versions", name)) || Directory.Exists(Path.Combine(root, "instances", name)))
            throw new IOException("该目录已存在同名实例，请修改名称。");
        imports.Get(token, true);
        (enqueue ?? context.Tasks.Enqueue)("导入整合包 " + name, root, name,
            new DownloadRequest { Kind = "pack", File = preview.File, SourceHash = preview.Hash, Java = context.Config.Java, PackInstance = name });
        context.Send(new
        {
            type = "packQueued",
            token
        });
    }

    // Legacy callers receive the same confirmation step; picking a file never starts installation.
    public Task ImportPackAsync(JsonElement message) => PreviewPackAsync(message);
}
