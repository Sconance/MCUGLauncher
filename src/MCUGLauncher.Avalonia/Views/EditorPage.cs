using System.Text.Json;
using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;

namespace MCUGLauncher.Avalonia.Views;
internal sealed class EditorPage : NativePage
{
    readonly StackPanel existing = Ui.Stack();
    public EditorPage(LauncherFacade session) : base(session)
    {
        Title("管理员与公告编辑", "沿用现有后端权限与本地待发布流程；此页不改变服务端权限。");
        var key = Ui.Input("", "本机管理员密钥");
        key.PasswordChar = '●';
        var result = Ui.Text("");
        Body.Children.Add(Ui.Card(Ui.Stack(
                    key,
                    Ui.Button("验证管理员", async () =>
                        {
                            var response = await Session.RequestAsync("keyResult", new
                            {
                                type = "verifyKey",
                                role = "admin",
                                key = key.Text ?? ""
                            });
                            key.Text = "";
                            result.Text = response.Message;
                        }),
                    result
        )));
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
        Body.Children.Add(existing);
        Draw();
    }

    void Draw()
    {
        existing.Children.Clear();
        foreach (var key in new[]
            {
                "announcements",
                "recommendations"
            }

        )
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
