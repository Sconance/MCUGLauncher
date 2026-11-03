using Avalonia.Controls;
using Avalonia.Threading;

namespace MCUGLauncher.Avalonia.Views;

/// <summary>拖放与文件选择共享的确认流程；识别、兼容检查和入队全部交给应用接口。</summary>
internal static class LocalImportDialog
{
    public static async Task Open(ILauncherService session, UiContext ui, Window owner, string path)
    {
        var available = session.State.Instances.Where(i => !i.IsPlaceholder).ToArray();
        var instances = Ui.Select(available);
        instances.SelectedItem = available.FirstOrDefault(i => i.Name == session.State.SelectedInstance) ?? available.FirstOrDefault();
        var summary = Ui.Text("正在识别文件内容…");
        LocalImportPreviewInfo? preview = null;
        var kinds = Ui.Select(Array.Empty<ImportKind>());
        var kindField = Ui.Field("导入用途（结构有歧义时手动选择）", kinds);
        kindField.IsVisible = false;
        var revision = 0;
        var closed = false;
        var queued = false;
        var pack = false;
        var install = Ui.Button("开始导入", async () =>
        {
            if (preview is not { CanImport: true } choice || choice.Token.Length == 0)
                throw new InvalidOperationException("请先选择可用目标并完成检查");
            // The receipt is awaited: an error keeps this dialog open for correction/retry.
            await session.ConfirmLocalImportAsync(token: choice.Token);
            queued = true;
            (TopLevel.GetTopLevel(summary) as Window)?.Close();
        }, true);
        install.IsEnabled = false;
        async Task Read()
        {
            var current = ++revision;
            preview = null;
            install.IsEnabled = false;
            summary.Text = "正在识别和检查…";
            try
            {
                var result = await session.PreviewLocalImportAsync(path, (instances.SelectedItem as InstanceInfo)?.Name ?? "", (kinds.SelectedItem as ImportKind)?.Value ?? "");
                if (closed || current != revision)
                    return;
                if (result.Kind == "modpack")
                {
                    pack = true;
                    (TopLevel.GetTopLevel(summary) as Window)?.Close();
                    return;
                }
                if (result.Kind == "ambiguous")
                {
                    kindField.IsVisible = true;
                    kinds.ItemsSource = result.Choices.Select(c => new ImportKind(c)).ToArray();
                    kinds.SelectedIndex = -1;
                    summary.Text = string.Join("\n", result.Warnings);
                    return;
                }
                preview = result;
                summary.Text = "识别类型：" + Kind(result.Kind) + "\n安装目录：" + result.Target +
                    "\n同名文件不会覆盖" + (result.Warnings.Length == 0 ? "" : "\n\n" + string.Join("\n", result.Warnings));
                install.IsEnabled = result.CanImport && result.Token.Length > 0;
            }
            catch (Exception ex)
            {
                if (!closed && current == revision)
                {
                    summary.Text = ex.Message;
                }
            }
        }
        instances.SelectionChanged += async (_, _) => await Read();
        kinds.SelectionChanged += async (_, _) => { if (kinds.SelectedItem != null) await Read(); };
        var content = Ui.Stack(Ui.Text(Path.GetFileName(path), 16), Ui.Field("目标实例", instances), kindField, summary,
            Ui.Button("重新检查", Read));
        var started = false;
        content.AttachedToVisualTree += (_, _) =>
        {
            if (started)
                return;
            started = true;
            // Wait until the dialog is attached and ShowDialog has installed its modal lifetime.
            Dispatcher.UIThread.Post(async () => { if (!closed) await Read(); });
        };
        try
        {
            await Ui.Show(owner, "确认导入", content, 650, 460, Ui.DialogActions(content, install),
                "识别文件内容 → 选择实例 → 检查冲突 → 确认入队");
        }
        finally { closed = true; revision++; }
        if (pack)
            await InstallDialogs.ImportPath(session, ui, owner, path);
        else if (queued)
            ui.Report("已提交导入任务，可在右上角查看进度或取消");
    }
    sealed record ImportKind(string Value)
    {
        public override string ToString() => Kind(Value);
    }
    static string Kind(string value) => value switch
    {
        "mods" => "模组",
        "resourcepacks" => "资源包",
        "shaderpacks" => "光影包",
        "world" => "世界存档",
        _ => value
    };
    public static async Task Pick(ILauncherService session, UiContext ui, Window owner)
    {
        var path = await FilePicker.Open(owner, "导入本地文件", "*.jar", "*.zip", "*.mrpack");
        if (path != null)
            await Open(session, ui, owner, path);
    }
}
