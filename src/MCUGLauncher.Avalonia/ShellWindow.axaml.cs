using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MCUGLauncher.Avalonia.Views;

namespace MCUGLauncher.Avalonia;

/// <summary>原生 Avalonia 外壳：导航、窗口生命周期与页面宿主；业务由 LauncherFacade 承担。</summary>
public partial class ShellWindow : Window
{
    LauncherFacade facade = null!;
    readonly ShellDesktopHost host;
    readonly SettingsView settingsView;
    readonly DispatcherTimer toastTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    StackPanel? pageContent;
    string exportVersion = "";
    LauncherState? typed;
    IReadOnlyList<TaskInfo> tasks = [];
    IReadOnlyList<ContentItem> contentItems = [];
    ContentItem? selectedContent;
    ResourceVersionsInfo? availableVersions;
    string versionRequest = "";
    string downloadCategory = "mod";
    string pendingLocalVersion = "";
    UninstallPreviewInfo? uninstallPreview;
    ContentUpdatesInfo? updates;
    ContentUpdatePreviewInfo? updatePreview;
    string page = "home";

    public ShellWindow()
    {
        InitializeComponent();
        host = new ShellDesktopHost(
            () => Handle,
            message => facade.SendMessage(message),
            HandleWindow,
            DragWindow,
            ResetWindow);
        facade = new LauncherFacade(host);
        settingsView = new SettingsView(facade);
        Width = Math.Clamp(facade.Config.WindowWidth, 900, 7680);
        Height = Math.Clamp(facade.Config.WindowHeight, 600, 4320);
        if (facade.Config.WindowMaximized)
            WindowState = WindowState.Maximized;
        Closing += (_, _) =>
        {
            try
            {
                facade.RememberWindow(Bounds.Width, Bounds.Height, WindowState == WindowState.Maximized);
            }
            catch
            {
                // A locked settings file must not prevent closing.
            }
        };
        toastTimer.Tick += (_, _) =>
        {
            ToastLayer.IsVisible = false;
            toastTimer.Stop();
        };
        facade.StateChanged += snapshot =>
        {
            typed = snapshot;
            if (page is "home" or "mods" or "maintenance" or "account")
                Render();
        };
        facade.TasksChanged += list =>
        {
            tasks = list;
            if (page == "tasks")
                Render();
        };
        facade.ContentChanged += list =>
        {
            contentItems = list;
            if (page == "download")
                Render();
        };
        facade.ResourceVersionsReceived += info =>
        {
            if (info.RequestId == versionRequest)
            {
                availableVersions = info;
                if (page == "download")
                    Render();
            }
        };
        facade.UninstallPreviewReceived += info =>
        {
            uninstallPreview = info;
            if (page == "maintenance")
                Render();
        };
        facade.ContentUpdatesReceived += info =>
        {
            updates = info;
            if (page == "maintenance")
                Render();
        };
        facade.ContentUpdatePreviewReceived += info =>
        {
            updatePreview = info;
            if (page == "maintenance")
                Render();
        };
        facade.PathPicked += info =>
        {
            if (info.Target == "export" && exportVersion.Length > 0)
            {
                var version = exportVersion;
                exportVersion = "";
                _ = facade.ExportInstanceAsync(version, info.Path, false, false);
                ShowToast("导出请求已提交，请查看任务列表。");
            }
            else if (info.Target == "download" && pendingLocalVersion.Length > 0)
            {
                var versionId = pendingLocalVersion;
                pendingLocalVersion = "";
                var token = availableVersions?.Token ?? "";
                if (token.Length > 0)
                {
                    _ = facade.QueueContentVersionAsync(token, versionId, "local", info.Path);
                    ShowToast("下载请求已提交，请查看任务列表。");
                }
            }
        };
        facade.LaunchResultReceived += info => ShowToast(info.Message);
        facade.AuthResultReceived += info => ShowToast(info.Message);
        facade.ErrorReceived += message => ShowToast("操作失败：" + message);
        facade.ToastReceived += info => ShowToast(info.Message);
        HomeButton.Click += (_, _) => Show("home");
        DownloadButton.Click += (_, _) => Show("download");
        MaintenanceButton.Click += (_, _) => Show("maintenance");
        AccountButton.Click += (_, _) => Show("account");
        TasksButton.Click += (_, _) => { page = "tasks"; _ = facade.RequestTasksAsync(); Render(); };
        ModsButton.Click += (_, _) => Show("mods");
        SettingsButton.Click += (_, _) => Show("settings");
        ClassicButton.Click += (_, _) => new MainWindow().Show();
        Opened += async (_, _) => await facade.StartAsync();
        Closed += (_, _) => facade.Dispose();
    }

