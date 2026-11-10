using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.Avalonia.Views;

internal partial class DownloadView : UserControl
{
    static readonly string[] CategoryValues = ["mod", "modpack", "resourcepack", "shader", "datapack"];
    static readonly string[] CategoryLabels = ["模组", "整合包", "资源包", "光影", "数据包"];
    static readonly string[] LoaderValues = ["fabric", "quilt", "forge", "neoforge"];
    static readonly string[] LoaderLabels = ["Fabric", "Quilt", "Forge", "NeoForge"];
    static readonly string[] VanillaTypeValues = ["release", "snapshot", "old", "all"];
    static readonly string[] VanillaTypeLabels = ["正式版", "快照版", "远古版", "全部"];
    const int VanillaPageSize = 50;

    ILauncherService service = EmptyServiceFallback.Instance;
    LauncherState? state;
    IReadOnlyList<ContentItem> results = [];
    ContentItem? selected;
    ResourceVersionsInfo? versions;
    IReadOnlyList<VanillaVersionInfo> vanilla = [];
    IReadOnlyList<VanillaVersionInfo> visibleVanilla = [];
    PackPreviewInfo? pack;
    string versionRequest = "";
    string pendingVersion = "";
    string pendingToken = "";
    string downloadCategory = "mod";
    int vanillaPage;
    bool vanillaLoaded;
    bool loadingVanilla;

    public DownloadView()
    {
        InitializeComponent();
        CategoryBox.ItemsSource = CategoryLabels;
        CategoryBox.SelectedIndex = 0;
        VanillaCategoryBox.ItemsSource = VanillaTypeLabels;
        VanillaCategoryBox.SelectedIndex = 0;
        LoaderTypeBox.ItemsSource = LoaderLabels;
        LoaderTypeBox.SelectedIndex = 0;
        LoaderTypeBox.SelectionChanged += (_, _) => LoaderVersionBox.ItemsSource = null;
        LoaderGameBox.SelectionChanged += (_, _) => LoaderVersionBox.ItemsSource = null;
        VanillaQueryBox.TextChanged += (_, _) =>
        {
            vanillaPage = 0;
            RenderVanilla();
        };
        VanillaCategoryBox.SelectionChanged += (_, _) =>
        {
            vanillaPage = 0;
            RenderVanilla();
        };
        PreviousVanillaPageButton.Click += (_, _) =>
        {
            vanillaPage--;
            RenderVanilla();
        };
        NextVanillaPageButton.Click += (_, _) =>
        {
            vanillaPage++;
            RenderVanilla();
        };
        ResultsPanel.Children.Add(Muted("输入关键词后搜索 Modrinth。"));
        WireButtons();
    }

    public DownloadView(ILauncherService service) : this()
    {
        Attach(service);
    }

    public void Attach(ILauncherService value)
    {
        service = value ?? EmptyServiceFallback.Instance;
        service.StateChanged += ApplyState;
        service.ContentErrorReceived += OnContentError;
        service.PathPicked += OnPathPicked;
        service.ToastReceived += info => Status(info.Message);
        service.ErrorReceived += message => Status("操作失败：" + message);
        CustomDownload.Attach(service);
        ApplyState(service.State);
    }

    public void Activate()
    {
        if (!vanillaLoaded && !loadingVanilla)
            _ = LoadVanilla();
    }

