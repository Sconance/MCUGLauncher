using System.Text.Json;
using BlockLaunch;
using MCUGLauncher.Contracts;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

// Handles resource use cases; no WPF controls or browser access.
internal sealed class ResourceCommands(LauncherContext context)
{
    public async Task<VanillaVersionsResultInfo> SendVanillaVersionsAsync()
    {
        try
        {
            var json = await context.Http.GetStringAsync("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json", context.Token);
            using var doc = JsonDocument.Parse(json);
            var items = doc.RootElement.GetProperty("versions").EnumerateArray()
                .Where(v => v.GetProperty("type").GetString() == "release")
                .Take(24)
                .Select(v => new VanillaVersionInfo(
                    v.GetProperty("id").GetString() ?? "",
                    v.GetProperty("type").GetString() ?? "release",
                    v.GetProperty("releaseTime").GetDateTimeOffset().ToString("yyyy-MM-dd")))
                .ToList();
            return VanillaVersionsResultInfo.Success(items);
        }
        catch (Exception ex)
        {
            return VanillaVersionsResultInfo.Failure("读取原版版本列表失败：" + ex.Message);
        }
    }

    public Task InstallVanillaAsync(string version)
    {
        InstancePath(context.Config.Root, version);
        context.Tasks.Enqueue("安装原版 " + version, context.Config.Root, version,
            new DownloadRequest { Kind = "game", Version = version, Java = context.Config.Java });
        return Task.CompletedTask;
    }

    public async Task<ContentSearchResultInfo> SearchContentAsync(JsonElement root)
    {
        var category = ReadString(root, "category");
        var requestId = ReadString(root, "requestId");
        if (category is not ("mod" or "modpack" or "resourcepack" or "shader" or "datapack"))
            throw new ArgumentException("未知资源分类。");
        try
        {
            var game = ReadString(root, "game");
            var loader = ReadString(root, "loader");
            if (loader.Length > 0 && loader is not ("vanilla" or "fabric" or "forge" or "neoforge" or "quilt"))
                throw new ArgumentException("未知加载器。");
            using var browse = CancellationTokenSource.CreateLinkedTokenSource(context.Token);
            browse.CancelAfter(TimeSpan.FromSeconds(15));
            var page = await new ContentService(context.Http, browse.Token).Search(ReadString(root, "query"), game, loader, category);
            return new(
                category,
                requestId,
                page.Items.Select(project => new ContentItem(
                    project.Id,
                    project.Title,
                    project.Description,
                    project.Author,
                    project.IconUrl,
                    project.Downloads.ToString(),
                    project.Tags.Split(" · ", StringSplitOptions.RemoveEmptyEntries))).ToArray(),
                "");
        }
        catch (Exception ex)
        {
            return new(category, requestId, Array.Empty<ContentItem>(), "读取资源失败：" + ex.Message);
        }
    }

    public async Task InstallContentAsync(JsonElement root)
    {
        var category = ReadString(root, "category");
        if (category is not ("mod" or "modpack" or "resourcepack" or "shader" or "datapack"))
            throw new ArgumentException("未知资源分类。");
        var id = ReadString(root, "id");
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("缺少项目标识。");
        var catalog = VersionCatalog.Load(context.Config.Root, context.Config.LastInstance);
        var details = VersionCatalog.ReadDetails(context.Config.Root, catalog.Selected);
        var loader = details.Loader == "原版" ? "vanilla" : details.Loader.ToLowerInvariant();
        using var browse = CancellationTokenSource.CreateLinkedTokenSource(context.Token);
        browse.CancelAfter(TimeSpan.FromSeconds(15));
        var service = new ContentService(context.Http, browse.Token);
        var versions = await service.Versions(id, details.Minecraft, loader, category);
        var versionId = ReadString(root, "versionId");
        var version = versions.SingleOrDefault(v => v.Id == versionId) ?? throw new IOException("请通过下载选项选择一个兼容的资源版本。");
        var local = ReadString(root, "mode") == "local" || category is "modpack" or "datapack";
        var target = local ? ReadString(root, "path") : VersionCatalog.InstanceDirectory(context.Config.Root, catalog.Selected);
        if (local && string.IsNullOrWhiteSpace(target))
            target = Path.Combine(context.Store.DataDirectory, "downloads");
        context.Tasks.Enqueue("下载 " + version.Name, context.Config.Root, target,
            new DownloadRequest
            {
                Kind = local ? "resource" : "content",
                Content = version,
                GameVersion = details.Minecraft,
                Loader = loader,
                ResourceKind = category,
                Destination = target
            });
    }

    public Task InstallModAsync(JsonElement root)
    {
        var request = JsonSerializer.SerializeToElement(new
        {
            id = ReadString(root, "id"),
            category = "mod",
            versionId = ReadString(root, "versionId"),
            mode = ReadString(root, "mode"),
            path = ReadString(root, "path")
        });
        return InstallContentAsync(request);
    }
}
