using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.Avalonia.Views;

internal partial class InstancesView : UserControl
{
    ILauncherService service = EmptyServiceFallback.Instance;
    LauncherState? state;
    LaunchOptionsInfo? options;
    UninstallPreviewInfo? uninstall;
    ContentUpdatesInfo? updates;
    ContentUpdatePreviewInfo? updatePreview;
    (string Version, bool Saves, bool Servers)? pendingExport;
    string selected = "";
    string optionsRequest = "";
    string modsRequest = "";
    IReadOnlyList<ModInfo> mods = Array.Empty<ModInfo>();
    bool updatingOptions;

    public InstancesView()
    {
        InitializeComponent();
        WireButtons();
    }

    public InstancesView(ILauncherService service) : this()
    {
        Attach(service);
    }

    /// <summary>请求收起实例设置，回到实例选择二级页。</summary>
    public event Action? CloseRequested;

    /// <summary>从上一级进来时指定要设置的实例；留空则跟随全局当前实例。</summary>
    public void Open(string name)
    {
        selected = name.Trim();
        options = null;
        uninstall = null;
        updates = null;
        updatePreview = null;
        mods = Array.Empty<ModInfo>();
        modsRequest = "";
        if (state is not null)
            ApplyState(state);
        else
        {
            RenderUninstall();
            RenderUpdates();
            RenderUpdatePreview();
            _ = LoadOptions(force: true);
        }
    }

    public void Attach(ILauncherService value)
    {
        service = value ?? EmptyServiceFallback.Instance;
        service.StateChanged += ApplyState;
        service.PathPicked += OnPathPicked;
        service.ToastReceived += info => Status(info.Message);
        service.ErrorReceived += message => Status("操作失败：" + message);
        ApplyState(service.State);
    }

    void WireButtons()
    {
        CloseButton.Click += (_, _) => CloseRequested?.Invoke();
        OpenVersionFolderButton.Click += async (_, _) => await service!.OpenFolderAsync("version", selected);
        RefreshButton.Click += async (_, _) => await service!.RequestStateAsync();
        BrowseJavaButton.Click += async (_, _) => await service!.PickFileAsync("java");
        LoadOptionsButton.Click += async (_, _) => await LoadOptions(force: true);
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

            await service!.CloneInstanceAsync(
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
            await service!.PickSaveFileAsync("export");
        };
        PreviewUninstallButton.Click += async (_, _) =>
        {
            OnUninstallPreview(await service!.PreviewUninstallAsync(selected));
        };
        CheckUpdatesButton.Click += async (_, _) =>
        {
            if (selected.Length == 0)
                return;
            updates = null;
            updatePreview = null;
            UpdatesPanel.Children.Clear();
            UpdatePreviewPanel.Children.Clear();
            Status("正在检查内容更新…");
            OnUpdates(await service!.CheckUpdatesAsync(selected));
        };
    }

    void ApplyState(LauncherState? snapshot)
    {
        state = snapshot;
        if (snapshot is null)
            return;
        var instances = ValidInstances(snapshot);
        var previous = selected;
        // 保留上一级传进来的实例；它不在列表里时才退回全局选择 / 第一个
        if (selected.Length == 0 || !instances.Any(item => item.Name == selected))
            selected = instances.FirstOrDefault(item => item.Name == snapshot.SelectedInstance)?.Name
                       ?? instances.FirstOrDefault()?.Name
                       ?? "";
        var current = instances.FirstOrDefault(item => item.Name == selected);
        var currentRow = current is null ? null : InstanceListItem.From(current);
        var isCurrent = snapshot.SelectedInstance == selected;
        SettingsSubtitle.Text = currentRow is null
            ? "该实例已不存在，请返回上一级重新选择。"
            : currentRow.Title
              + (currentRow.Subtitle.Length > 0 ? " · " + currentRow.Subtitle : "")
              + (isCurrent ? "" : " · 未设为当前实例");
        // 文件夹与模组都按实例名读取，不要求切换当前实例
        OpenVersionFolderButton.IsEnabled = current is not null;
        ToolTip.SetTip(OpenVersionFolderButton, "打开该实例的版本文件夹");
        if (previous != selected)
        {
            options = null;
            uninstall = null;
            updates = null;
            updatePreview = null;
            mods = Array.Empty<ModInfo>();
            modsRequest = "";
            RenderUninstall();
            RenderUpdates();
            RenderUpdatePreview();
        }

        if (current is null)
        {
            RenderMods(null);
        }
        else
            _ = LoadMods();

        if (current is not null && (options is null || options.Version != selected))
            _ = LoadOptions();
    }

    static IReadOnlyList<InstanceInfo> ValidInstances(LauncherState snapshot) =>
        snapshot.Instances
            .Where(item => item.Name.Length > 0 && !item.IsPlaceholder)
            .ToArray();

    async Task LoadOptions(bool force = false)
    {
        if (service is null || selected.Length == 0)
            return;
        if (!force && optionsRequest == selected)
            return;
        optionsRequest = selected;
        OptionsStatus.Text = "正在读取启动设置…";
        var info = await service.ReadLaunchOptionsAsync(selected, selected);
        if (info.RequestId != selected && info.Version != selected)
            return;
        optionsRequest = "";
        options = info;
        ApplyOptions(info);
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
        AutoChineseCheck.IsChecked = settings.AutoChinese;
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
        AutoChineseCheck.IsEnabled = enabled;
        WindowTitleBox.IsEnabled = enabled;
        ServerBox.IsEnabled = enabled;
        JvmArgumentsBox.IsEnabled = enabled;
        InheritHint.Text = inherit
            ? "当前继承全局启动设置；取消勾选后可保存独立设置。"
            : "当前使用独立设置；勾选“继承”并保存会恢复全局设置。";
    }