    void WireButtons()
    {
        SearchButton.Click += async (_, _) =>
        {
            selected = null;
            versions = null;
            DetailCard.IsVisible = false;
            ResultsPanel.IsVisible = true;
            ResultsPanel.Children.Clear();
            ResultsPanel.Children.Add(Muted("正在搜索…"));
            downloadCategory = CategoryValues[Math.Max(0, CategoryBox.SelectedIndex)];
            Status("正在搜索 Modrinth…");
            var search = await service!.SearchContentAsync(downloadCategory, QueryBox.Text?.Trim() ?? "");
            if (search.Ok)
                OnContent(search.Items);
            else
                OnContentError(new ContentErrorInfo(search.Category, search.RequestId, search.Error));
        };
        ChooseVersionButton.Click += async (_, _) => await LoadVersions();
        DownloadButton.Click += async (_, _) => await QueueDownload();
        BackButton.Click += (_, _) =>
        {
            selected = null;
            versions = null;
            versionRequest = "";
            DetailCard.IsVisible = false;
            ResultsPanel.IsVisible = true;
            RenderResults();
        };
        LoadVanillaButton.Click += async (_, _) => await LoadVanilla();
        InstallVanillaButton.Click += async (_, _) =>
        {
            if (VanillaList.SelectedIndex < 0 || VanillaList.SelectedIndex >= visibleVanilla.Count)
            {
                VanillaHint.Text = "请先读取并选择一个原版版本。";
                return;
            }
            var version = visibleVanilla[VanillaList.SelectedIndex].Id;
            VanillaHint.Text = "正在提交原版安装…";
            await service!.InstallVanillaAsync(version);
            VanillaHint.Text = "原版安装请求已提交：" + version;
        };
        LoadGameVersionsButton.Click += async (_, _) => await LoadVanilla();
        LoadLoaderVersionsButton.Click += async (_, _) =>
        {
            var game = LoaderGameBox.SelectedItem as string ?? "";
            if (game.Length == 0)
            {
                LoaderHint.Text = "请先选择游戏版本。";
                return;
            }
            var loader = LoaderValues[Math.Max(0, LoaderTypeBox.SelectedIndex)];
            LoaderVersionBox.ItemsSource = null;
            LoaderHint.Text = "正在读取 " + LoaderLabels[LoaderTypeBox.SelectedIndex] + " 版本…";
            var result = await service!.ListLoadersAsync(game, loader);
            if (result.Ok && result.Result is { } loaders)
                OnLoaders(loaders);
            else
                LoaderHint.Text = "读取加载器版本失败：" + result.Error;
        };
        InstallLoaderButton.Click += async (_, _) =>
        {
            var game = LoaderGameBox.SelectedItem as string ?? "";
            var loaderVersion = LoaderVersionBox.SelectedItem as string ?? "";
            if (game.Length == 0 || loaderVersion.Length == 0)
            {
                LoaderHint.Text = "请先选择游戏版本和加载器版本。";
                return;
            }
            var loader = LoaderValues[Math.Max(0, LoaderTypeBox.SelectedIndex)];
            await service!.InstallLoaderAsync(game, loader, loaderVersion);
            LoaderHint.Text = $"安装请求已提交：{game} / {LoaderLabels[LoaderTypeBox.SelectedIndex]} {loaderVersion}";
        };
        PickPackButton.Click += async (_, _) => await service!.PickFileAsync("pack");
        LocalImportButton.Click += async (_, _) => await service!.PickFileAsync("localImport");
        PickPackRootButton.Click += async (_, _) => await service!.PickFolderAsync("packRoot");
        ConfirmPackButton.Click += async (_, _) =>
        {
            if (pack is null)
                return;
            var name = PackNameBox.Text?.Trim() ?? "";
            var root = PackRootBox.Text?.Trim() ?? "";
            if (name.Length == 0 || root.Length == 0)
            {
                PackHint.Text = "请填写实例名称并选择已存在的安装目录。";
                return;
            }
            PackHint.Text = "正在提交导入…";
            await service!.ConfirmPackAsync(pack.Token, name, root);
            PackHint.Text = "整合包导入请求已提交，请查看任务列表。";
        };
    }

    void ApplyState(LauncherState? snapshot)
    {
        state = snapshot;
        if (snapshot is null)
            return;
        var current = snapshot.Instances.FirstOrDefault(item => item.Name == snapshot.SelectedInstance);
        InstanceHint.Text = current is null
            ? "尚未选择实例"
            : $"当前实例：{current.DisplayName} · {current.Loader} · {current.Minecraft}";
        if (vanillaLoaded)
            FillLoaderGames();
        if (selected is null && ResultsPanel.IsVisible && results.Count > 0)
            RenderResults();
    }

