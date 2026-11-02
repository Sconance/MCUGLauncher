using System.Security.Cryptography;
using System.Text.Json;
using MCUGLauncher;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

internal sealed class InstallationCommands(LauncherContext context, Action<string, string, string, DownloadRequest>? enqueue = null)
{
    public async Task LoadersAsync(JsonElement root)
    {
        var game = ReadString(root, "version");
        InstancePath(context.Config.Root, game);
        var loader = ReadString(root, "loader");
        List<string> versions;
        if (loader is "forge" or "neoforge")
            versions = (await LoaderCatalog.Get(context.Http, game, loader, context.Token)).Select(v => v.Version).ToList();
        else if (loader is "fabric" or "quilt")
        {
            var endpoint = loader == "fabric" ? "https://meta.fabricmc.net/v2" : "https://meta.quiltmc.org/v3";
            using var doc = JsonDocument.Parse(await context.Http.GetStringAsync(endpoint + "/versions/loader/" + Uri.EscapeDataString(game), context.Token));
            versions = doc.RootElement.EnumerateArray().Select(v => v.GetProperty("loader").GetProperty("version").GetString()!).Distinct().ToList();
        }
        else
            throw new ArgumentException("不支持的加载器。");
        context.Send(new
        {
            type = "loaderList",
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
