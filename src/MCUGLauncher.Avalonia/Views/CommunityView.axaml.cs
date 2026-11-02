using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace MCUGLauncher.Avalonia.Views;

internal partial class CommunityView : UserControl
{
    LauncherFacade? facade;
    string imageData = "";
    Bitmap? image;

    public CommunityView()
    {
        InitializeComponent();
        WireButtons();
    }

    public CommunityView(LauncherFacade facade) : this()
    {
        Attach(facade);
    }

    public void Attach(LauncherFacade value)
    {
        facade = value;
        facade.StateChanged += ApplyState;
        facade.CommunityChanged += OnCommunity;
        facade.PublishResultReceived += OnPublishResult;
        facade.KeyResultReceived += OnKeyResult;
        facade.PathPicked += OnPathPicked;
        facade.ToastReceived += info => PublishStatus.Text = info.Message;
        facade.ErrorReceived += message => PublishStatus.Text = "操作失败：" + message;
        ApplyState(facade.State);
        OnCommunity(facade.Community);
    }

    void WireButtons()
    {
        VerifyAdminButton.Click += async (_, _) =>
        {
            var key = AdminKeyBox.Text?.Trim() ?? "";
            if (key.Length == 0)
            {
                AdminStatus.Text = "请输入管理员密钥。";
                return;
            }
            AdminStatus.Text = "正在验证…";
            await facade!.VerifyKeyAsync("admin", key);
        };
        PickImageButton.Click += async (_, _) => await facade!.PickFileAsync("announcementImage");
        ClearImageButton.Click += (_, _) => ClearImage();
        PublishButton.Click += async (_, _) =>
        {
            var title = TitleBox.Text?.Trim() ?? "";
            var body = BodyBox.Text?.Trim() ?? "";
            if (title.Length == 0 || body.Length == 0)
            {
                PublishStatus.Text = "标题和正文不能为空。";
                return;
            }
            PublishStatus.Text = "正在保存公告…";
            await facade!.PublishAsync(title, body, ExpiresBox.Text?.Trim() ?? "", imageData);
        };
    }

    void ApplyState(LauncherState? snapshot)
    {
        if (snapshot is null)
            return;
        var hasKey = snapshot.HasAdminKey;
        AdminHint.Text = hasKey
            ? "本机已设置管理员密钥。密钥只保存在本机，不会上传。"
            : "尚未设置管理员密钥。首次输入任意 8 位以上密钥即设置本机管理员密钥。";
        PublishButton.IsEnabled = hasKey;
        if (!hasKey)
            PublishStatus.Text = "请先设置管理员密钥后再发布公告。";
    }

    void OnCommunity(CommunityData data)
    {
        AnnouncementsPanel.Children.Clear();
        if (data.Announcements.Count == 0)
        {
            AnnouncementsPanel.Children.Add(Muted("暂无公告。"));
            return;
        }
        foreach (var item in data.Announcements.Take(10))
        {
            var copy = new StackPanel { Spacing = 2 };
            copy.Children.Add(new TextBlock
            {
                Text = "[" + item.Tag + "] " + item.Title,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });
            copy.Children.Add(new TextBlock { Text = item.Body, TextWrapping = TextWrapping.Wrap, Classes = { "muted" } });
            copy.Children.Add(new TextBlock
            {
                Text = string.Join(" · ", new[]
                {
                    item.Author,
                    item.PublishedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "",
                    item.ImageUrl.Length > 0 ? "含配图" : ""
                }.Where(value => value.Length > 0)),
                Classes = { "muted" }
            });
            AnnouncementsPanel.Children.Add(copy);
        }
    }

    async void OnPathPicked(PickedPathInfo info)
    {
        if (info.Target != "announcementImage")
            return;
        try
        {
            var bytes = await File.ReadAllBytesAsync(info.Path);
            if (bytes.Length > 5 * 1024 * 1024)
            {
                PublishStatus.Text = "公告配图不能超过 5 MB。";
                return;
            }
            image?.Dispose();
            using var stream = new MemoryStream(bytes);
            image = new Bitmap(stream);
            ImagePreview.Source = image;
            imageData = "data:" + Mime(info.Path) + ";base64," + Convert.ToBase64String(bytes);
            ImageLabel.Text = Path.GetFileName(info.Path) + " · " + (bytes.Length / 1024) + " KB";
            ClearImageButton.IsEnabled = true;
            PublishStatus.Text = "公告配图已选择。";
        }
        catch (Exception ex)
        {
            PublishStatus.Text = "读取公告配图失败：" + ex.Message;
        }
    }

    void OnPublishResult(PublishResultInfo info)
    {
        PublishStatus.Text = info.Message;
        if (!info.Ok)
            return;
        TitleBox.Text = "";
        BodyBox.Text = "";
        ExpiresBox.Text = "";
        ClearImage();
        _ = facade!.RefreshCommunityAsync();
    }

    void OnKeyResult(KeyResultInfo info)
    {
        AdminStatus.Text = info.Message;
        if (info.Ok)
        {
            AdminKeyBox.Text = "";
            _ = facade!.RequestStateAsync();
        }
    }

    void ClearImage()
    {
        imageData = "";
        image?.Dispose();
        image = null;
        ImagePreview.Source = null;
        ImageLabel.Text = "";
        ClearImageButton.IsEnabled = false;
    }

    static string Mime(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        _ => "image/png"
    };

    static TextBlock Muted(string text) => new() { Text = text, Classes = { "muted" } };
}
