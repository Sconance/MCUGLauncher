using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MCUGLauncher.Avalonia.Views;

internal partial class InstancesView : UserControl
{
    LauncherFacade? facade;
    LauncherState? state;
    LaunchOptionsInfo? options;
    UninstallPreviewInfo? uninstall;
    ContentUpdatesInfo? updates;
    ContentUpdatePreviewInfo? updatePreview;
    (string Version, bool Saves, bool Servers)? pendingExport;
    string selected = "";
    string optionsRequest = "";
    bool updatingSelector;
    bool updatingOptions;

    public InstancesView()
    {
        InitializeComponent();
        WireButtons();
    }

    public InstancesView(LauncherFacade facade) : this()
    {
        Attach(facade);
    }

    public void Attach(LauncherFacade value)
    {
        facade = value;
        facade.StateChanged += ApplyState;
        facade.LaunchOptionsReceived += OnLaunchOptions;
        facade.LaunchOptionsSaved += OnLaunchOptionsSaved;
        facade.UninstallPreviewReceived += OnUninstallPreview;
        facade.ContentUpdatesReceived += OnUpdates;
        facade.ContentUpdatePreviewReceived += OnUpdatePreview;
        facade.PathPicked += OnPathPicked;
        facade.ToastReceived += info => Status(info.Message);
        facade.ErrorReceived += message => Status("操作失败：" + message);
        ApplyState(facade.State);
    }

    void WireButtons()
    {
        InstanceSelector.SelectionChanged += (_, _) => SelectInstance();
        LaunchButton.Click += async (_, _) =>
        {
            if (selected.Length == 0)
                return;
            Status("正在准备启动…");
            await facade!.LaunchAsync(selected);
        };
        OpenVersionFolderButton.Click += async (_, _) => await facade!.OpenFolderAsync("version");
        RefreshButton.Click += async (_, _) => await facade!.RequestStateAsync();
        BrowseJavaButton.Click += async (_, _) => await facade!.PickFileAsync("java");
        LoadOptionsButton.Click += (_, _) => LoadOptions(force: true);
        SaveOptionsButton.Click += async (_, _) => await SaveOptions();
        InheritCheck.Click += (_, _) => ApplyInheritState();
        CloneButton.Click += async (_, _) =>
        {
            var name = CloneNameBox.Text?.Trim() ?? "";
            if (name.Length == 0)
            {
                Status("请填写新实例名称。");
                return;
            }
            await facade!.CloneInstanceAsync(
                selected,
                name,
                CloneSavesCheck.IsChecked == true,
                CloneServersCheck.IsChecked == true);
            Status("克隆请求已提交，请查看任务列表。");
        };
        ExportButton.Click += async (_, _) =>
        {
            if (selected.Length == 0)
                return;
            pendingExport = (selected, ExportSavesCheck.IsChecked == true, ExportServersCheck.IsChecked == true);
            await facade!.PickSaveFileAsync("export");
        };
        PreviewUninstallButton.Click += async (_, _) => await facade!.PreviewUninstallAsync(selected);
        CheckUpdatesButton.Click += async (_, _) =>
        {
            if (selected.Length == 0)
                return;
            updates = null;
            updatePreview = null;
            UpdatesPanel.Children.Clear();
            UpdatePreviewPanel.Children.Clear();
            Status("正在检查内容更新…");
            await facade!.CheckUpdatesAsync(selected);
        };
    }

    void ApplyState(LauncherState? snapshot)
    {
        state = snapshot;
        if (snapshot is null)
            return;
        var instances = ValidInstances(snapshot);
        var previous = selected;
        if (selected.Length == 0 || !instances.Any(item => item.Name == selected))
            selected = instances.FirstOrDefault(item => item.Name == snapshot.SelectedInstance)?.Name
                ?? instances.FirstOrDefault()?.Name
                ?? "";
        updatingSelector = true;
        InstanceSelector.ItemsSource = instances.Count == 0
            ? new[] { "尚未安装实例" }
            : instances.Select(item =>
                item.DisplayName
                + (item.Minecraft.Length > 0 ? " · " + item.Minecraft : "")
                + " · " + item.Loader).ToArray();
        InstanceSelector.SelectedIndex = instances.Count == 0
            ? 0
            : Math.Max(0, Array.FindIndex(instances.ToArray(), item => item.Name == selected));
        updatingSelector = false;
        var current = instances.FirstOrDefault(item => item.Name == selected);
        Summary.Text = instances.Count == 0
            ? "尚未找到已安装实例。请先在下载页安装或导入整合包。"
            : $"共 {instances.Count} 个实例 · 当前 {current?.DisplayName ?? selected} · {current?.Minecraft ?? ""} · {current?.Loader ?? ""}";
        LaunchButton.IsEnabled = current is not null;
        OpenVersionFolderButton.IsEnabled = current is not null;
        if (previous != selected)
        {
            options = null;
            uninstall = null;
            updates = null;
            updatePreview = null;
            RenderUninstall();
            RenderUpdates();
            RenderUpdatePreview();
        }
        RenderMods(current);
        if (current is not null && (options is null || options.Version != selected))
            LoadOptions();
    }

