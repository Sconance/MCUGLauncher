using System.Text.Json;
using BlockLaunch;
using MCUGLauncher.Contracts;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

internal sealed class ContentUpdateCommands(LauncherContext context, Func<CancellationToken, IResourceProvider>? createProvider = null)
{
    sealed record Checked(InstanceSnapshot Instance, List<ContentUpdate> Items);
    sealed record Previewed(InstanceSnapshot Instance, ContentUpdatePreview Preview);
    readonly PreviewCache<Checked> checks = PreviewCache<Checked>.For(context);
    readonly PreviewCache<Previewed> previews = PreviewCache<Previewed>.For(context);
    IResourceProvider Provider(CancellationToken token) => createProvider?.Invoke(token) ?? new ContentService(context.Http, token);

    public async Task<ContentUpdatesInfo> CheckAsync(string version)
    {
        var instance = InstanceSnapshot.Capture(context.Config, version);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        var items = await ContentUpdates.Check(Provider(timeout.Token), instance.Directory,
            instance.Requirements.Minecraft, instance.Requirements.Loader, timeout.Token);
        var token = checks.Add(new(instance, items));
        return new(
            token,
            version,
            items.Select(item => new ContentUpdateInfo(
                item.Current.ProjectId,
                item.Current.Name,
                item.Current.VersionId,
                item.Latest?.VersionNumber ?? "",
                item.Latest != null,
                item.Error)).ToArray());
    }

    public async Task<ContentUpdatePreviewInfo> PreviewAsync(JsonElement root)
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
        return new(
            token,
            instance.Key,
            preview.Bytes,
            preview.Changes.Select(change => new ContentChangeInfo(
                change.Project,
                change.Before,
                change.After,
                change.Path,
                change.OldPath ?? "")).ToArray());
    }

    public SavedInfo Apply(string token)
    {
        var preview = previews.Get(token, consume: true);
        context.Tasks.Enqueue("更新 " + preview.Instance.Name, preview.Instance.Root, preview.Instance.Directory,
            new DownloadRequest { Kind = "update", PreparedPlan = preview.Preview.Plan, ContentFingerprint = preview.Preview.Fingerprint });
        return new("更新任务已加入队列。");
    }
}
