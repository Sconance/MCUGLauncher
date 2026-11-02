using Avalonia.Controls;
using MCUGLauncher.Models;
using MCUGLauncher.Avalonia.Presentation;

namespace MCUGLauncher.Avalonia.Views;
internal sealed class SourcesPage : NativePage
{
    static readonly (string Key, string Label)[] Fields = [("announcements", "公告"), ("servers", "服务器"), ("recommendations", "推荐"), ("quotes", "语录")];
    readonly StackPanel rows = Ui.Stack();
    readonly Dictionary<string, TextBlock> results = [];
    readonly Dictionary<string, (string Id, string Url)> requests = [];
    readonly Dictionary<string, string> draft;
    readonly Button use;
    string mode = "default";
    public SourcesPage(LauncherFacade session) : base(session)
    {
        draft = Values(State.CustomDataSources);
        Title("数据源设置", "进入页面或切换页签自动测试，也可逐项手动测试。保存与使用分开。");
        use = Ui.Button("使用默认源", () => Send(new { type = "useDataSources", mode }), true);
        Body.Children.Add(Ui.Row(
                Ui.Action("默认源", () => Switch("default")),
                Ui.Action("自定义源", () => Switch("custom"))
        ));
        Body.Children.Add(rows);
        Switch(mode);
    }

    static Dictionary<string, string> Values(DataSourceConfig source) => new()
    {
        ["announcements"] = source.Announcements,
        ["servers"] = source.Servers,
        ["recommendations"] = source.Recommendations,
        ["quotes"] = source.Quotes
    };
    void Switch(string next)
    {
        if (use.Parent is Panel parent)
            parent.Children.Remove(use);
        mode = next;
        rows.Children.Clear();
        results.Clear();
        requests.Clear();
        var sources = mode == "default" ? Values(State.DefaultDataSources) : draft;
        foreach (var (key, label) in Fields)
        {
            var input = Ui.Input(sources.GetValueOrDefault(key) ?? "");
            input.IsReadOnly = mode == "default";
            var result = Ui.Text("尚未测试");
            results[key] = result;
            var observed = input.Text;
            input.TextChanged += (_, _) =>
            {
                if (input.Text == observed)
                    return;
                observed = input.Text;
                if (mode == "custom")
                    draft[key] = input.Text ?? "";
                result.Text = "地址已修改，尚未测试";
            };
            rows.Children.Add(Ui.Card(Ui.Stack(
                        Ui.Field(label, input),
                        Ui.Button("测试", () => Test(key, input.Text ?? "")),
                        result
            )));
            _ = Test(key, input.Text ?? "");
        }

        var actions = Ui.Row();
        if (mode == "custom")
            actions.Children.Add(Ui.Button("保存自定义源", () => Send(new { type = "saveCustomSources", dataSources = new { announcements = draft["announcements"], servers = draft["servers"], recommendations = draft["recommendations"], quotes = draft["quotes"] } })));
        actions.Children.Add(use);
        rows.Children.Add(Ui.Text(mode == "custom" ? "使用自定义源会应用已保存的地址，未保存的修改不会生效。" : "默认地址只读，测试不会切换当前使用的源。"));
        rows.Children.Add(actions);
        Status();
    }

    Task Test(string name, string url)
    {
        var id = Guid.NewGuid().ToString("N");
        requests[name] = (id, url);
        results[name].Text = url.Length == 0 ? "未填写地址" : "测试中…";
        return url.Length == 0 ? Task.CompletedTask : Send(new
        {
            type = "testSource",
            name,
            url,
            requestId = id
        });
    }

    void Status()
    {
        var applied = State.DataSourceMode == mode && (mode == "default" || Values(State.CustomDataSources).OrderBy(i => i.Key).SequenceEqual(Values(State.DataSources).OrderBy(i => i.Key)));
        use.Content = (applied ? "已使用" : "使用") + (mode == "default" ? "默认源" : "自定义源");
        use.IsEnabled = !applied;
    }

    protected override void Receive(NativeMessage message)
    {
        if (message.Type == "state")
            Status();
        if (message.Type == "testResult" && requests.TryGetValue(message.Name, out var pending) && pending.Id == message.RequestId && pending.Url == message.Url && (mode != "custom" || draft[message.Name] == message.Url))
            results[message.Name].Text = message.Ok ? $"连接正常 · {message.Ms} ms" : message.Message;
    }
}
