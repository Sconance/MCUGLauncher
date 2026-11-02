using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.Avalonia.Views;

internal partial class BookmarksView : UserControl
{
    ILauncherService? service;
    LauncherState? state;
    FavoriteInfo? selected;
    ResourceVersionsInfo? versions;
    string requestId = "";
    string pendingVersion = "";
    string pendingToken = "";

    public BookmarksView()
    {
        InitializeComponent();
        StartInstallButton.Click += async (_, _) => await StartInstall();
        CancelInstallButton.Click += (_, _) => CloseInstall();
    }

    public BookmarksView(ILauncherService service) : this()
    {
        Attach(service);
    }

    public void Attach(ILauncherService value)
    {
        service = value;
        service.StateChanged += ApplyState;
        service.ResourceVersionsReceived += OnVersions;
        service.PathPicked += OnPathPicked;
        service.ToastReceived += info => Status(info.Message);
        service.ErrorReceived += message => Status("操作失败：" + message);
        ApplyState(service.State);
    }

    void ApplyState(LauncherState? snapshot)
    {
        state = snapshot;
        if (snapshot is null)
            return;
        FavoritesPanel.Children.Clear();
        if (snapshot.Favorites.Count == 0)
        {
            FavoritesPanel.Children.Add(Muted("没有收藏的资源。在下载页搜索并收藏后会出现在这里。"));
            return;
        }
        foreach (var favorite in snapshot.Favorites)
        {
            var copy = new StackPanel { Spacing = 2 };
            copy.Children.Add(new TextBlock { Text = favorite.Name, FontWeight = FontWeight.SemiBold });
            copy.Children.Add(new TextBlock
            {
                Text = KindLabel(favorite.Kind) + " · 项目 ID " + favorite.Id,
                Classes = { "muted" }
            });
            if (favorite.Description.Length > 0)
                copy.Children.Add(new TextBlock
                {
                    Text = favorite.Description,
                    TextWrapping = TextWrapping.Wrap,
                    Classes = { "muted" }
                });
            var install = new Button { Content = "选择版本安装", Classes = { "primary" } };
            var current = favorite;
            install.Click += (_, _) => OpenInstall(current);
            var remove = new Button { Content = "取消收藏", Classes = { "ghost" } };
            var id = favorite.Id;
            var name = favorite.Name;
            var description = favorite.Description;
            var kind = favorite.Kind;
            remove.Click += async (_, _) =>
            {
                if (selected?.Id == id)
                    CloseInstall();
                await service!.ToggleBookmarkAsync(id, name, description, "", kind);
                Status("已取消收藏：" + name);
            };
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            actions.Children.Add(install);
            actions.Children.Add(remove);
            FavoritesPanel.Children.Add(new Border
            {
                Classes = { "card" },
                Padding = new Thickness(14),
                Child = Row(copy, actions)
            });
        }
    }

    void OpenInstall(FavoriteInfo favorite)
    {
        if (service is null)
            return;
        selected = favorite;
        versions = null;
        requestId = favorite.Id;
        InstallCard.IsVisible = true;
        InstallTitle.Text = "安装 " + favorite.Name;
        InstallHint.Text = "正在读取兼容版本…";
        InstallVersions.ItemsSource = null;
        InstallMode.ItemsSource = null;
        StartInstallButton.IsEnabled = false;
        _ = service.ListContentVersionsAsync(
            favorite.Id,
            favorite.Kind,
            state?.SelectedInstance ?? "",
            requestId: favorite.Id);
    }

    void OnVersions(ResourceVersionsInfo info)
    {
        if (info.RequestId != requestId || selected is null)
            return;
        versions = info;
        InstallVersions.ItemsSource = info.Items
            .Select(item => item.Name + "  ·  " + string.Join(", ", item.Files))
            .ToArray();
        if (info.Items.Count > 0)
            InstallVersions.SelectedIndex = 0;
        var localOnly = selected.Kind is "modpack" or "datapack";
        InstallMode.ItemsSource = localOnly
            ? new[] { "下载到目录" }
            : new[] { "安装到当前实例", "下载到目录" };
        InstallMode.SelectedIndex = 0;
        InstallHint.Text = info.Items.Count == 0
            ? $"{info.Game} · {info.Loader} · 当前实例没有兼容版本，请先在启动页切换实例。"
            : $"{info.Game} · {info.Loader} · {info.Items.Count} 个兼容版本";
        StartInstallButton.IsEnabled = info.Items.Count > 0;
    }

    async Task StartInstall()
    {
        if (service is null || selected is null || versions is null || InstallVersions.SelectedIndex < 0)
        {
            Status("请选择要安装的版本。");
            return;
        }
        var versionId = versions.Items[InstallVersions.SelectedIndex].Id;
        var localOnly = selected.Kind is "modpack" or "datapack";
        var local = localOnly || InstallMode.SelectedIndex == 1;
        if (local)
        {
            pendingVersion = versionId;
            pendingToken = versions.Token;
            await service.PickFolderAsync("bookmarkDownload");
            return;
        }
        await service.QueueContentVersionAsync(versions.Token, versionId, "instance");
        Status("下载请求已提交，请查看任务列表。");
    }

    void OnPathPicked(PickedPathInfo info)
    {
        if (info.Target != "bookmarkDownload" || pendingVersion.Length == 0)
            return;
        var versionId = pendingVersion;
        var token = pendingToken;
        pendingVersion = "";
        pendingToken = "";
        if (token.Length == 0)
            return;
        _ = service!.QueueContentVersionAsync(token, versionId, "local", info.Path);
        Status("下载请求已提交，请查看任务列表。");
    }

    void CloseInstall()
    {
        selected = null;
        versions = null;
        requestId = "";
        pendingVersion = "";
        pendingToken = "";
        InstallCard.IsVisible = false;
    }

    void Status(string message) => StatusText.Text = message;

    static string KindLabel(string kind) => kind switch
    {
        "mod" => "模组",
        "modpack" => "整合包",
        "resourcepack" => "资源包",
        "shader" => "光影",
        "datapack" => "数据包",
        _ => kind
    };

    static Control Row(Control copy, Control action)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        row.Children.Add(copy);
        Grid.SetColumn(action, 1);
        action.Margin = new Thickness(12, 0, 0, 0);
        action.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(action);
        return row;
    }

    static TextBlock Muted(string text) => new() { Text = text, Classes = { "muted" } };
}
