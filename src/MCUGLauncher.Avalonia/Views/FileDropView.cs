using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.Avalonia.Views;

internal sealed class FileDropView : Border, IDisposable
{
    readonly ILauncherService service;
    readonly Window owner;
    bool busy;

    public FileDropView(ILauncherService service, Window owner)
    {
        this.service = service;
        this.owner = owner;
        IsVisible = false;
        IsHitTestVisible = false;
        Margin = new Thickness(18);
        Padding = new Thickness(30);
        Background = Brush.Parse("#E8F2F8");
        BorderBrush = Brush.Parse("#326EA7");
        BorderThickness = new Thickness(2);
        CornerRadius = new CornerRadius(12);
        Child = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = "松开鼠标以导入", FontSize = 18, FontWeight = FontWeight.Bold },
                new TextBlock
                {
                    Text = "支持模组、资源包、光影、存档与整合包；导入前会检查内容并确认目标。",
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 460,
                    TextAlignment = TextAlignment.Center
                }
            }
        };
        DragDrop.SetAllowDrop(owner, true);
        owner.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        owner.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        owner.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    void OnDragOver(object? sender, DragEventArgs e)
    {
        var supported = !busy && e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().Any() == true;
        e.DragEffects = supported ? DragDropEffects.Copy : DragDropEffects.None;
        IsVisible = supported;
        e.Handled = true;
    }

    void OnDragLeave(object? sender, DragEventArgs e) => IsVisible = false;

    async void OnDrop(object? sender, DragEventArgs e)
    {
        IsVisible = false;
        e.Handled = true;
        var paths = e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>()
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
        if (paths.Length == 0)
            return;
        if (busy)
            return;

        busy = true;
        try
        {
            foreach (var path in paths)
                await LocalImportDialog.Open(service, owner, path);
        }
        catch (Exception error)
        {
            await LocalImportDialog.ShowError(owner, error.Message);
        }
        finally
        {
            busy = false;
        }
    }

    public void Dispose()
    {
        owner.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
        owner.RemoveHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        owner.RemoveHandler(DragDrop.DropEvent, OnDrop);
    }
}