    async Task SaveOptions()
    {
        if (service is null || options is null)
        {
            OptionsStatus.Text = "请先重新读取此实例的启动设置。";
            return;
        }

        var name = DisplayNameBox.Text?.Trim() ?? "";
        if (name.Length is 0 or > LaunchLimits.DisplayNameMax)
        {
            OptionsStatus.Text = $"实例显示名需为 1–{LaunchLimits.DisplayNameMax} 个字符。";
            return;
        }

        if (!int.TryParse(RamBox.Text, out var ram) || ram is < LaunchLimits.RamMin or > LaunchLimits.RamMax)
        {
            OptionsStatus.Text = $"内存需在 {LaunchLimits.RamMin}–{LaunchLimits.RamMax} MB 之间。";
            return;
        }

        if (!int.TryParse(WidthBox.Text, out var width) ||
            width is < LaunchLimits.WindowWidthMin or > LaunchLimits.WindowWidthMax)
        {
            OptionsStatus.Text = $"窗口宽需在 {LaunchLimits.WindowWidthMin}–{LaunchLimits.WindowWidthMax} 之间。";
            return;
        }

        if (!int.TryParse(HeightBox.Text, out var height) ||
            height is < LaunchLimits.WindowHeightMin or > LaunchLimits.WindowHeightMax)
        {
            OptionsStatus.Text = $"窗口高需在 {LaunchLimits.WindowHeightMin}–{LaunchLimits.WindowHeightMax} 之间。";
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
            ServerBox.Text?.Trim() ?? "",
            AutoChineseCheck.IsChecked == true);
        OptionsStatus.Text = "正在保存…";
        OptionsStatus.Text = (await service.SaveLaunchOptionsAsync(options.Token, selected, name, settings, inherit))
            .Message;
        await service.RequestStateAsync();
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
            _ = service!.ExportInstanceAsync(pending.Version, info.Path, pending.Servers, pending.Saves);
            Status("导出请求已提交，请查看任务列表。");
        }
    }

    /// <summary>按实例名拉取模组清单；和全局当前实例无关，所以不切换也能看。</summary>
    async Task LoadMods(bool force = false)
    {
        if (service is null || selected.Length == 0)
            return;
        if (!force && modsRequest == selected)
            return;
        var target = selected;
        modsRequest = target;
        ContentSummary.Text = "正在读取模组…";
        var list = await service.ListInstanceModsAsync(target);
        if (target != selected)
            return;
        mods = list;
        RenderMods(state?.Instances.FirstOrDefault(item => item.Name == selected));
    }

    void RenderMods(InstanceInfo? instance)
    {
        ModsPanel.Children.Clear();
        if (instance is null)
        {
            ContentSummary.Text = "请选择实例。";
            return;
        }

        ContentSummary.Text =
            $"{instance.DisplayName} · {instance.Loader} · {instance.Minecraft} · {mods.Count} 个模组，{instance.Updates} 个可更新";
        if (mods.Count == 0)
        {
            ModsPanel.Children.Add(Muted("这个实例还没有可管理的模组。"));
            return;
        }

        foreach (var mod in mods)
        {
            var check = new CheckBox
                { Content = mod.Name, IsChecked = mod.Enabled, VerticalAlignment = VerticalAlignment.Center };
            var file = mod.File;
            var enabled = mod.Enabled;
            check.Click += async (_, _) =>
            {
                ContentSummary.Text = (await service!.ToggleModAsync(file, !enabled)).Message;
                await service.RequestStateAsync();
                await LoadMods(force: true);
            };
            var copy = new StackPanel { Spacing = 2 };
            copy.Children.Add(check);
            copy.Children.Add(new TextBlock
            {
                Text = mod.File + (mod.Version.Length > 0 ? " · " + mod.Version : "") + " · " + FormatSize(mod.Size),
                Classes = { "muted" },
                TextWrapping = TextWrapping.Wrap
            });
            var delete = new Button
                { Content = "删除", Classes = { "ghost" }, VerticalAlignment = VerticalAlignment.Center };
            var name = mod.Name;
            delete.Click += async (_, _) =>
            {
                Status((await service!.DeleteModAsync(file)).Message + " " + name);
                await service.RequestStateAsync();
                await LoadMods(force: true);
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
            UninstallPanel.Children.Add(new TextBlock
                { Text = "· " + path, TextWrapping = TextWrapping.Wrap, Classes = { "muted" } });
        var confirm = new CheckBox { Content = "我已检查删除范围并确认备份" };
        var apply = new Button { Content = "确认卸载", IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Left };
        confirm.Click += (_, _) => apply.IsEnabled = confirm.IsChecked == true;
        apply.Click += async (_, _) =>
        {
            var token = preview.Token;
            uninstall = null;
            RenderUninstall();
            await service!.ApplyUninstallAsync(token);
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
            OnUpdatePreview(await service!.PreviewUpdatesAsync(checkedUpdates.Token, projects));
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
            Status((await service!.ApplyUpdatesAsync(token)).Message);
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