    static IReadOnlyList<InstanceInfo> ValidInstances(LauncherState snapshot) =>
        snapshot.Instances
            .Where(item => item.Name.Length > 0 && item.Loader != "请安装版本")
            .ToArray();

    void SelectInstance()
    {
        if (updatingSelector || facade is null || state is null)
            return;
        var instances = ValidInstances(state).ToArray();
        if (InstanceSelector.SelectedIndex < 0 || InstanceSelector.SelectedIndex >= instances.Length)
            return;
        var next = instances[InstanceSelector.SelectedIndex].Name;
        if (next == selected)
            return;
        selected = next;
        options = null;
        uninstall = null;
        updates = null;
        updatePreview = null;
        RenderUninstall();
        RenderUpdates();
        RenderUpdatePreview();
        Status("正在切换实例…");
        _ = facade.SelectInstanceAsync(next);
        LoadOptions();
    }

    void LoadOptions(bool force = false)
    {
        if (facade is null || selected.Length == 0)
            return;
        if (!force && optionsRequest == selected)
            return;
        optionsRequest = selected;
        OptionsStatus.Text = "正在读取启动设置…";
        _ = facade.ReadLaunchOptionsAsync(selected, selected);
    }

    void OnLaunchOptions(LaunchOptionsInfo info)
    {
        if (info.RequestId != selected && info.Version != selected)
            return;
        optionsRequest = "";
        options = info;
        ApplyOptions(info);
    }

    void OnLaunchOptionsSaved()
    {
        OptionsStatus.Text = "启动设置已保存。";
        _ = facade!.RequestStateAsync();
    }

    void ApplyOptions(LaunchOptionsInfo info)
    {
        updatingOptions = true;
        DisplayNameBox.Text = info.Name.Length > 0 ? info.Name : selected;
        InheritCheck.IsChecked = info.Inherited;
        var settings = info.Settings;
        JavaBox.Text = settings.Java;
        AutoMemoryCheck.IsChecked = settings.AutoMemory;
        RamBox.Text = settings.RamMb.ToString();
        WidthBox.Text = settings.Width.ToString();
        HeightBox.Text = settings.Height.ToString();
        FullScreenCheck.IsChecked = settings.FullScreen;
        WindowTitleBox.Text = settings.WindowTitle;
        ServerBox.Text = settings.Server;
        JvmArgumentsBox.Text = settings.JvmArguments;
        InheritHint.Text = info.Inherited
            ? "当前继承全局启动设置；取消勾选后可保存独立设置。"
            : "当前使用独立设置；勾选“继承”并保存会恢复全局设置。";
        updatingOptions = false;
        ApplyInheritState();
        OptionsStatus.Text = "";
    }

    void ApplyInheritState()
    {
        if (updatingOptions)
            return;
        var inherit = InheritCheck.IsChecked == true;
        var enabled = !inherit;
        JavaBox.IsEnabled = enabled;
        BrowseJavaButton.IsEnabled = enabled;
        AutoMemoryCheck.IsEnabled = enabled;
        RamBox.IsEnabled = enabled;
        WidthBox.IsEnabled = enabled;
        HeightBox.IsEnabled = enabled;
        FullScreenCheck.IsEnabled = enabled;
        WindowTitleBox.IsEnabled = enabled;
        ServerBox.IsEnabled = enabled;
        JvmArgumentsBox.IsEnabled = enabled;
        InheritHint.Text = inherit
            ? "当前继承全局启动设置；取消勾选后可保存独立设置。"
            : "当前使用独立设置；勾选“继承”并保存会恢复全局设置。";
    }

