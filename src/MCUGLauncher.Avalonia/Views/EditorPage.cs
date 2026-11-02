using System.Text.Json;
using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;

namespace MCUGLauncher.Avalonia.Views;
internal sealed class EditorPage : NativePage
{
    readonly StackPanel existing = Ui.Stack();
    readonly string mode;
    public EditorPage(LauncherFacade session, string mode = "admin") : base(session)
    {
        this.mode = mode;
        if (mode == "admin")
        {
            Title("管理员", "首次输入设置本机管理员密钥，验证成功后显示编辑入口。此处不代表服务端身份授权。");
            var key = Ui.Input("", "管理员密钥");
            key.PasswordChar = '●';
            var status = Ui.Text("");
            var publish = Ui.Button("发布公告", () => Ui.Show(Owner, "发布公告", new EditorPage(Session, "publish")), true);
            var recommendations = Ui.Button("管理群友推荐", () => Ui.Show(Owner, "管理群友推荐", new EditorPage(Session, "recommendations")));
            publish.IsEnabled = recommendations.IsEnabled = Session.AdminUiUnlocked;
            Body.Children.Add(Ui.Card(Ui.Stack(Ui.Heading("管理员认证"), key, Ui.Button("验证管理员", async () =>
            {
                var response = await Session.RequestAsync("keyResult", new
                {
                    type = "verifyKey",
                    role = "admin",
                    key = key.Text ?? ""
                });
                key.Text = "";
                status.Text = response.Message;
                Session.AdminUiUnlocked = response.Ok;
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
            var expires = Ui.Input("", "到期时间，可留空");
            var image = "";
            var imageLabel = Ui.Text("未选择配图");
            Body.Children.Add(Ui.Card(Ui.Stack(
                        Ui.Heading("发布公告"),
                        Ui.Field("标题", title),
                        Ui.Field("正文", body),
                        Ui.Field("到期时间", expires),
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
                        Ui.Button("发布 / 保存为待发布", () => Send(new { type = "publish", title = title.Text ?? "", body = body.Text ?? "", expiresAt = expires.Text ?? "", imageData = image }), true)
            )));
        }
        if (mode == "recommendations")
        {
            var name = Ui.Input();
            var desc = Ui.Input();
            var url = Ui.Input();
            Body.Children.Add(Ui.Card(Ui.Stack(
                        Ui.Heading("添加群友推荐"),
                        Ui.Field("名称", name),
                        Ui.Field("简介", desc),
                        Ui.Field("链接", url),
                        Ui.Button("保存本地推荐", () =>
                            {
                                var items = (Session.Community.GetValueOrDefault("recommendations") ?? []).Append(new CommunityEntry { Id = Guid.NewGuid().ToString("N"), Name = name.Text ?? "", Description = desc.Text ?? "", Url = url.Text ?? "", Author = State.Player }).ToArray();
                                return Send(new
                                {
                                    type = "saveRecommendations",
                                    items
                                });
                            })
            )));
        }
        Body.Children.Add(existing);
        Draw();
    }

    void Draw()
    {
        existing.Children.Clear();
        if (mode == "admin" && !Session.AdminUiUnlocked)
            return;
        var keys = mode == "recommendations" ? new[] { "recommendations" } : new[] { "announcements" };
        foreach (var key in keys)
            foreach (var item in Session.Community.GetValueOrDefault(key) ?? [])
            {
                existing.Children.Add(Ui.Card(Ui.Row(
                            Ui.Text(item.Title.Length > 0 ? item.Title : item.Name),
                            Ui.Button("删除本地条目", async () =>
                                {
                                    if (!await Ui.Confirm(Owner, "删除内容", "删除这条本地内容？"))
                                        return;
                                    var items = (Session.Community.GetValueOrDefault(key) ?? []).Where(i => i.Id != item.Id).ToArray();
                                    await Send(new
                                    {
                                        type = key == "announcements" ? "saveAnnouncements" : "saveRecommendations",
                                        items
                                    });
                                })
                )));
            }
    }

    protected override void Receive(NativeMessage message)
    {
        if (message.Type == "community")
            Draw();
    }
}
