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
        window.Navigate("accounts");
        await Task.Delay(10);
        await Click(Find(window, "MUA"));
        Check(Sent.Last().GetProperty("account").GetString() == "mua", "native account mode switch");
        var dialog = ResourceDialog.Open(Session, window, new NativeItem { Id = "sodium", Name = "Sodium" }, "mod");
        await Task.Delay(30);
        // Owned modal windows form their own visual root.
        var owned = window.OwnedWindows.Last();
        await Click(Find(owned, "读取兼容资源版本"));
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
            using var frame = window.CaptureRenderedFrame();
            Check(frame != null, "Skia native frame " + name);
            frame!.Save("work/native-ui/home-" + name + ".png");
        }
        window.Navigate("sources");
        await Task.Delay(20);
        using (var frame = window.CaptureRenderedFrame())
            frame!.Save("work/native-ui/sources.png");
        window.Close();
        Console.WriteLine("ALL_NATIVE_UI_CHECKS_PASSED (headless native controls and simulated application responses; no live account/game)");
    }
}
