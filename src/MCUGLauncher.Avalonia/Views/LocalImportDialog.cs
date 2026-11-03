using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.Avalonia.Views;

internal static class LocalImportDialog
{
    sealed record InstanceChoice(string Name, string Label)
    {
        public override string ToString() => Label;
    }

    sealed record KindChoice(string Value)
    {
        public override string ToString() => Value switch
        {
            "resourcepacks" => "资源包",
            "shaderpacks" => "光影包",
            _ => Value
        };
    }

    public static async Task Open(ILauncherService service, Window owner, string path)
    {
        var available = service.State?.Instances.Where(item => !item.IsPlaceholder && item.Name.Length > 0).ToArray()
                        ?? [];
        var targets = available.Select(item => new InstanceChoice(
            item.Name,
            item.DisplayName + " · " + item.Loader + " · " + item.Minecraft)).ToArray();
        var initial = Array.FindIndex(targets, item => item.Name == service.State?.SelectedInstance);
        var instancePicker = new ComboBox
        {
            ItemsSource = targets,
            SelectedIndex = targets.Length == 0 ? -1 : Math.Max(0, initial),
            MinWidth = 380
        };
        var kindPicker = new ComboBox { IsVisible = false, MinWidth = 240 };
        var kindLabel = new TextBlock { Text = "导入用途", IsVisible = false };
        var packName = new TextBox { Watermark = "实例名称" };
        var packRoot = new TextBox { Watermark = "已存在的游戏目录" };
        var chooseRoot = new Button { Content = "选择目录", Classes = { "ghost" } };
        var packFields = new StackPanel { Spacing = 8, IsVisible = false };
        packFields.Children.Add(new TextBlock { Text = "实例名称" });
        packFields.Children.Add(packName);
        packFields.Children.Add(new TextBlock { Text = "安装目录" });
        packFields.Children.Add(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                packRoot,
                chooseRoot
            }
        });
        Grid.SetColumn(chooseRoot, 1);
        var summary = new TextBlock
        {
            Text = "正在识别文件内容…",
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 70,
            Classes = { "muted" }
        };
        var confirm = new Button { Content = "确认并加入任务", Classes = { "primary" }, IsEnabled = false };
        var cancel = new Button { Content = "取消", Classes = { "ghost" } };
        var targetLabel = new TextBlock { Text = "目标实例" };
        var details = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = Path.GetFileName(path),
                    FontSize = 16,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = path,
                    Classes = { "muted" },
                    TextWrapping = TextWrapping.Wrap
                },
                targetLabel,
                instancePicker,
                kindLabel,
                kindPicker,
                packFields,
                summary
            }
        };
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancel, confirm }
        };
        var content = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), RowSpacing = 12 };
        content.Children.Add(new ScrollViewer { Content = details });
        content.Children.Add(actions);
        Grid.SetRow(actions, 1);
        var dialog = new Window
        {
            Title = "确认导入",
            Width = 620,
            Height = 470,
            MinWidth = 540,
            MinHeight = 400,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Border
            {
                Padding = new Thickness(18),
                Child = content
            }
        };
        LocalImportPreviewInfo? preview = null;
        PackPreviewInfo? pack = null;
        var revision = 0;
        var closed = false;
        var updatingKinds = false;

        async Task Inspect()
        {
            var current = ++revision;
            preview = null;
            pack = null;
            confirm.IsEnabled = false;
            kindPicker.IsVisible = false;
            kindLabel.IsVisible = false;
            packFields.IsVisible = false;
            instancePicker.IsVisible = true;
            targetLabel.IsVisible = true;
            summary.Text = "正在识别和检查…";
            try
            {
                var target = instancePicker.SelectedItem as InstanceChoice;
                var result = await service.PreviewLocalImportAsync(
                    path,
                    target?.Name ?? "",
                    (kindPicker.SelectedItem as KindChoice)?.Value ?? "");
                if (closed || current != revision)
                    return;
                if (result.Kind == "modpack")
                {
                    var packResult = await service.PreviewPackAsync(path);
                    if (closed || current != revision)
                        return;
                    if (!packResult.Ok || packResult.Preview is not { } packPreview)
                    {
                        summary.Text = "读取整合包失败：" + packResult.Error;
                        return;
                    }
                    pack = packPreview;
                    instancePicker.IsVisible = false;
                    targetLabel.IsVisible = false;
                    packFields.IsVisible = true;
                    packName.Text = packPreview.Name;
                    packRoot.Text = packPreview.Root;
                    summary.Text = $"游戏版本：{packPreview.Game}\n加载器：{packPreview.Loader}\n确认后才会加入安装队列。";
                    confirm.IsEnabled = true;
                    return;
                }
                if (result.Kind == "ambiguous")
                {
                    updatingKinds = true;
                    kindPicker.ItemsSource = result.Choices.Select(value => new KindChoice(value)).ToArray();
                    kindPicker.SelectedIndex = -1;
                    updatingKinds = false;
                    kindPicker.IsVisible = true;
                    kindLabel.IsVisible = true;
                    summary.Text = string.Join(Environment.NewLine, result.Warnings);
                    return;
                }

                preview = result;
                summary.Text = "识别类型：" + Kind(result.Kind)
                    + (result.Name.Length > 0 ? "\n目标实例：" + result.Name : "")
                    + (result.Target.Length > 0 ? "\n安装目录：" + result.Target : "")
                    + (result.Warnings.Count > 0 ? "\n\n" + string.Join(Environment.NewLine, result.Warnings) : "");
                confirm.IsEnabled = result.CanImport && result.Token.Length > 0;
            }
            catch (Exception error)
            {
                if (!closed && current == revision)
                    summary.Text = error.Message;
            }
        }

        void OnPathPicked(PickedPathInfo result)
        {
            if (result.Target == "localImportPackRoot")
                packRoot.Text = result.Path;
        }

        instancePicker.SelectionChanged += async (_, _) => await Inspect();
        kindPicker.SelectionChanged += async (_, _) =>
        {
            if (!updatingKinds && kindPicker.SelectedItem is not null)
                await Inspect();
        };
        chooseRoot.Click += async (_, _) => await service.PickFolderAsync("localImportPackRoot");
        cancel.Click += (_, _) => dialog.Close();
        confirm.Click += async (_, _) =>
        {
            confirm.IsEnabled = false;
            try
            {
                if (pack is { } packPreview)
                {
                    var name = packName.Text?.Trim() ?? "";
                    var root = packRoot.Text?.Trim() ?? "";
                    if (name.Length == 0 || !Directory.Exists(root))
                    {
                        summary.Text = "请输入实例名称并选择已存在的游戏目录。";
                        confirm.IsEnabled = true;
                        return;
                    }
                    await service.ConfirmPackAsync(packPreview.Token, name, root);
                }
                else if (preview is { CanImport: true } choice)
                {
                    await service.ConfirmLocalImportAsync(choice.Token);
                }
                dialog.Close();
            }
            catch (Exception error)
            {
                summary.Text = error.Message;
                confirm.IsEnabled = true;
            }
        };
        dialog.Opened += (_, _) => _ = Inspect();
        dialog.Closed += (_, _) =>
        {
            closed = true;
            revision++;
        };
        service.PathPicked += OnPathPicked;
        try
        {
            await dialog.ShowDialog(owner);
        }
        finally
        {
            closed = true;
            revision++;
            service.PathPicked -= OnPathPicked;
        }
    }

    public static async Task ShowError(Window owner, string message)
    {
        var dialog = new Window
        {
            Title = "无法导入文件",
            Width = 440,
            Height = 190,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(18),
                Spacing = 14,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    new Button
                    {
                        Content = "关闭",
                        Classes = { "primary" },
                        HorizontalAlignment = HorizontalAlignment.Right
                    }
                }
            }
        };
        var close = ((StackPanel)dialog.Content).Children.OfType<Button>().Single();
        close.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
    }

    static string Kind(string value) => value switch
    {
        "mods" => "模组",
        "resourcepacks" => "资源包",
        "shaderpacks" => "光影包",
        "world" => "世界存档",
        "datapacks" => "数据包",
        _ => value
    };
}
