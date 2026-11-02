using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace MCUGLauncher.Avalonia.Views;

// Native file gesture and confirmation only; classification and installation remain in App/Core.
internal sealed class FileDropView : Border, IDisposable
{
    readonly LauncherFacade session;
    readonly Window owner;
    bool busy;
    public FileDropView(LauncherFacade session, Window owner)
    {
        this.session = session;
        this.owner = owner;
        IsVisible = false;
        IsHitTestVisible = false;
        Margin = new Thickness(18);
        Padding = new Thickness(30);
        Background = Brush.Parse("#E8F2F8");
        BorderBrush = Brush.Parse("#006DB8");
        BorderThickness = new Thickness(2);
        CornerRadius = new CornerRadius(12);
        Child = Ui.Stack(Ui.Heading("松开鼠标以导入"), Ui.Text("支持整合包、模组、资源包、光影和存档；安装前会确认目标"));
        DragDrop.SetAllowDrop(owner, true);
        owner.AddHandler(DragDrop.DragOverEvent, Over);
        owner.AddHandler(DragDrop.DragLeaveEvent, Leave);
        owner.AddHandler(DragDrop.DropEvent, Drop);
    }
    void Over(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        var supported = !busy && files?.Any(f => f is IStorageFile) == true;
        e.DragEffects = supported ? DragDropEffects.Copy : DragDropEffects.None;
        IsVisible = supported;
        e.Handled = true;
    }
    void Leave(object? sender, DragEventArgs e) => IsVisible = false;
    async void Drop(object? sender, DragEventArgs e)
    {
        IsVisible = false;
        e.Handled = true;
        var paths = e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().Select(f => f.TryGetLocalPath()).Where(p => p != null).Cast<string>().ToArray() ?? [];
        await Import(paths);
    }
    internal async Task Import(IEnumerable<string> paths)
    {
        if (busy)
            return;
        busy = true;
        try
        {
            foreach (var path in paths)
            {
                try
                {
                    var preview = await session.RequestAsync("localImportPreview", new
                    {
                        type = "previewLocalImport",
                        path
                    });
                    if (preview.Kind == "modpack")
                        await InstallDialogs.ImportPath(session, owner, preview.Path);
                    else if (await Ui.Confirm(owner, "确认导入", Path.GetFileName(path) + "\n目标实例：" + preview.Name + "\n目录：" + preview.Target + "\n同名文件不会覆盖", "开始导入"))
                        await session.RequestAsync("localImportQueued", new
                        {
                            type = "confirmLocalImport",
                            token = preview.Token
                        }, m => m.Token == preview.Token);
                }
                catch (Exception error) { await Ui.Error(owner, error.Message); }
            }
        }
        finally { busy = false; }
    }
    public void Dispose()
    {
        owner.RemoveHandler(DragDrop.DragOverEvent, Over);
        owner.RemoveHandler(DragDrop.DragLeaveEvent, Leave);
        owner.RemoveHandler(DragDrop.DropEvent, Drop);
    }
}