    async Task SaveOptions()
    {
        if (facade is null || options is null)
        {
            OptionsStatus.Text = "请先重新读取此实例的启动设置。";
            return;
        }
        var name = DisplayNameBox.Text?.Trim() ?? "";
        if (name.Length is 0 or > 120)
        {
            OptionsStatus.Text = "实例显示名需为 1–120 个字符。";
            return;
        }
        if (!int.TryParse(RamBox.Text, out var ram) || ram is < 512 or > 131072)
        {
            OptionsStatus.Text = "内存需在 512–131072 MB 之间。";
            return;
        }
        if (!int.TryParse(WidthBox.Text, out var width) || width is < 320 or > 16384)
        {
            OptionsStatus.Text = "窗口宽需在 320–16384 之间。";
            return;
        }
        if (!int.TryParse(HeightBox.Text, out var height) || height is < 240 or > 16384)
        {
            OptionsStatus.Text = "窗口高需在 240–16384 之间。";
            return;
        }
        var inherit = InheritCheck.IsChecked == true;
        var settings = new LaunchOptionsSettings(
            AutoMemoryCheck.IsChecked == true,
            FullScreenCheck.IsChecked == true,
            ram,
            width,
            height,
            JavaBox.Text?.Trim() ?? "",
            JvmArgumentsBox.Text ?? "",
            WindowTitleBox.Text?.Trim() ?? "",
            ServerBox.Text?.Trim() ?? "");
        OptionsStatus.Text = "正在保存…";
        await facade.SaveLaunchOptionsAsync(options.Token, selected, name, settings, inherit);
    }

    void OnPathPicked(PickedPathInfo info)
    {
        if (info.Target == "java")
        {
            JavaBox.Text = info.Path;
            OptionsStatus.Text = "已选择 Java 路径，保存后生效。";
            return;
        }
        if (info.Target == "export" && pendingExport is { } pending)
        {
            pendingExport = null;
            _ = facade!.ExportInstanceAsync(pending.Version, info.Path, pending.Servers, pending.Saves);
            Status("导出请求已提交，请查看任务列表。");
        }
    }

    void RenderMods(InstanceInfo? instance)
    {
        ModsPanel.Children.Clear();
        if (instance is null)
        {
            ContentSummary.Text = "请选择实例。";
            return;
        }
        if (state?.SelectedInstance != selected)
        {
            ContentSummary.Text = "正在切换实例…";
            return;
        }
        ContentSummary.Text = $"{instance.DisplayName} · {instance.Loader} · {instance.Minecraft} · {instance.Mods} 个模组，{instance.Updates} 个可更新";
        if (state?.Mods.Count is not > 0)
        {
            ModsPanel.Children.Add(Muted("当前实例没有可管理的模组。"));
            return;
        }
        foreach (var mod in state.Mods)
        {
            var check = new CheckBox { Content = mod.Name, IsChecked = mod.Enabled, VerticalAlignment = VerticalAlignment.Center };
            var file = mod.File;
            var enabled = mod.Enabled;
            check.Click += async (_, _) =>
            {
                await facade!.ToggleModAsync(file, !enabled);
                await facade.RequestStateAsync();
            };
            var copy = new StackPanel { Spacing = 2 };
            copy.Children.Add(check);
            copy.Children.Add(new TextBlock
            {
                Text = mod.File + (mod.Version.Length > 0 ? " · " + mod.Version : "") + " · " + FormatSize(mod.Size),
                Classes = { "muted" },
                TextWrapping = TextWrapping.Wrap
            });
            var delete = new Button { Content = "删除", Classes = { "ghost" }, VerticalAlignment = VerticalAlignment.Center };
            var name = mod.Name;
            delete.Click += async (_, _) =>
            {
                await facade!.DeleteModAsync(file);
                Status("已删除 " + name);
                await facade.RequestStateAsync();
            };
            ModsPanel.Children.Add(Row(copy, delete));
        }
    }

    void OnUninstallPreview(UninstallPreviewInfo info)
    {
        if (info.Version != selected)
            return;
        uninstall = info;
        RenderUninstall();
    }

    void RenderUninstall()
    {
        UninstallPanel.Children.Clear();
        if (uninstall is not { Paths.Count: > 0 } preview)
        {
            UninstallPanel.Children.Add(Muted("尚未生成卸载预览。"));
            return;
        }
        UninstallPanel.Children.Add(new TextBlock
        {
            Text = "卸载 " + preview.Name,
            FontWeight = FontWeight.SemiBold
        });
        UninstallPanel.Children.Add(Muted(preview.Notice));
        foreach (var path in preview.Paths)
            UninstallPanel.Children.Add(new TextBlock { Text = "· " + path, TextWrapping = TextWrapping.Wrap, Classes = { "muted" } });
        var confirm = new CheckBox { Content = "我已检查删除范围并确认备份" };
        var apply = new Button { Content = "确认卸载", IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Left };
        confirm.Click += (_, _) => apply.IsEnabled = confirm.IsChecked == true;
        apply.Click += async (_, _) =>
        {
            var token = preview.Token;
            uninstall = null;
            RenderUninstall();
            await facade!.ApplyUninstallAsync(token);
            Status("卸载请求已提交，请在系统确认框核对。");
        };
        UninstallPanel.Children.Add(confirm);
        UninstallPanel.Children.Add(apply);
    }