    void Show(string next)
    {
        page = next;
        if (next == "download")
        {
            selectedContent = null;
            availableVersions = null;
            versionRequest = "";
        }
        Render();
    }

    void Render()
    {
        if (page == "settings")
        {
            PageHost.Content = settingsView;
            return;
        }
        var panel = new StackPanel { Spacing = 12 };
        pageContent = panel;
        PageHost.Content = new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        if (page == "mods")
            RenderMods();
        else if (page == "maintenance")
            RenderMaintenance();
        else if (page == "download")
            RenderDownload();
        else if (page == "account")
            RenderAccount();
        else if (page == "tasks")
            RenderTasks();
        else
            RenderHome();
    }

    void RenderHome()
    {
        var view = new HomeView();
        view.Render(typed);
        view.LaunchRequested += name => _ = facade.LaunchAsync(name);
        Add(view);
        var refresh = new Button { Content = "刷新状态", HorizontalAlignment = HorizontalAlignment.Left };
        refresh.Click += async (_, _) => await facade.RequestStateAsync();
        Add(refresh);
    }

    void RenderMods()
    {
        var snapshot = typed;
        AddHeading("模组");
        Add("当前实例：" + (snapshot?.SelectedVersion ?? ""));
        if (snapshot is not { Mods.Count: > 0 })
        {
            Add("当前实例没有可管理的模组。");
            return;
        }
        foreach (var mod in snapshot.Mods)
        {
            var check = new CheckBox { Content = mod.Name, IsChecked = mod.Enabled };
            var file = mod.File;
            var enabled = mod.Enabled;
            check.Click += async (_, _) =>
            {
                await facade.ToggleModAsync(file, !enabled);
                await facade.RequestStateAsync();
            };
            Add(check);
        }
        Add("切换后会自动刷新模组状态。");
    }

