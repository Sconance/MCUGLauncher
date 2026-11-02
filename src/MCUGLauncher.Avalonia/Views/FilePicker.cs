using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace MCUGLauncher.Avalonia.Views;
internal static class FilePicker
{
    public static async Task<string?> Folder(Window owner)
    {
        var result = await owner.StorageProvider.OpenFolderPickerAsync(new()
        {
            Title = "选择目录",
            AllowMultiple = false
        });
        return result.FirstOrDefault()?.TryGetLocalPath();
    }

    public static async Task<string?> Open(Window owner, string title, params string[] patterns)
    {
        var result = await owner.StorageProvider.OpenFilePickerAsync(new()
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(title) { Patterns = patterns }]
        });
        return result.FirstOrDefault()?.TryGetLocalPath();
    }

    public static async Task<string?> Save(Window owner)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new()
        {
            Title = "导出整合包",
            SuggestedFileName = "instance-export.zip",
            DefaultExtension = "zip",
            ShowOverwritePrompt = true
        });
        return file?.TryGetLocalPath();
    }
}
