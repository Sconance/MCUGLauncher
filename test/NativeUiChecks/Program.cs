using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MCUGLauncher.Avalonia;
using MCUGLauncher.Avalonia.Presentation;
using MCUGLauncher.Avalonia.Views;
using MCUGLauncher.Models;

internal static class Program
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    static readonly List<JsonElement> Sent = [];
    static LauncherFacade Session = null!;
    static int code;
    [STAThread]
    public static int Main()
    {
        AppBuilder.Configure<App>().UseSkia().UseHeadless(new()
        {
            UseHeadlessDrawing = false
        }).SetupWithoutStarting();
        using var stop = new CancellationTokenSource();
        Dispatcher.UIThread.Post(async () => { try { await Run(); } catch (Exception ex) { Console.Error.WriteLine(ex); code = 1; } finally { stop.Cancel(); } });
        Dispatcher.UIThread.MainLoop(stop.Token);
        return code;
    }
    static void Check(bool value, string description)
    {
        if (!value)
            throw new Exception(description);
        Console.WriteLine("PASS " + description);
    }
    static void Deliver(object message) => Session.Receive(JsonSerializer.Serialize(message, Json));
    static Task Send(object message)
    {
        var item = JsonSerializer.SerializeToElement(message, Json);
        Sent.Add(item);
        var type = item.GetProperty("type").GetString();
        if (type == "vanillaVersions")
            Deliver(new
            {
                type = "vanillaList",
                items = new[] { new { id = "1.21.1", released = "2024-08-08" } }
            });
        if (type == "listSaveBackups")
            Deliver(new
            {
                type = "saveBackups",
                items = Array.Empty<object>()
            });
        if (type == "readLaunchOptions")
            Deliver(new
            {
                type = "launchOptions",
                requestId = item.GetProperty("requestId").GetString(),
                token = "launch-token",
                name = "Fixture",
                inherited = false,
                settings = new
                {
                    ramMb = 4096,
                    width = 1200,
                    height = 720
                }
            });
        if (type == "testSource")
            Deliver(new
            {
                type = "testResult",
                name = item.GetProperty("name").GetString(),
                url = item.GetProperty("url").GetString(),
                requestId = item.GetProperty("requestId").GetString(),
                ok = true,
                ms = 24
            });
        if (type == "verifyKey")
            Deliver(new
            {
                type = "keyResult",
                ok = true,
                message = "已验证"
            });
        if (type == "saveServer")
            Deliver(new
            {
                type = "serverResult",
                ok = true
            });
        if (type == "searchContent")
            Deliver(new
            {
                type = "contentList",
                requestId = item.GetProperty("requestId").GetString(),
                items = new[] { new { id = "sodium", name = "Sodium", description = "Fixture resource", downloads = 123L } }
            });
        if (type == "listContentVersions")
            Deliver(new
            {
                type = "resourceVersions",
                requestId = item.GetProperty("requestId").GetString(),
                token = "version-token",
                game = "1.21.1",
                loader = "fabric",
                items = new[] { new { id = "v1", name = "Version one" }, new { id = "v2", name = "Version two" } }
            });
        return Task.CompletedTask;
    }
    static IEnumerable<T> Controls<T>(Control view) where T : Control => view.GetVisualDescendants().OfType<T>();
    static Button Find(Control view, string label) => Controls<Button>(view).First(b => b.Content?.ToString() == label);
    static async Task Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(25);
    }
    static async Task Run()
    {
        Session = new LauncherFacade(Send);
        var sources = new DataSourceConfig();
        Deliver(new
        {
            type = "state",
            player = "Steve",
            accountType = "offline",
            selectedVersion = "fixture",
            selectedInstance = "fixture",
            root = "fixture",
            ram = 4096,
            instances = new[] { new { name = "fixture", displayName = "测试实例", loader = "Fabric", minecraft = "1.21.1" } },
            mods = new[] { new { id = "sodium", name = "Sodium", file = "sodium.jar", enabled = true, v = "1.0", downloads = "" } },
            dataSources = sources,
            defaultDataSources = sources,
            customDataSources = sources,
            dataSourceMode = "default"
        });
        Deliver(new
        {
            type = "community",
            payload = new Dictionary<string, string> { { "announcements", "{\"items\":[{\"id\":\"a\",\"title\":\"测试公告\",\"body\":\"联网内容使用原生控件展示\"}]}" } }
        });
        Check(Session.Community["announcements"].Length == 1, "community envelope parsed as typed native entries");
        Deliver(new
        {
            type = "tasks",
            items = new[] { new { id = "pending", name = "准备任务", state = "Queued", percent = (double?)null } }
        });
        Check(Session.Tasks[0].Percent == null, "indeterminate task progress parses without losing the state");
        using (var face = new global::Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(8, 8)))
        {
            using (var drawing = face.CreateDrawingContext())
                drawing.FillRectangle(global::Avalonia.Media.Brushes.Blue, new Rect(0, 0, 8, 8));
            using var png = new MemoryStream();
            face.Save(png);
            var blue = "data:image/png;base64," + Convert.ToBase64String(png.ToArray());
            Deliver(new
            {
                type = "accountState",
                accountType = "microsoft",
                player = "AvatarA"
            });
            Deliver(new
            {
                type = "avatar",
                account = "microsoft",
                player = "AvatarA",
                image = blue
            });
            Deliver(new
            {
                type = "accountState",
                accountType = "mua",
                player = "AvatarB"
            });
            Check(Session.Avatar != blue, "avatar is isolated between account modes");
            Deliver(new
            {
                type = "avatar",
                account = "microsoft",
                player = "AvatarA",
                image = blue
            });
            Check(Session.Avatar != blue, "late avatar from previous account ignored");
            Deliver(new
            {
                type = "accountState",
                accountType = "microsoft",
                player = "AvatarA"
            });
            Check(Session.Avatar == blue, "switching back restores cached avatar immediately");
            Deliver(new
            {
                type = "avatar",
                account = "microsoft",
                player = "AvatarA",
                image = AvaloniaAvatarImage.Encode(null),
                fallback = true
            });
            Check(Session.Avatar == blue, "failed refresh does not replace cached online skin");
            Deliver(new
            {
                type = "accountState",
                accountType = "offline",
                player = "Steve"
            });
        }
        var unrelated = Session.RequestAsync("probeResult", new
        {
            type = "probe",
            requestId = "probe-request"
        });
        Deliver(new
        {
            type = "error",
            requestId = "another-request",
            message = "Unrelated"
        });
        Check(!unrelated.IsCompleted, "an unrelated command error does not cancel another page request");
        Deliver(new
        {
            type = "probeResult",
            requestId = "probe-request"
        });
        await unrelated;
        var window = new ShellWindow(Session);
        window.Show();
        Directory.CreateDirectory("work/native-ui");
        foreach (var entry in NavigationCatalog.All)
        {
            window.Navigate(entry.Id);
            await Task.Delay(20);
            Check(window.GetVisualDescendants().OfType<NativePage>().Any(), "native route " + entry.Id);
        }
        await CodexParity(window);
        window.Navigate("sources");
        await Task.Delay(20);
        Check(Find(window, "已使用默认源").IsEnabled == false, "default source active status");
        var before = Sent.Count(i => i.GetProperty("type").GetString() == "testSource");
        await Click(Find(window, "自定义源"));
        Check(Sent.Count(i => i.GetProperty("type").GetString() == "testSource") == before + 4, "tab entry automatically tests all four sources");
        var boxes = Controls<TextBox>(window).ToArray();
        boxes[0].Text = "https://example.org/custom.json";
        await Click(Find(window, "保存自定义源"));
        Check(Sent.Last().GetProperty("type").GetString() == "saveCustomSources", "save custom preset does not activate it");
        await Click(Find(window, "使用自定义源"));
        Check(Sent.Last().GetProperty("type").GetString() == "useDataSources", "explicit native activation command");
        window.Navigate("home");
        await Task.Delay(10);
        await Click(Find(window, "MUA 登录"));
        Check(Sent.Last().GetProperty("account").GetString() == "mua", "native account mode switch");
        var dialog = ResourceDialog.Open(Session, window, new NativeItem { Id = "sodium", Name = "Sodium" }, "mod");
        await Task.Delay(30);
        // Owned modal windows form their own visual root.
        var owned = window.OwnedWindows.Last();
        await Click(Find(owned, "读取兼容资源版本"));
        await Task.Delay(25);
        var description = Controls<StackPanel>(owned).Single(c => c.Name == "ResourceDescription");
        var install = Controls<StackPanel>(owned).Single(c => c.Name == "ResourceInstallOptions");
        Check(description.TranslatePoint(default, owned)!.Value.X < install.TranslatePoint(default, owned)!.Value.X, "resource description stays left of installation options");
        using (var frame = owned.CaptureRenderedFrame())
            frame!.Save("work/native-ui/resource-detail.png");
        owned.Width = 520;
        await Task.Delay(25);
        Check(install.TranslatePoint(default, owned)!.Value.Y > description.TranslatePoint(default, owned)!.Value.Y, "narrow resource dialog moves options below its description without hiding controls");
        owned.Width = 1000;
        await Task.Delay(25);
        var combos = Controls<ComboBox>(owned).ToArray();
        combos[2].SelectedIndex = 1;
        await Click(Find(owned, "确认下载"));
        Check(Sent.Last().GetProperty("versionId").GetString() == "v2", "native download uses explicitly selected resource version");
        owned.Close();
        await dialog;
        Deliver(new
        {
            type = "tasks",
            items = new[] { new { id = "task", name = "安装测试", state = "Running", percent = 42.0, message = "整体安装进度" } }
        });
        window.Navigate("tasks");
        await Task.Delay(10);
        await Click(Find(window, "暂停"));
        Check(Sent.Any(i => i.GetProperty("type").GetString() == "taskAction" && i.GetProperty("action").GetString() == "pause"), "task pause remains an application command");
        var confirmation = Ui.Confirm(window, "测试关闭确认", "未保存的内容可能丢失。");
        await Task.Delay(20);
        await Click(Find(window.OwnedWindows.Last(), "取消"));
        Check(!await confirmation, "native secondary confirmation cancellation preserves the operation");
        Session.Report("AI 迁移框架尝试 · 原生布局检查（模拟数据）");
        foreach (var (size, name) in new[] { ((960, 640), "small"), ((1280, 800), "medium"), ((1920, 1080), "large") })
        {
            window.Width = size.Item1;
            window.Height = size.Item2;
            window.Navigate("home");
            await Task.Delay(35);
            CheckHomeGeometry(window);
            using var frame = window.CaptureRenderedFrame();
            Check(frame != null, "Skia native frame " + name);
            frame!.Save("work/native-ui/home-" + name + ".png");
        }
        window.Navigate("sources");
        window.Width = 1280;
        window.Height = 800;
        await Task.Delay(20);
        using (var frame = window.CaptureRenderedFrame())
            frame!.Save("work/native-ui/sources.png");
        window.Navigate("mod");
        await Task.Delay(25);
        await Click(Find(window, "搜索 / 刷新"));
        using (var frame = window.CaptureRenderedFrame())
            frame!.Save("work/native-ui/catalog.png");
        window.Navigate("home");
        await Task.Delay(25);
        await Click(Find(window, "›"));
        await Task.Delay(25);
        var picker = Controls<InstancePickerView>(window).Single();
        var roots = Controls<ComboBox>(picker).Single();
        Check(roots.Bounds.Width > picker.Bounds.Width - 110, "instance directory stretches beside its add action");
        using (var frame = window.CaptureRenderedFrame())
            frame!.Save("work/native-ui/instance-panel.png");
        window.Close();
        Console.WriteLine("ALL_NATIVE_UI_CHECKS_PASSED (headless native controls and simulated application responses; no live account/game)");
    }
    static void CheckHomeGeometry(ShellWindow window)
    {
        var account = Controls<Border>(window).Single(c => c.Name == "HomeAccountCard");
        var footer = Controls<Border>(window).Single(c => c.Name == "HomeAccountFooter");
        var launch = Controls<Button>(window).Single(c => c.Name == "HomeLaunchButton");
        var footerBottom = footer.TranslatePoint(default, account)!.Value.Y + footer.Bounds.Height;
        Check(Math.Abs(account.Bounds.Height - 426) < 1 && account.Bounds.Height - footerBottom < 15, "account footer remains anchored at the bottom of the original card");
        Check(Math.Abs(account.TranslatePoint(default, window)!.Value.X - launch.TranslatePoint(default, window)!.Value.X) < 1, "launch action aligns with the account card's left edge");
        var panels = Controls<HomeFeedPanel>(window).ToArray();
        Check(Math.Abs(panels[1].Bounds.Height - 112) < 1, "announcement remains the compact middle dashboard row");
        Check(Math.Abs(panels.Last().Bounds.Height - 100) < 1, "quote strip retains its horizontal 100px region");
        var progress = Controls<ProgressBar>(window).First(p => p.Name == "TaskProgress");
        Check(progress.Bounds.Width <= 120, "titlebar progress stays inside its task summary width");
    }
    static async Task CodexParity(ShellWindow window)
    {
        var expected = new[] { "启动设置", "Java 管理", "安装与导入", "下载与网络", "界面与背景", "更多选项", "管理员", "数据源", "服务器收藏", "运行中的游戏", "实例维护", "我的收藏", "存档保护", "下载任务", "任务与恢复", "运行日志", "诊断与反馈", "关于与许可" };
        Check(NavigationCatalog.All.Where(i => i.Group == "settings" && i.Visible).Select(i => i.Label).SequenceEqual(expected), "settings sections preserve codex order and labels");
        Session.HomeInstancePanelOpen = false;
        window.Navigate("home");
        await Task.Delay(25);
        Check(!Controls<InstancePickerView>(window).Any(), "home defaults to dashboard, not permanent instance list");
        Check(Controls<HomeFeedPanel>(window).Count() == 4, "home retains separate server, announcement, recommendation and quote regions");
        foreach (var label in new[] { "正版", "离线", "MUA 登录", "账号设置", "切换本地账号" })
            Check(Controls<Button>(window).Any(b => b.Content?.ToString() == label), "home account control " + label);
        await Click(Find(window, "›"));
        Check(Controls<InstancePickerView>(window).Count() == 1, "launch arrow opens right-hand instance panel");
        await Click(Find(window, "收起"));
        Check(!Controls<InstancePickerView>(window).Any(), "instance close restores home dashboard");
        await Click(Find(window, "切换本地账号"));
        var dialog = window.OwnedWindows.Last();
        Check(dialog.Title == "切换本地账号" && dialog.Owner == window && dialog.WindowStartupLocation == WindowStartupLocation.CenterOwner, "offline switch remains a centered owned dialog");
        using (var frame = dialog.CaptureRenderedFrame())
            frame!.Save("work/native-ui/offline-account-dialog.png");
        dialog.Close();
        await Task.Delay(20);
        var avatar = Controls<Button>(window).First(b => b.Content is Image);
        await Click(avatar);
        dialog = window.OwnedWindows.Last();
        Check(dialog.Title == "皮肤与披风", "avatar keeps original skin dialog entry point");
        dialog.Close();
        await Task.Delay(20);
        await Click(Find(window, "添加服务器"));
        dialog = window.OwnedWindows.Last();
        Check(dialog.Title == "添加服务器", "server creation stays on home as a dialog");
        dialog.Close();
        await Task.Delay(20);
        await Click(Find(window, "↓ 下载"));
        Check(Controls<CommunityPage>(window).Any(), "top download opens group recommendations as in codex");
        foreach (var label in new[] { "服务器", "材质包", "光影" })
            Check(Controls<Button>(window).Any(b => b.Content?.ToString() == label), "recommendation category " + label);
        window.Navigate("options");
        await Task.Delay(20);
        Check(Controls<Button>(window).Any(b => b.Content?.ToString() == "保存密钥"), "community key settings restored");
        window.Navigate("settings");
        await Task.Delay(20);
        Check(Controls<TextBlock>(window).Any(t => t.Text == "游戏窗口宽") && Controls<TextBlock>(window).Any(t => t.Text == "游戏窗口高"), "basic launch dimensions restored in original settings page");
        window.Navigate("editor");
        await Task.Delay(20);
        Check(!Find(window, "发布公告").IsEnabled, "admin publishing entry starts behind the original local verification gate");
        await Click(Find(window, "验证管理员"));
        await Click(Find(window, "发布公告"));
        var publishDialog = window.OwnedWindows.Last();
        Check(publishDialog.Title == "发布公告" && publishDialog.Owner == window, "announcement editing remains an owned dialog, not a merged settings form");
        using (var frame = publishDialog.CaptureRenderedFrame())
            frame!.Save("work/native-ui/announcement-editor-dialog.png");
        publishDialog.Close();
        await Task.Delay(20);
        Console.WriteLine("CODEX_UI_PARITY_CHECKS_PASSED (entry points and actions, not a claim of pixel-perfect rendering)");
    }

}
