using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.Avalonia.Views;

/// <summary>下载任意 URL 的自定义文件，进度来自任务队列推送的任务快照。</summary>
internal partial class CustomDownloadView : UserControl
{
    private static readonly string[] ReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

    private ILauncherService service = EmptyServiceFallback.Instance;
    private string taskId = "";
    private string targetPath = "";
    private string lastAutoName = "";
    private bool running;
    private bool updatingName;

    public CustomDownloadView()
    {
        InitializeComponent();
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (Directory.Exists(downloads))
        {
            FolderBox.Text = downloads;
        }

        UrlBox.TextChanged += (_, _) => ApplyUrl();
        FolderBox.TextChanged += (_, _) => Refresh();
        FileNameBox.TextChanged += (_, _) =>
        {
            if (!updatingName)
            {
                Refresh();
            }
        };
        PickFolderButton.Click += async (_, _) => await PickFolderAsync();
        StartButton.Click += async (_, _) => await StartAsync();
        CancelButton.Click += async (_, _) => await CancelAsync();
        OpenFolderButton.Click += async (_, _) => await OpenFolderAsync();
        Refresh();
    }

    public CustomDownloadView(ILauncherService service) : this()
    {
        Attach(service);
    }

    public void Attach(ILauncherService value)
    {
        if (ReferenceEquals(service, value))
        {
            return;
        }

        service.TasksChanged -= OnTasksChanged;
        service = value ?? EmptyServiceFallback.Instance;
        service.TasksChanged += OnTasksChanged;
        Refresh();
    }

    private void ApplyUrl()
    {
        var current = FileNameBox.Text ?? "";
        var extracted = ExtractFileName(UrlBox.Text ?? "");
        var wasAutomatic = current.Length == 0 || string.Equals(current, lastAutoName, StringComparison.Ordinal);
        lastAutoName = extracted;
        if (wasAutomatic && !string.Equals(current, extracted, StringComparison.Ordinal))
        {
            updatingName = true;
            FileNameBox.Text = extracted;
            updatingName = false;
        }

        Refresh();
    }

