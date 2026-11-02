using System.Text.Json;
using MCUGLauncher;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

/// <summary>检查、预览并确认指定实例的内容更新。</summary>
/// <remarks>B 类。LauncherContext.Config/Http/Token 提供配置和请求；Tasks.Enqueue(string,string,string,DownloadRequest)/Send(object) 入队并回推；InstanceSnapshot.Capture(AppConfig,string).Root/Key/Name/Directory/Requirements 冻结目标；PreviewCache.Add/Get 保存检查和预览。Core ContentUpdates.Check/Preview 执行领域逻辑。 无窗口/视图依赖；完整成员说明见 docs/components/ContentUpdateCommands.md。</remarks>
internal sealed class ContentUpdateCommands(LauncherContext context, Func<CancellationToken, IResourceProvider>? createProvider = null)
{
    sealed record Checked(InstanceSnapshot Instance, List<ContentUpdate> Items);
    sealed record Previewed(InstanceSnapshot Instance, ContentUpdatePreview Preview);
    readonly PreviewCache<Checked> checks = new();
    readonly PreviewCache<Previewed> previews = new();
    IResourceProvider Provider(CancellationToken token) => createProvider?.Invoke(token) ?? new ContentService(context.Http, token);

    public async Task CheckAsync(string version)
    {
        var instance = InstanceSnapshot.Capture(context.Config, version);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        var items = await ContentUpdates.Check(Provider(timeout.Token), instance.Directory,
            instance.Requirements.Minecraft, instance.Requirements.Loader, timeout.Token);
        var token = checks.Add(new(instance, items));
        context.Send(new
        {
            type = "contentUpdates",
            token,
            version,
            items = items.Select(i => new
            {
                id = i.Current.ProjectId,
                name = i.Current.Name,
                before = i.Current.VersionId,
                after = i.Latest?.VersionNumber,
                available = i.Latest != null,
                error = i.Error
            })
        });
    }

    public async Task PreviewAsync(JsonElement root)
    {
        var check = checks.Get(ReadString(root, "token"));
        var ids = root.GetProperty("projects").EnumerateArray().Select(i => i.GetString() ?? "").ToHashSet();
        if (ids.Count is 0 or > 128)
            throw new ArgumentException("请选择 1–128 项更新。");
        var selected = check.Items.Where(i => ids.Contains(i.Current.ProjectId) && i.Latest != null).Select(i => i.Latest!).ToList();
        if (selected.Count != ids.Count)
            throw new ArgumentException("所选项目不在当前更新结果中。");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        var instance = check.Instance;
        var preview = await ContentUpdates.Preview(Provider(timeout.Token), instance.Directory, selected,
            instance.Requirements.Minecraft, instance.Requirements.Loader, timeout.Token);
        var token = previews.Add(new(instance, preview));
        context.Send(new
        {
            type = "contentUpdatePreview",
            token,
            version = instance.Key,
            bytes = preview.Bytes,
            changes = preview.Changes.Select(c => new { c.Project, c.Before, c.After, c.Path, c.OldPath })
        });
    }

    public void Apply(string token)
    {
        var preview = previews.Get(token, consume: true);
        context.Tasks.Enqueue("更新 " + preview.Instance.Name, preview.Instance.Root, preview.Instance.Directory,
            new DownloadRequest { Kind = "update", PreparedPlan = preview.Preview.Plan, ContentFingerprint = preview.Preview.Fingerprint });
        context.Send(new
        {
            type = "contentUpdateQueued",
            token
        });
    }
}