    void RenderMaintenance()
    {
        var snapshot = typed;
        AddHeading("实例维护");
        if (snapshot is not { Instances.Count: > 0 })
        {
            Add("尚未找到已安装实例。");
            return;
        }
        var cloneName = new TextBox { Watermark = "新实例名称", Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
        Add(cloneName);
        foreach (var instance in snapshot.Instances)
        {
            if (instance.Name.Length == 0 || instance.Loader == "请安装版本")
                continue;
            Add(instance.DisplayName + (instance.Minecraft.Length > 0 ? " · " + instance.Minecraft : ""));
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var clone = new Button { Content = "克隆" };
            clone.Click += async (_, _) =>
            {
                var name = cloneName.Text?.Trim() ?? "";
                if (name.Length == 0)
                {
                    ShowToast("请先填写新实例名称。");
                    return;
                }
                await facade.CloneInstanceAsync(instance.Name, name);
                ShowToast("克隆请求已提交，请查看任务列表。");
            };
            var export = new Button { Content = "导出" };
            export.Click += (_, _) => { exportVersion = instance.Name; host.PickSaveFile("export"); };
            var check = new Button { Content = "检查更新" };
            check.Click += async (_, _) =>
            {
                ShowToast("正在检查内容更新…");
                await facade.CheckUpdatesAsync(instance.Name);
            };
            var uninstall = new Button { Content = "卸载预览" };
            uninstall.Click += async (_, _) => await facade.PreviewUninstallAsync(instance.Name);
            var delete = new Button { Content = "删除版本" };
            delete.Click += async (_, _) => await facade.PreviewDeleteVersionAsync(instance.Name);
            row.Children.Add(clone);
            row.Children.Add(export);
            row.Children.Add(check);
            row.Children.Add(uninstall);
            row.Children.Add(delete);
            Add(row);
        }

        if (uninstallPreview is { Paths.Count: > 0 } preview)
        {
            AddHeading("卸载预览 · " + preview.Name);
            Add(preview.Notice);
            foreach (var path in preview.Paths)
                Add("· " + path);
            var confirm = new CheckBox { Content = "我已检查删除范围并确认备份" };
            var apply = new Button { Content = "确认卸载", IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Left };
            confirm.Click += (_, _) => apply.IsEnabled = confirm.IsChecked == true;
            apply.Click += async (_, _) =>
            {
                var token = preview.Token;
                uninstallPreview = null;
                await facade.ApplyUninstallAsync(token);
                ShowToast("卸载请求已提交，请在系统确认框核对。");
                Render();
            };
            Add(confirm);
            Add(apply);
        }
        else
        {
            Add("选择实例后可克隆、导出或预览卸载范围。卸载必须先查看路径。");
        }

        if (updates is { Items.Count: > 0 } checkedUpdates)
        {
            AddHeading("内容更新");
            var selected = new List<(string Id, CheckBox Box)>();
            foreach (var item in checkedUpdates.Items)
            {
                var box = new CheckBox
                {
                    Content = item.Name + " · " + item.Before + " → " + (item.After.Length > 0 ? item.After : item.Error),
                    IsEnabled = item.Available,
                    IsChecked = item.Available
                };
                selected.Add((item.Id, box));
                Add(box);
            }
            var previewButton = new Button { Content = "生成所选更新预览", HorizontalAlignment = HorizontalAlignment.Left };
            previewButton.Click += async (_, _) =>
            {
                var projects = selected.Where(pair => pair.Box.IsChecked == true).Select(pair => pair.Id).ToArray();
                if (projects.Length == 0)
                {
                    ShowToast("请先选择更新项目。");
                    return;
                }
                updatePreview = null;
                ShowToast("正在解析更新及必需依赖…");
                await facade.PreviewUpdatesAsync(checkedUpdates.Token, projects);
            };
            Add(previewButton);
        }

        if (updatePreview is { Changes.Count: > 0 } previewUpdate)
        {
            AddHeading("更新预览 · " + (previewUpdate.Bytes / 1048576.0).ToString("0.00") + " MiB");
            foreach (var change in previewUpdate.Changes)
                Add($"{change.Project}: {change.Before} → {change.After}\n{change.Path}");
            var apply = new Button { Content = "按预览加入更新队列", HorizontalAlignment = HorizontalAlignment.Left };
            apply.Click += async (_, _) =>
            {
                var token = previewUpdate.Token;
                updatePreview = null;
                await facade.ApplyUpdatesAsync(token);
                ShowToast("更新请求已提交，请查看任务列表。");
                Render();
            };
            Add(apply);
        }
    }

    void RenderDownload()
    {
        AddHeading("下载");
        var categoryValues = new[] { "mod", "modpack", "resourcepack", "shader", "datapack" };
        var categoryLabels = new[] { "模组", "整合包", "资源包", "光影", "数据包" };
        var category = new ComboBox
        {
            ItemsSource = categoryLabels,
            SelectedIndex = Math.Max(0, Array.IndexOf(categoryValues, downloadCategory)),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var query = new TextBox { Watermark = "搜索关键词", Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
        Add(category);
        Add(query);
        var search = new Button { Content = "搜索", HorizontalAlignment = HorizontalAlignment.Left };
        search.Classes.Add("primary");
        search.Click += async (_, _) =>
        {
            selectedContent = null;
            availableVersions = null;
            versionRequest = "";
            downloadCategory = categoryValues[Math.Max(0, category.SelectedIndex)];
            await facade.SearchContentAsync(downloadCategory, query.Text?.Trim() ?? "");
        };
        Add(search);

        if (selectedContent is { } selected)
        {
            AddHeading(selected.Name);
            Add(selected.Description);
            Add("项目 ID：" + selected.Id);
            var back = new Button { Content = "返回搜索结果", HorizontalAlignment = HorizontalAlignment.Left };
            back.Click += (_, _) =>
            {
                selectedContent = null;
                availableVersions = null;
                versionRequest = "";
                Render();
            };
            Add(back);
            var type = downloadCategory;
            if (availableVersions is { } versions && versions.RequestId == selected.Id)
            {
                Add($"{versions.Game} · {versions.Loader} · {versions.Items.Count} 个兼容版本");
                var list = new ListBox
                {
                    ItemsSource = versions.Items.Select(item =>
                        item.Name + "  ·  " + string.Join(", ", item.Files)).ToArray(),
                    Height = 180
                };
                if (versions.Items.Count > 0)
                    list.SelectedIndex = 0;
                else
                    Add("当前实例没有兼容版本。请先在启动页切换实例，再重新选择版本。");
                Add(list);
                var localOnly = type is "modpack" or "datapack";
                var mode = new ComboBox
                {
                    ItemsSource = localOnly ? new[] { "下载到目录" } : new[] { "安装到当前实例", "下载到目录" },
                    SelectedIndex = 0,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                Add(mode);
                var download = new Button
                {
                    Content = "开始下载",
                    HorizontalAlignment = HorizontalAlignment.Left,
                    IsEnabled = versions.Items.Count > 0
                };
                download.Classes.Add("primary");
                download.Click += async (_, _) =>
                {
                    if (list.SelectedIndex < 0)
                    {
                        ShowToast("请选择资源版本。");
                        return;
                    }
                    var versionId = versions.Items[list.SelectedIndex].Id;
                    var local = localOnly || mode.SelectedIndex == 1;
                    if (local)
                    {
                        pendingLocalVersion = versionId;
                        await facade.PickFolderAsync("download");
                    }
                    else
                    {
                        await facade.QueueContentVersionAsync(versions.Token, versionId, "instance");
                        ShowToast("下载请求已提交，请查看任务列表。");
                    }
                };
                Add(download);
            }
            else
            {
                var choose = new Button { Content = "选择版本并安装", HorizontalAlignment = HorizontalAlignment.Left };
                choose.Classes.Add("primary");
                choose.Click += async (_, _) =>
                {
                    versionRequest = selected.Id;
                    ShowToast("正在读取兼容版本…");
                    await facade.ListContentVersionsAsync(
                        selected.Id,
                        type,
                        typed?.SelectedInstance ?? "",
                        requestId: selected.Id);
                };
                Add(choose);
            }
            return;
        }

        if (contentItems.Count > 0)
        {
            foreach (var item in contentItems)
            {
                Add(item.Name + (item.Description.Length > 0 ? "  ·  " + item.Description : ""));
                var current = item;
                var detail = new Button { Content = "详情", HorizontalAlignment = HorizontalAlignment.Left };
                detail.Click += (_, _) =>
                {
                    selectedContent = current;
                    availableVersions = null;
                    versionRequest = "";
                    Render();
                };
                Add(detail);
                var choose = new Button { Content = "选择版本", HorizontalAlignment = HorizontalAlignment.Left };
                choose.Click += async (_, _) =>
                {
                    selectedContent = current;
                    availableVersions = null;
                    versionRequest = current.Id;
                    Render();
                    ShowToast("正在读取兼容版本…");
                    await facade.ListContentVersionsAsync(
                        current.Id,
                        downloadCategory,
                        typed?.SelectedInstance ?? "",
                        requestId: current.Id);
                };
                Add(choose);
            }
        }
        else
        {
            Add("输入关键词后搜索 Modrinth。");
        }
    }

    void RenderAccount()
    {
        var snapshot = typed;
        AddHeading("账号");
        Add("当前：" + (snapshot?.AccountLabel is { Length: > 0 } label ? label : "离线账号"));
        Add("玩家：" + (snapshot?.Player ?? ""));
        var microsoft = new Button { Content = "登录微软账号", HorizontalAlignment = HorizontalAlignment.Left };
        microsoft.Click += async (_, _) => { await facade.LoginMicrosoftAsync(); await facade.RequestStateAsync(); };
        Add(microsoft);
        AddHeading("MUA 账号");
        var account = new TextBox { Watermark = "MUA 账号" };
        var password = new TextBox { Watermark = "密码", PasswordChar = '●' };
        Add(account);
        Add(password);
        var muaLogin = new Button { Content = "登录 MUA", HorizontalAlignment = HorizontalAlignment.Left };
        muaLogin.Click += async (_, _) =>
        {
            await facade.LoginMuaAsync(account.Text?.Trim() ?? "", password.Text ?? "");
            await facade.RequestStateAsync();
        };
        Add(muaLogin);
        var offline = new Button { Content = "切换到离线账号", HorizontalAlignment = HorizontalAlignment.Left };
        offline.Click += async (_, _) => { await facade.SwitchAccountModeAsync("offline"); await facade.RequestStateAsync(); };
        Add(offline);
        var forget = new Button { Content = "忘记已保存账号", HorizontalAlignment = HorizontalAlignment.Left };
        forget.Click += async (_, _) =>
        {
            await facade.ForgetAccountAsync(snapshot?.AccountType is { Length: > 0 } mode ? mode : "offline");
            await facade.RequestStateAsync();
        };
        Add(forget);
    }

    void RenderTasks()
    {
        AddHeading("下载任务");
        if (tasks.Count == 0)
        {
            Add("当前没有排队或运行中的任务。");
            return;
        }
        foreach (var task in tasks)
        {
            Add($"{task.Name} · {task.State} · {task.Percent:0}%");
            if (task.Message.Length > 0)
                Add("  " + task.Message);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            foreach (var (label, action) in new[]
                     {
                         ("暂停", "pause"), ("继续", "resume"), ("取消", "cancel"), ("删除", "delete")
                     })
            {
                var button = new Button { Content = label };
                var taskId = task.Id;
                var taskAction = action;
                button.Click += async (_, _) =>
                {
                    await facade.TaskActionAsync(taskId, taskAction);
                    await facade.RequestTasksAsync();
                };
                row.Children.Add(button);
            }
            Add(row);
        }
        var refresh = new Button { Content = "刷新任务", HorizontalAlignment = HorizontalAlignment.Left };
        refresh.Click += async (_, _) => await facade.RequestTasksAsync();
        Add(refresh);
    }

    void ShowToast(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
        ToastText.Text = message;
        ToastLayer.IsVisible = true;
        toastTimer.Stop();
        toastTimer.Start();
    }

    IntPtr Handle => TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

    void HandleWindow(string action)
    {
        switch (action)
        {
            case "minimize":
                WindowState = WindowState.Minimized;
                break;
            case "maximize":
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                break;
            case "close":
                Close();
                break;
        }
    }

    void DragWindow()
    {
        if (Handle == IntPtr.Zero)
            return;
        ReleaseCapture();
        SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
    }

    void ResetWindow()
    {
        Width = 1200;
        Height = 780;
        WindowState = WindowState.Normal;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool ReleaseCapture();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

    void AddHeading(string text) => Add(new TextBlock
    {
        Text = text,
        FontSize = 18,
        FontWeight = FontWeight.Bold,
        Margin = new Thickness(0, 12, 0, 0)
    });

    void Add(string text) => Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });

    void Add(Control control) => pageContent?.Children.Add(control);
}