    private async Task PickFolderAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return;
        }

        try
        {
            var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择保存文件夹",
                AllowMultiple = false
            });
            var picked = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            if (!string.IsNullOrEmpty(picked))
            {
                FolderBox.Text = picked;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "无法打开文件夹选择窗口：" + ex.Message;
        }
    }

    private async Task StartAsync()
    {
        if (running || !Validate(out _))
        {
            return;
        }

        var directory = Path.GetFullPath(FolderBox.Text!.Trim());
        var name = FileNameBox.Text!.Trim();
        var target = Path.Combine(directory, name);
        if (File.Exists(target))
        {
            var handle = TopLevel.GetTopLevel(this)?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (!NativeDialogs.Confirm(handle, "文件已存在，是否覆盖？\n" + target, "确认覆盖"))
            {
                return;
            }

            try
            {
                File.Delete(target);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                StatusText.Text = "无法删除已有文件，请确认文件未被其他程序占用。";
                return;
            }
        }

        StartButton.IsEnabled = false;
        try
        {
            var info = await service.StartCustomDownloadAsync(UrlBox.Text!.Trim(), directory, name);
            taskId = info.TaskId;
            targetPath = info.FilePath;
            running = true;
            DownloadProgress.Value = 0;
            CancelButton.IsEnabled = true;
            CancelButton.IsVisible = true;
            OpenFolderButton.IsEnabled = false;
            StatusText.Text = "已加入下载队列，等待开始…";
        }
        catch (Exception ex)
        {
            StatusText.Text = "无法开始下载：" + ex.Message;
            Refresh();
        }
    }

    private async Task CancelAsync()
    {
        if (!running || taskId.Length == 0)
        {
            return;
        }

        CancelButton.IsEnabled = false;
        StatusText.Text = "正在取消，请等待清理完成…";
        try
        {
            await service.TaskActionAsync(taskId, "cancel");
        }
        catch (Exception ex)
        {
            StatusText.Text = "取消失败：" + ex.Message;
            CancelButton.IsEnabled = true;
        }
    }

    private async Task OpenFolderAsync()
    {
        var directory = targetPath.Length > 0 ? Path.GetDirectoryName(targetPath) : FolderBox.Text?.Trim();
        var top = TopLevel.GetTopLevel(this);
        if (top is null || string.IsNullOrEmpty(directory))
        {
            return;
        }

        try
        {
            await top.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(directory));
        }
        catch (Exception ex)
        {
            StatusText.Text = "无法打开文件夹：" + ex.Message;
        }
    }

    private void OnTasksChanged(IReadOnlyList<TaskInfo> tasks)
    {
        if (taskId.Length == 0)
        {
            return;
        }

        TaskInfo? task = null;
        foreach (var item in tasks)
        {
            if (string.Equals(item.Id, taskId, StringComparison.Ordinal))
            {
                task = item;
                break;
            }
        }

        if (task is null)
        {
            return;
        }

        DownloadProgress.Value = Math.Clamp(task.Percent, 0, 100);
        if (running)
        {
            StatusText.Text = Describe(task);
        }

        switch (task.State)
        {
            case "Completed":
                Finish(true, "下载完成：" + targetPath);
                break;
            case "Failed":
                Finish(false, "下载失败：" + task.Message);
                break;
            case "Cancelled":
                Finish(false, "已取消；下载进度已保留，可稍后重试。");
                break;
        }
    }

    private void Finish(bool succeeded, string message)
    {
        running = false;
        taskId = "";
        CancelButton.IsVisible = false;
        CancelButton.IsEnabled = true;
        OpenFolderButton.IsEnabled = succeeded;
        StartButton.IsEnabled = Validate(out _);
        StatusText.Text = message;
    }

    private void Refresh()
    {
        var valid = Validate(out var hint);
        StartButton.IsEnabled = !running && valid && !ReferenceEquals(service, EmptyServiceFallback.Instance);
        if (!running)
        {
            StatusText.Text = hint;
        }
    }

    private bool Validate(out string hint)
    {
        var url = UrlBox.Text?.Trim() ?? "";
        var directory = FolderBox.Text?.Trim() ?? "";
        var name = FileNameBox.Text?.Trim() ?? "";
        if (url.Length == 0)
        {
            hint = "请填写下载地址。";
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            hint = "下载地址格式不正确，仅支持 http 或 https。";
            return false;
        }

        if (directory.Length == 0)
        {
            hint = "请选择保存文件夹。";
            return false;
        }

        if (!Directory.Exists(directory))
        {
            hint = "保存文件夹不存在，请重新选择。";
            return false;
        }

        if (name.Length == 0)
        {
            hint = "请填写文件名。";
            return false;
        }

        if (!IsValidFileName(name))
        {
            hint = "文件名包含非法字符或为系统保留名。";
            return false;
        }

        hint = "准备就绪，点击开始下载。";
        return true;
    }

    private static bool IsValidFileName(string name)
    {
        if (name is "." or ".." || name.Length == 0)
        {
            return false;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return false;
        }

        if (name.EndsWith(' ') || name.EndsWith('.'))
        {
            return false;
        }

        var stem = Path.GetFileNameWithoutExtension(name).ToUpperInvariant();
        return Array.IndexOf(ReservedNames, stem) < 0;
    }

    private static string ExtractFileName(string text)
    {
        text = text.Trim();
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return "";
        }

        var path = uri.AbsolutePath;
        var slash = path.LastIndexOf('/');
        var name = slash >= 0 ? path[(slash + 1)..] : path;
        name = Uri.UnescapeDataString(name).Trim();
        return name is "." or ".." ? "" : name;
    }

    private static string Describe(TaskInfo task)
    {
        var state = task.State switch
        {
            "Queued" => "排队中",
            "Running" => "下载中",
            "Pausing" => "正在暂停",
            "Paused" => "已暂停",
            "Cancelling" => "正在取消",
            "Interrupted" => "已中断",
            _ => task.State
        };
        return $"{state} · {task.Percent:0}% · {task.Message}";
    }
}