    async Task LoadVanilla()
    {
        if (service is null || loadingVanilla)
            return;
        loadingVanilla = true;
        vanillaLoaded = false;
        VanillaHint.Text = "正在读取 Mojang 版本列表…";
        var result = await service.ListVanillaVersionsAsync();
        if (result.Ok)
            OnVanilla(result.Items.ToArray());
        else
        {
            loadingVanilla = false;
            VanillaHint.Text = result.Error;
        }
    }

    void OnVanilla(VanillaVersionInfo[] items)
    {
        loadingVanilla = false;
        vanillaLoaded = true;
        vanilla = items;
        vanillaPage = 0;
        RenderVanilla();
        VanillaHint.Text = items.Length == 0 ? "没有读取到版本。" : $"已读取 {items.Length} 个正式版、快照和远古版本。";
        FillLoaderGames();
    }

    void RenderVanilla()
    {
        var type = VanillaTypeValues[Math.Clamp(VanillaCategoryBox.SelectedIndex, 0, VanillaTypeValues.Length - 1)];
        var query = VanillaQueryBox.Text?.Trim() ?? "";
        var filtered = vanilla.Where(item =>
                (type == "all" || (type == "old"
                    ? item.Type is "old_alpha" or "old_beta"
                    : item.Type == type))
                && item.Id.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var pageCount = Math.Max(1, (filtered.Length + VanillaPageSize - 1) / VanillaPageSize);
        vanillaPage = Math.Clamp(vanillaPage, 0, pageCount - 1);
        visibleVanilla = filtered.Skip(vanillaPage * VanillaPageSize).Take(VanillaPageSize).ToArray();
        VanillaList.ItemsSource = visibleVanilla.Select(item => item.Id + " · " + item.Released).ToArray();
        VanillaList.SelectedIndex = visibleVanilla.Count > 0 ? 0 : -1;
        VanillaPageText.Text = $"{vanillaPage + 1} / {pageCount} 页 · {filtered.Length} 个版本";
        PreviousVanillaPageButton.IsEnabled = vanillaPage > 0;
        NextVanillaPageButton.IsEnabled = vanillaPage + 1 < pageCount;
        InstallVanillaButton.IsEnabled = visibleVanilla.Count > 0;
    }

    void FillLoaderGames()
    {
        if (service?.State is not { } snapshot)
            return;
        var current = LoaderGameBox.SelectedItem as string;
        var instanceGame = snapshot.Instances
            .FirstOrDefault(item => item.Name == snapshot.SelectedInstance)?.Minecraft ?? "";
        var games = vanilla.Select(item => item.Id)
            .Prepend(instanceGame)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        LoaderGameBox.ItemsSource = games;
        var index = current is null ? -1 : Array.FindIndex(games, value => value == current);
        if (index < 0)
            index = instanceGame.Length > 0 ? Array.FindIndex(games, value => value == instanceGame) : -1;
        LoaderGameBox.SelectedIndex = games.Length == 0 ? -1 : Math.Max(0, index);
    }

    void OnContent(IReadOnlyList<ContentItem> items)
    {
        results = items;
        if (selected is null && DetailCard.IsVisible is false)
        {
            RenderResults();
            Status($"搜索完成，共 {items.Count} 项。");
        }
    }

    void RenderResults()
    {
        ResultsPanel.Children.Clear();
        if (results.Count == 0)
        {
            ResultsPanel.Children.Add(Muted("没有搜索结果。输入关键词后搜索 Modrinth。"));
            return;
        }
        foreach (var item in results)
        {
            var copy = new StackPanel { Spacing = 2 };
            copy.Children.Add(new TextBlock { Text = item.Name, FontWeight = FontWeight.SemiBold });
            copy.Children.Add(new TextBlock
            {
                Text = item.Description,
                TextWrapping = TextWrapping.Wrap,
                Classes = { "muted" }
            });
            copy.Children.Add(new TextBlock
            {
                Text = string.Join(" · ", new[]
                {
                    item.Author.Length > 0 ? "作者 " + item.Author : "",
                    item.Downloads.Length > 0 ? item.Downloads + " 次下载" : ""
                }.Where(value => value.Length > 0)),
                Classes = { "muted" }
            });
            var detail = new Button { Content = "详情", Classes = { "ghost" } };
            var current = item;
            detail.Click += (_, _) => ShowDetail(current);
            var choose = new Button { Content = "选择版本", Classes = { "ghost" } };
            choose.Click += async (_, _) =>
            {
                ShowDetail(current);
                await LoadVersions();
            };
            var bookmarked = service?.State?.Favorites.Any(favorite => favorite.Id == item.Id) == true;
            var bookmark = new Button
            {
                Content = bookmarked ? "取消收藏" : "收藏",
                Classes = { "ghost" }
            };
            var itemId = item.Id;
            var itemName = item.Name;
            var itemDescription = item.Description;
            var itemAuthor = item.Author;
            var itemKind = downloadCategory;
            bookmark.Click += async (_, _) =>
            {
                Status((await service!.ToggleBookmarkAsync(itemId, itemName, itemDescription, itemAuthor, itemKind)).Message + " " + itemName);
            };
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 10, 0, 0) };
            actions.Children.Add(detail);
            actions.Children.Add(choose);
            actions.Children.Add(bookmark);
            copy.Children.Add(actions);
            ResultsPanel.Children.Add(new Border
            {
                Width = 350,
                Margin = new Thickness(0, 0, 14, 14),
                Classes = { "card" },
                Child = copy
            });
        }
    }

    void ShowDetail(ContentItem item)
    {
        selected = item;
        versions = null;
        versionRequest = "";
        ResultsPanel.IsVisible = false;
        DetailCard.IsVisible = true;
        DetailTitle.Text = item.Name;
        DetailDescription.Text = item.Description;
        DetailMeta.Text = string.Join(" · ", new[]
        {
            "项目 ID " + item.Id,
            item.Author.Length > 0 ? "作者 " + item.Author : "",
            item.Downloads.Length > 0 ? item.Downloads + " 次下载" : "",
            item.Tags.Count > 0 ? string.Join(" / ", item.Tags) : ""
        }.Where(value => value.Length > 0));
        DetailVersionsHint.Text = "尚未读取兼容版本。";
        ChooseVersionButton.IsVisible = true;
        VersionList.IsVisible = false;
        TargetModeBox.IsVisible = false;
        DownloadButton.IsVisible = false;
    }

    async Task LoadVersions()
    {
        if (service is null || selected is null)
            return;
        versionRequest = selected.Id;
        DetailVersionsHint.Text = "正在读取兼容版本…";
        ChooseVersionButton.IsVisible = false;
        var result = await service.ListContentVersionsAsync(
            selected.Id,
            downloadCategory,
            state?.SelectedInstance ?? "",
            requestId: selected.Id);
        if (result.Ok && result.Result is { } info)
            OnResourceVersions(info);
        else
            OnContentError(new ContentErrorInfo(downloadCategory, selected.Id, result.Error));
    }

    void OnResourceVersions(ResourceVersionsInfo info)
    {
        if (info.RequestId != versionRequest || selected is null)
            return;
        versions = info;
        DetailVersionsHint.Text = $"{info.Game} · {info.Loader} · {info.Items.Count} 个兼容版本";
        VersionList.ItemsSource = info.Items
            .Select(item => item.Name + "  ·  " + string.Join(", ", item.Files))
            .ToArray();
        VersionList.IsVisible = info.Items.Count > 0;
        if (info.Items.Count > 0)
            VersionList.SelectedIndex = 0;
        var localOnly = downloadCategory is "modpack" or "datapack";
        TargetModeBox.ItemsSource = localOnly
            ? new[] { "下载到目录" }
            : new[] { "安装到当前实例", "下载到目录" };
        TargetModeBox.SelectedIndex = 0;
        TargetModeBox.IsVisible = info.Items.Count > 0;
        DownloadButton.IsVisible = true;
        DownloadButton.IsEnabled = info.Items.Count > 0;
        if (info.Items.Count == 0)
            DetailVersionsHint.Text += "；当前实例没有兼容版本，请切换实例后重试。";
    }

    async Task QueueDownload()
    {
        if (service is null || versions is null || VersionList.SelectedIndex < 0)
        {
            Status("请选择资源版本。");
            return;
        }
        var versionId = versions.Items[VersionList.SelectedIndex].Id;
        var localOnly = downloadCategory is "modpack" or "datapack";
        var local = localOnly || TargetModeBox.SelectedIndex == 1;
        if (local)
        {
            pendingVersion = versionId;
            pendingToken = versions.Token;
            await service.PickFolderAsync("download");
            return;
        }
        await service.QueueContentVersionAsync(versions.Token, versionId, "instance");
        Status("下载请求已提交，请查看任务列表。");
    }

    void OnPathPicked(PickedPathInfo info)
    {
        if (info.Target == "pack")
        {
            _ = PreviewPack(info.Path);
        }
        else if (info.Target == "localImport" && TopLevel.GetTopLevel(this) is Window owner)
        {
            _ = LocalImportDialog.Open(service, owner, info.Path);
        }
        else if (info.Target == "packRoot")
        {
            PackRootBox.Text = info.Path;
        }
        else if (info.Target == "download" && pendingVersion.Length > 0)
        {
            var versionId = pendingVersion;
            var token = pendingToken;
            pendingVersion = "";
            pendingToken = "";
            if (token.Length > 0)
            {
                _ = service!.QueueContentVersionAsync(token, versionId, "local", info.Path);
                Status("下载请求已提交，请查看任务列表。");
            }
        }
    }

    void OnLoaders(LoaderListInfo info)
    {
        var game = LoaderGameBox.SelectedItem as string ?? "";
        var loader = LoaderValues[Math.Max(0, LoaderTypeBox.SelectedIndex)];
        if (info.Version != game || info.Loader != loader)
            return;
        LoaderVersionBox.ItemsSource = info.Versions;
        if (info.Versions.Count > 0)
            LoaderVersionBox.SelectedIndex = 0;
        LoaderHint.Text = info.Versions.Count == 0
            ? "没有读取到加载器版本。"
            : $"已读取 {info.Versions.Count} 个 {LoaderLabels[LoaderTypeBox.SelectedIndex]} 版本。";
    }

    void OnPackPreview(PackPreviewInfo info)
    {
        pack = info;
        PackPreviewCard.IsVisible = true;
        PackTitle.Text = info.Name;
        PackMeta.Text = $"{info.Game} · {info.Loader} · 来源目录 {info.Root}";
        PackNameBox.Text = info.Name;
        PackRootBox.Text = info.Root;
        PackHint.Text = "已读取整合包信息，确认后加入导入队列。";
    }

    async Task PreviewPack(string path)
    {
        Status("正在读取整合包…");
        var result = await service!.PreviewPackAsync(path);
        if (result.Ok && result.Preview is { } preview)
            OnPackPreview(preview);
        else
            PackHint.Text = "读取整合包失败：" + result.Error;
    }

    void OnContentError(ContentErrorInfo info)
    {
        if (info.Category == "vanilla")
        {
            loadingVanilla = false;
            VanillaHint.Text = info.Message;
        }
        else
        {
            Status(info.Message);
            if (selected is not null)
            {
                DetailVersionsHint.Text = "读取版本失败：" + info.Message;
                ChooseVersionButton.IsVisible = true;
                VersionList.IsVisible = false;
                TargetModeBox.IsVisible = false;
                DownloadButton.IsVisible = false;
            }
            else if (ResultsPanel.IsVisible)
            {
                ResultsPanel.Children.Clear();
                ResultsPanel.Children.Add(Muted("搜索失败：" + info.Message));
            }
        }
    }

    void Status(string message) => StatusText.Text = message;

    static TextBlock Muted(string text) => new() { Text = text, Classes = { "muted" } };
}