    void OnUpdates(ContentUpdatesInfo info)
    {
        if (info.Version != selected)
            return;
        updates = info;
        updatePreview = null;
        UpdatePreviewPanel.Children.Clear();
        RenderUpdates();
    }

    void RenderUpdates()
    {
        UpdatesPanel.Children.Clear();
        if (updates is null)
        {
            UpdatesPanel.Children.Add(Muted("尚未检查内容更新。"));
            return;
        }
        if (updates.Items.Count == 0)
        {
            UpdatesPanel.Children.Add(Muted("当前实例没有启动器管理的内容。"));
            return;
        }
        var checkedUpdates = updates;
        var selectedProjects = new List<(string Id, CheckBox Box)>();
        foreach (var item in checkedUpdates.Items)
        {
            var box = new CheckBox
            {
                Content = item.Name + " · " + item.Before + " → " + (item.After.Length > 0 ? item.After : item.Error),
                IsEnabled = item.Available,
                IsChecked = item.Available
            };
            selectedProjects.Add((item.Id, box));
            UpdatesPanel.Children.Add(box);
        }
        var preview = new Button { Content = "生成所选更新预览", HorizontalAlignment = HorizontalAlignment.Left };
        preview.Click += async (_, _) =>
        {
            var projects = selectedProjects
                .Where(pair => pair.Box.IsChecked == true)
                .Select(pair => pair.Id)
                .ToArray();
            if (projects.Length == 0)
            {
                Status("请先选择更新项目。");
                return;
            }
            updatePreview = null;
            UpdatePreviewPanel.Children.Clear();
            Status("正在解析更新及必需依赖…");
            await facade!.PreviewUpdatesAsync(checkedUpdates.Token, projects);
        };
        UpdatesPanel.Children.Add(preview);
    }

    void OnUpdatePreview(ContentUpdatePreviewInfo info)
    {
        if (info.Version != selected)
            return;
        updatePreview = info;
        RenderUpdatePreview();
    }

    void RenderUpdatePreview()
    {
        UpdatePreviewPanel.Children.Clear();
        if (updatePreview is not { Changes.Count: > 0 } preview)
        {
            UpdatePreviewPanel.Children.Add(Muted("尚未生成更新预览。"));
            return;
        }
        UpdatePreviewPanel.Children.Add(new TextBlock
        {
            Text = "更新预览 · " + (preview.Bytes / 1048576.0).ToString("0.00") + " MiB",
            FontWeight = FontWeight.SemiBold
        });
        foreach (var change in preview.Changes)
            UpdatePreviewPanel.Children.Add(new TextBlock
            {
                Text = $"{change.Project}: {change.Before} → {change.After}\n{change.Path}",
                TextWrapping = TextWrapping.Wrap,
                Classes = { "muted" }
            });
        var apply = new Button { Content = "按预览加入更新队列", HorizontalAlignment = HorizontalAlignment.Left };
        apply.Click += async (_, _) =>
        {
            var token = preview.Token;
            updatePreview = null;
            RenderUpdatePreview();
            await facade!.ApplyUpdatesAsync(token);
            Status("更新请求已提交，请查看任务列表。");
        };
        UpdatePreviewPanel.Children.Add(apply);
    }

    void Status(string message) => MaintenanceStatus.Text = message;

    static Control Row(Control copy, Control action)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        row.Children.Add(copy);
        Grid.SetColumn(action, 1);
        action.Margin = new Thickness(12, 0, 0, 0);
        action.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(action);
        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#E3EAF0")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 6),
            Child = row
        };
    }

    static TextBlock Muted(string text) => new() { Text = text, Classes = { "muted" } };

    static string FormatSize(long bytes) => bytes <= 0
        ? "—"
        : bytes < 1024 * 1024
            ? $"{bytes / 1024.0:0.#} KB"
            : $"{bytes / 1024.0 / 1024.0:0.#} MB";
}
