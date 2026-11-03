using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;

namespace MCUGLauncher.Avalonia.Views;
internal sealed class EditorPage : NativePage
{
    readonly StackPanel existing = Ui.Stack();
    readonly string mode;
    public EditorPage(ILauncherService session, UiContext ui, string mode = "admin") : base(session, ui)
    {
        this.mode = mode;
        session.CommunityChanged += OnCommunity;
        UnsubscribeWith(() => session.CommunityChanged -= OnCommunity);
        if (mode == "admin")
        {
            Title("管理员", "首次输入设置本机管理员密钥，验证成功后显示编辑入口。此处不代表服务端身份授权。");
            var key = Ui.Input("", "管理员密钥");
            key.PasswordChar = '●';
            var status = Ui.Text("");
            var publish = Ui.Button("发布公告", () => Ui.Show(Owner, "发布公告", new EditorPage(Session, UI, "publish")), true);
            var recommendations = Ui.Button("管理群友推荐", () => Ui.Show(Owner, "管理群友推荐", new EditorPage(Session, UI, "recommendations")));
            publish.IsEnabled = recommendations.IsEnabled = UI.AdminUiUnlocked;
            Body.Children.Add(Ui.Card(Ui.Stack(Ui.Heading("管理员认证"), key, Ui.Button("验证管理员", async () =>
            {
                var response = await Session.VerifyKeyAsync(role: "admin", key: key.Text ?? "");
                key.Text = "";
                status.Text = response.Message;
                UI.AdminUiUnlocked = response.Ok;
                publish.IsEnabled = recommendations.IsEnabled = response.Ok;
                Draw();
            }), status)));
            Body.Children.Add(Ui.Card(Ui.Row(publish, recommendations)));
            Body.Children.Add(existing);
            Draw();
            return;
        }
        Title(mode == "publish" ? "发布公告" : "管理群友推荐");
        if (mode == "publish")
        {
            var title = Ui.Input();
            var body = Ui.Input();
            body.AcceptsReturn = true;
            body.MinHeight = 120;
            var expires = Ui.Input("", "YYYY-MM-DD HH:mm，可留空");
            var image = "";
            var imageLabel = Ui.Text("未选择配图");
            Body.Children.Add(Ui.Card(Ui.Stack(
                        Ui.Field("标题", title),
                        Ui.Field("正文", body),
                        Ui.Field("到期时间（可选）", expires),
                        imageLabel,
                        Ui.Button("选择配图", async () =>
                            {
                                var path = await FilePicker.Open(Owner, "公告图片", "*.png", "*.jpg", "*.jpeg");
                                if (path == null)
                                    return;
                                var info = new FileInfo(path);
                                if (info.Length > 2 * 1024 * 1024)
                                    throw new IOException("配图不能超过 2 MB。");
                                image = "data:image/" + (Path.GetExtension(path).ToLowerInvariant() == ".png" ? "png" : "jpeg") + ";base64," + Convert.ToBase64String(await File.ReadAllBytesAsync(path));
                                imageLabel.Text = Path.GetFileName(path);
                            }),
                        Ui.Muted("提交后按当前配置发布，或保存为本地待发布内容。")
            )));
            DialogActions = Ui.DialogActions(this, Ui.Button("提交公告", async () =>
            {
                var result = await Session.PublishAsync(title: title.Text ?? "", body: body.Text ?? "", expiresAt: expires.Text ?? "", imageData: image);
                if (!result.Ok)
                    throw new IOException(result.Message);
                (TopLevel.GetTopLevel(this) as Window)?.Close();
            }, true));
        }
        if (mode == "recommendations")
        {
            var name = Ui.Input();
            var desc = Ui.Input();
            var url = Ui.Input();
            Body.Children.Add(Ui.Card(Ui.Stack(Ui.Field("推荐名称", name), Ui.Field("简介", desc), Ui.Field("资源或服务器链接", url))));
            DialogActions = Ui.DialogActions(this, Ui.Button("保存本地推荐", async () =>
            {
                var items = (Session.Community.GetValueOrDefault("recommendations") ?? []).Append(new CommunityEntry { Id = Guid.NewGuid().ToString("N"), Name = name.Text ?? "", Description = desc.Text ?? "", Url = url.Text ?? "", Author = State.Player }).ToArray();
                await Session.SaveRecommendationsAsync(items: items);
                (TopLevel.GetTopLevel(this) as Window)?.Close();
            }, true));
        }
        Body.Children.Add(existing);
        Draw();
    }

    void Draw()
    {
        existing.Children.Clear();
        if (mode == "admin" && !UI.AdminUiUnlocked)
            return;
        var keys = mode == "recommendations" ? new[] { "recommendations" } : new[] { "announcements" };
        foreach (var key in keys)
            foreach (var item in Session.Community.GetValueOrDefault(key) ?? [])
            {
                existing.Children.Add(Ui.Card(Ui.Row(
                            Ui.Text(item.Title.Length > 0 ? item.Title : item.Name),
                            Ui.Button("删除本地条目", async () =>
                                {
                                    if (!await Ui.Confirm(Owner, "删除内容", "将从本地列表删除这条内容。该操作不能撤销。", "删除内容", true))
                                        return;
                                    var items = (Session.Community.GetValueOrDefault(key) ?? []).Where(i => i.Id != item.Id).ToArray();
                                    await Session.SaveCommunityAsync(key, items);
                                })
                )));
            }
    }

    void OnCommunity(IReadOnlyDictionary<string, CommunityEntry[]> entries) => Draw();
}
