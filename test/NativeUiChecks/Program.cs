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
                items = new[] { new { id = "1.21.1", type = "release", released = "2024-08-08" }, new { id = "26.4-snapshot-6", type = "snapshot", released = "2026-01-01" }, new { id = "b1.7.3", type = "old_beta", released = "2011-01-01" } }
            });
        if (type == "listLoaders")
            Deliver(new
            {
                type = "loaderList",
                version = item.GetProperty("version").GetString(),
                loader = item.GetProperty("loader").GetString(),
                versions = new[] { "0.16.0" }
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
        await DialogParity(window);
        await WindowAndVanillaParity(window);
        await SettingsParity(window);
        await ButtonAppearance();
        window.Navigate("sources");
        await Task.Delay(20);
        Check(Find(window, "已使用默认源").IsEnabled == false, "default source active status");
        Check(Find(window, "默认源").Classes.Contains("primary"), "source tab selection uses original primary-button role");
        var before = Sent.Count(i => i.GetProperty("type").GetString() == "testSource");
        await Click(Find(window, "自定义源"));
        Check(Find(window, "自定义源").Classes.Contains("primary") && !Find(window, "默认源").Classes.Contains("primary"), "source tab visual selection follows the visible page");
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
        await Click(Find(window, "搜索"));
        await Click(Find(window, "♡"));
        Check(Sent.Last().GetProperty("type").GetString() == "toggleBookmark", "heart button preserves the existing bookmark command");
        var snapshot = JsonSerializer.SerializeToNode(Session.State with
        {
            Favorites = [new NativeItem { Id = "sodium", Kind = "mod", Name = "Sodium" }]
        }, Json)!;
        snapshot["type"] = "state";
        Session.Receive(snapshot.ToJsonString());
        Check(Find(window, "♥") != null, "filled heart follows confirmed favorite state");
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
    static async Task SettingsParity(ShellWindow window)
    {
        window.Navigate("about");
        await Task.Delay(30);
        Check(!Controls<TextBox>(window.FindControl<ContentControl>("PageHost")!).Any(), "about page has no empty license text area");
        Check(Find(window, "打开项目仓库") != null, "about page preserves a project link");
        using (var frame = window.CaptureRenderedFrame())
            frame!.Save("work/native-ui/about-clean.png");
        window.Navigate("settings");
        await Task.Delay(30);
        var scroll = window.FindControl<ScrollViewer>("PageScroll")!;
        var bar = Controls<global::Avalonia.Controls.Primitives.ScrollBar>(scroll)
            .First(b => b.Orientation == global::Avalonia.Layout.Orientation.Vertical && b.IsVisible);
        var thumb = Controls<global::Avalonia.Controls.Primitives.Thumb>(bar).Single();
        Check(bar.Bounds.Width >= 16, "vertical scrollbar keeps a wide pointer hit area");
        window.MouseMove(new Point(400, 60));
        await Task.Delay(600);
        var idleWidth = thumb.Bounds.Width;
        var edge = bar.TranslatePoint(new Point(bar.Bounds.Width / 2, bar.Bounds.Height / 2), window)!.Value;
        window.MouseMove(edge);
        await Task.Delay(600);
        Check(thumb.Bounds.Width > idleWidth && thumb.Bounds.Width >= 11, "approaching the scrollbar expands its thumb");
        using (var frame = window.CaptureRenderedFrame())
            frame!.Save("work/native-ui/scrollbar-expanded.png");
        var grip = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
        window.MouseDown(grip, global::Avalonia.Input.MouseButton.Left);
        window.MouseMove(grip + new Vector(0, 40));
        window.MouseUp(grip + new Vector(0, 40), global::Avalonia.Input.MouseButton.Left);
        await Task.Delay(30);
        Check(scroll.Offset.Y > 0, "expanded scrollbar still drags the page");
        scroll.Offset = default;
        window.MouseMove(new Point(400, 60));
        window.Navigate("network");
        await Task.Delay(25);
        await Click(Find(window, "保存网络设置"));
        Check(Sent.Last().GetProperty("network").GetProperty("proxyMode").GetString() == "系统代理", "network page sends actual policy settings instead of community URLs");
        using (var frame = window.CaptureRenderedFrame())
            frame!.Save("work/native-ui/settings-network.png");
        window.Navigate("appearance");
        await Task.Delay(25);
        await Click(Find(window, "保存外观设置"));
        Check(Sent.Last().GetProperty("appearance").GetProperty("path").GetString() == "", "appearance can save an empty background without a placeholder asset");
        using (var frame = window.CaptureRenderedFrame())
            frame!.Save("work/native-ui/settings-appearance.png");
        window.Navigate("settings");
        await Task.Delay(30);
        var chinese = Controls<CheckBox>(window).Single(c => c.Content?.ToString() == "启动时自动切换为简体中文");
        Check(chinese.IsChecked == true, "automatic Simplified Chinese is enabled by default");
        chinese.IsChecked = false;
        await Click(Find(window, "保存设置"));
        Check(!Sent.Last().GetProperty("settings").GetProperty("autoChinese").GetBoolean(), "language preference is sent through the shared launch editor");
        using (var frame = window.CaptureRenderedFrame())
            frame!.Save("work/native-ui/settings-launch.png");
        window.Navigate("saves");
        await Task.Delay(25);
        await Click(Find(window, "保存备份设置"));
        Check(Sent.Last().GetProperty("backupCount").GetInt32() == 5, "save protection exposes existing backup retention");
        window.Navigate("directories");
        await Task.Delay(25);
        Check(Find(window, "正在使用").IsEnabled == false, "active directory cannot be removed or switched redundantly");
        using (var frame = window.CaptureRenderedFrame())
            frame!.Save("work/native-ui/settings-directories.png");
        Session.Report("社区内容已刷新。");
        await Task.Delay(25);
        var notice = Controls<NotificationView>(window).Single();
        var text = Controls<TextBlock>(notice).Single(t => t.Name == "NotificationText");
        Check(text.Text == "社区内容已刷新" && text.TextAlignment == global::Avalonia.Media.TextAlignment.Center, "notification text is centered and omits the final full stop");
        Check(Math.Abs(text.TranslatePoint(default, notice)!.Value.X + text.Bounds.Width / 2 - notice.Bounds.Width / 2) < 1, "notification text area is centered independently of its close button");
        using (var frame = window.CaptureRenderedFrame())
            frame!.Save("work/native-ui/notification-centered.png");
        await Click(Controls<Button>(notice).Single());
    }

    static void CheckDialogFooter(Window dialog, string primaryLabel)
    {
        Check(dialog is DialogWindow && dialog.SystemDecorations == SystemDecorations.BorderOnly, "shared native dialog shell: " + dialog.Title);
        var footer = Controls<Border>(dialog).Single(c => c.Name == "DialogFooter");
        var body = Controls<ContentControl>(dialog).Single(c => c.Name == "DialogBody");
        var scroller = Controls<ScrollViewer>(dialog).Single(s => ReferenceEquals(s.Content, body));
        scroller.ScrollToEnd();
        dialog.UpdateLayout();
        var bodyBottom = body.TranslatePoint(new Point(0, body.Bounds.Height), dialog)!.Value.Y;
        var footerTop = footer.TranslatePoint(default, dialog)!.Value.Y;
        Check(bodyBottom <= footerTop + 1, "scrolling reaches the complete dialog body above its fixed footer: " + dialog.Title + " / " + bodyBottom + " / " + footerTop);
        using (var frame = dialog.CaptureRenderedFrame())
            frame!.Save("work/native-ui/dialog-bottom-" + primaryLabel + ".png");
        scroller.ScrollToHome();
        dialog.UpdateLayout();
        foreach (var text in Controls<TextBlock>(body))
            Check(text.Bounds.Width <= dialog.ClientSize.Width - 43, "dialog body text stays within the content width: " + text.Text + " / " + text.Bounds.Width + " / " + dialog.ClientSize.Width);
        var primary = Find(footer, primaryLabel);
        var cancel = Find(footer, "取消");
        Check(primary.TranslatePoint(default, dialog)!.Value.X > cancel.TranslatePoint(default, dialog)!.Value.X, "cancel precedes main action: " + dialog.Title);
        foreach (var button in Controls<Button>(footer))
        {
            var point = button.TranslatePoint(default, dialog)!.Value;
            Check(point.X >= 0 && point.X + button.Bounds.Width <= dialog.ClientSize.Width + 1 && point.Y + button.Bounds.Height <= dialog.ClientSize.Height + 1, "footer action stays inside dialog: " + button.Content);
        }
    }
    static async Task DialogParity(ShellWindow window)
    {
        foreach (var (mode, label) in new[] { ("microsoft", "打开登录窗口"), ("mua", "登录 MUA"), ("offline", "保存并切换") })
        {
            var sentBefore = Sent.Count;
            var pending = AccountDialogs.Open(Session, window, mode);
            await Task.Delay(30);
            var dialog = window.OwnedWindows.Last();
            CheckDialogFooter(dialog, label);
            using (var frame = dialog.CaptureRenderedFrame())
                frame!.Save("work/native-ui/dialog-" + mode + ".png");
            await Click(Find(dialog, "取消"));
            await pending;
            Check(Sent.Count == sentBefore, "canceling account dialog does not submit: " + mode);
        }
        var profiles = AccountDialogs.SelectProfile(Session, window, new NativeMessage { Type = "muaProfiles", Token = "dialog-profile", Items = [new NativeItem { Id = "profile", Name = "测试角色" }] });
        await Task.Delay(25);
        var profileDialog = window.OwnedWindows.Last();
        CheckDialogFooter(profileDialog, "使用选中角色");
        await Click(Find(profileDialog, "取消"));
        await profiles;
        Check(Sent.Last().GetProperty("type").GetString() == "selectMuaProfile" && Sent.Last().GetProperty("profileId").GetString() == "", "profile cancellation preserves explicit login cancellation");
        var server = ServerDialog.Open(Session, window);
        await Task.Delay(25);
        var serverDialog = window.OwnedWindows.Last();
        CheckDialogFooter(serverDialog, "添加服务器");
        using (var frame = serverDialog.CaptureRenderedFrame())
            frame!.Save("work/native-ui/dialog-server.png");
        await Click(Find(serverDialog, "取消"));
        await server;
        var skin = AccountDialogs.Skin(Session, window);
        await Task.Delay(25);
        var skinDialog = window.OwnedWindows.Last();
        skinDialog.Width = skinDialog.MinWidth;
        await Task.Delay(25);
        using (var frame = skinDialog.CaptureRenderedFrame())
            frame!.Save("work/native-ui/dialog-skin.png");
        CheckDialogFooter(skinDialog, "选择本地 PNG 皮肤");
        await Click(Find(skinDialog, "取消"));
        await skin;
        var launch = LaunchOptionsPage.Open(Session, window, "fixture");
        await Task.Delay(30);
        var launchDialog = window.OwnedWindows.Last();
        launchDialog.Height = 400;
        await Task.Delay(25);
        CheckDialogFooter(launchDialog, "保存设置");
        launchDialog.Height = 650;
        await Task.Delay(25);
        CheckDialogFooter(launchDialog, "保存设置");
        Check(Controls<ScrollViewer>(launchDialog).Any(), "long launch form scrolls independently of its footer");
        using (var frame = launchDialog.CaptureRenderedFrame())
            frame!.Save("work/native-ui/dialog-launch-options.png");
        await Click(Find(launchDialog, "取消"));
        await launch;
        foreach (var (label, page) in new (string, NativePage)[]
        {
            ("提交公告", new EditorPage(Session, "publish")),
            ("确认下载", new ResourceDialog(Session, new NativeItem { Id = "fixture", Name = "资源详情", Description = string.Join("\n", Enumerable.Repeat("较长的介绍内容，用于验证完整滚动范围", 24)) }, "mod"))
        })
        {
            var pending = Ui.Show(window, label, page, 1000, 400);
            await Task.Delay(30);
            var dialog = window.OwnedWindows.Last();
            CheckDialogFooter(dialog, label);
            dialog.Height = 650;
            await Task.Delay(25);
            CheckDialogFooter(dialog, label);
            await Click(Find(dialog, "取消"));
            await pending;
        }
        var confirmation = Ui.Confirm(window, "关闭游戏", "关闭后未保存的进度会丢失。", "关闭游戏", true);
        await Task.Delay(25);
        var confirmDialog = window.OwnedWindows.Last();
        CheckDialogFooter(confirmDialog, "关闭游戏");
        Check(Find(confirmDialog, "关闭游戏").Classes.Contains("danger-primary"), "destructive confirmation has a distinct action role");
        using (var frame = confirmDialog.CaptureRenderedFrame())
            frame!.Save("work/native-ui/dialog-confirm.png");
        await Click(Find(confirmDialog, "取消"));
        Check(!await confirmation, "destructive dialog cancel does not approve the operation");
        var escape = Ui.Show(window, "阅读提示", Ui.Text("按 Esc 关闭。"), 460, 300);
        await Task.Delay(25);
        var escapeDialog = window.OwnedWindows.Last();
        escapeDialog.KeyPress(global::Avalonia.Input.Key.Escape, global::Avalonia.Input.RawInputModifiers.None);
        await escape;
        Check(!window.OwnedWindows.Contains(escapeDialog), "escape dismisses the shared dialog without submission");
    }

    static async Task WindowAndVanillaParity(ShellWindow window)
    {
        Check(window.SystemDecorations == SystemDecorations.BorderOnly && window.ExtendClientAreaToDecorationsHint, "custom shell requests border-only chrome with integrated titlebar");
        Check(!Controls<TextBlock>(window).Any(t => t.Name == "StatusText"), "persistent footer text removed");
        Session.Report("操作提示测试");
        await Task.Delay(20);
        var notice = Controls<NotificationView>(window).Single();
        Check(notice.IsVisible, "backend feedback remains visible as a temporary notification");
        await Click(Controls<Button>(notice).Single());
        Check(!notice.IsVisible, "temporary notification can be dismissed");
        await Click(Controls<Button>(window).Single(b => b.Name == "MaximizeButton"));
        Check(window.WindowState == WindowState.Maximized, "integrated maximize action");
        await Click(Controls<Button>(window).Single(b => b.Name == "MaximizeButton"));
        Check(window.WindowState == WindowState.Normal, "integrated restore action");
        window.Navigate("vanilla");
        await Task.Delay(30);
        await Click(Find(window, "快照版"));
        Check(Controls<TextBlock>(window).Any(t => t.Text == "26.4-snapshot-6") && !Controls<TextBlock>(window).Any(t => t.Text == "1.21.1"), "snapshot filter displays snapshots separately from releases");
        await Click(Find(window, "安装"));
        Check(Sent.Last().GetProperty("version").GetString() == "26.4-snapshot-6", "snapshot installation uses selected snapshot id");
        await Click(Find(window, "远古版"));
        Check(Controls<TextBlock>(window).Any(t => t.Text == "b1.7.3"), "historical versions remain accessible");
        await Click(Find(window, "全部"));
        using (var frame = window.CaptureRenderedFrame())
            frame!.Save("work/native-ui/vanilla-types.png");
        window.Navigate("manage");
        await Task.Delay(25);
        var loading = Click(Find(window, "安装模组加载器"));
        await loading;
        var dialog = window.OwnedWindows.Last();
        await Click(Find(dialog, "读取加载器版本"));
        await Click(Find(dialog, "安装所选加载器"));
        Check(Sent.Last().GetProperty("type").GetString() == "installLoader" && Sent.Last().GetProperty("version").GetString() == "1.21.1", "installed-instance loader entry uses base Minecraft version, not instance display name");
        dialog.Close();
        await Task.Delay(20);
        using (var frame = window.CaptureRenderedFrame())
            frame!.Save("work/native-ui/manage-actions.png");
    }

    static async Task ButtonAppearance()
    {
        var activations = 0;
        var primary = Ui.Button("主要操作", () => { activations++; return Task.CompletedTask; }, true);
        var child = Ui.Text("按钮内嵌文字");
        primary.Content = child;
        var ghost = Ui.Action("普通操作", () => { });
        var soft = Ui.Action("次要操作", () => { });
        soft.Classes.Add("soft");
        var link = Ui.Action("文字链接", () => { });
        link.Classes.Add("link");
        var dialog = new Window { Width = 520, Height = 280, Content = Ui.Stack(primary, ghost, soft, link) };
        dialog.Show();
        await Task.Delay(30);
        static bool Color(global::Avalonia.Media.IBrush? brush, string expected) => brush is global::Avalonia.Media.ISolidColorBrush solid && solid.Color == global::Avalonia.Media.Color.Parse(expected);
        Check(Color(ghost.Background, "#FFFFFF") && Color(ghost.Foreground, "#414B55"), "ordinary buttons preserve white background and gray text");
        Check(Color(soft.Background, "#E8F2F8") && Color(soft.Foreground, "#0B4F7A"), "soft buttons use original tint and deep-blue text");
        Check(Color(primary.Background, "#006DB8") && Color(child.Foreground, "#FFFFFF"), "primary button text inherits white even inside custom content");
        using (var frame = dialog.CaptureRenderedFrame())
            frame!.Save("work/native-ui/button-states.png");
        var point = primary.TranslatePoint(new Point(10, 10), dialog)!.Value;
        dialog.MouseMove(point);
        await Task.Delay(20);
        Check(Color(primary.Background, "#2C6193") && Color(child.Foreground, "#FFFFFF"), "primary hover keeps original blue and white text");
        using (var frame = dialog.CaptureRenderedFrame())
            frame!.Save("work/native-ui/button-hover.png");
        dialog.MouseDown(point, global::Avalonia.Input.MouseButton.Left);
        await Task.Delay(20);
        Check(Color(primary.Background, "#005A99"), "primary pressed state avoids Fluent default gray");
        dialog.MouseUp(point, global::Avalonia.Input.MouseButton.Left);
        await Task.Delay(20);
        Check(activations == 1, "isolated button theme preserves real mouse activation");
        dialog.MouseMove(new Point(510, 260));
        primary.IsEnabled = false;
        await Task.Delay(20);
        Check(Color(primary.Background, "#ACC3D9") && Color(child.Foreground, "#FFFFFF"), "disabled primary keeps the original pale-blue and white palette");
        using (var frame = dialog.CaptureRenderedFrame())
            frame!.Save("work/native-ui/button-disabled.png");
        dialog.Close();
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
        var expected = new[] { "启动设置", "Java 管理", "游戏目录", "实例维护", "存档保护", "界面与背景", "下载与网络", "安装与导入", "维护与恢复", "账号与社区", "数据源", "服务器收藏", "我的收藏", "管理员", "下载任务", "运行中的游戏", "运行日志", "诊断与反馈", "关于" };
        Check(NavigationCatalog.All.Where(i => i.Group == "settings" && i.Visible).Select(i => i.Label).SequenceEqual(expected), "settings retain all functions with user-approved category grouping");
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
