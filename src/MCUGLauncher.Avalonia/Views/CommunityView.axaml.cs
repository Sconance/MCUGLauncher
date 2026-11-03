using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.Avalonia.Views;

internal partial class CommunityView : UserControl
{
    ILauncherService service = EmptyServiceFallback.Instance;
    CommunityData community = CommunityData.Empty;
    bool canManage;

    public CommunityView()
    {
        InitializeComponent();
        WireButtons();
    }

    public CommunityView(ILauncherService service) : this()
    {
        Attach(service);
    }

    public void Attach(ILauncherService value)
    {
        service = value ?? EmptyServiceFallback.Instance;
        service.StateChanged += ApplyState;
        service.CommunityChanged += OnCommunity;
        service.ToastReceived += info => ManageStatus.Text = info.Message;
        service.ErrorReceived += message => ManageStatus.Text = "操作失败：" + message;
        ApplyState(service.State);
        OnCommunity(service.Community);
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
            var result = await service!.VerifyKeyAsync("admin", key);
            AdminStatus.Text = result.Message;
            if (result.Ok)
            {
                AdminKeyBox.Text = "";
                _ = service.RequestStateAsync();
            }
        };
    }

    void ApplyState(LauncherState? snapshot)
    {
        if (snapshot is null)
            return;
        var hasKey = snapshot.HasAdminKey;
        canManage = hasKey;
        AdminHint.Text = hasKey
            ? "本机已设置管理员密钥。密钥只保存在本机，不会上传。"
            : "尚未设置管理员密钥。首次输入任意 8 位以上密钥即设置本机管理员密钥。";
        RenderManagement();
    }

    void OnCommunity(CommunityData data)
    {
        community = data;
        RenderAnnouncements();
        RenderManagement();
    }

    void RenderAnnouncements()
    {
        AnnouncementsPanel.Children.Clear();
        if (community.Announcements.Count == 0)
        {
            AnnouncementsPanel.Children.Add(Muted("暂无公告。"));
            return;
        }
        foreach (var item in community.Announcements.Take(10))
        {
            var copy = new StackPanel { Spacing = 2 };
            copy.Children.Add(new TextBlock
            {
                Text = item.Tag.Length > 0
                    ? "[" + item.Tag + "] " + item.Title
                    : item.Title,
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
            var open = new Button
            {
                Content = "查看完整公告",
                Classes = { "link" },
                HorizontalAlignment = HorizontalAlignment.Left,
                IsVisible = item.Markdown.Length > 0
            };
            open.Click += async (_, _) => await OpenAnnouncement(item);
            copy.Children.Add(open);
            AnnouncementsPanel.Children.Add(copy);
        }
    }

    async Task OpenAnnouncement(AnnouncementInfo item)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            ManageStatus.Text = "无法打开公告窗口。";
            return;
        }

        await new AnnouncementWindow(item).ShowDialog(owner);
    }

    void RenderManagement()
    {
        RecommendationManagePanel.Children.Clear();
        if (community.Recommendations.Count == 0)
            RecommendationManagePanel.Children.Add(Muted("没有可管理的推荐。"));
        else
            for (var index = 0; index < community.Recommendations.Count; index++)
            {
                var item = community.Recommendations[index];
                var copy = new StackPanel { Spacing = 2 };
                copy.Children.Add(new TextBlock
                {
                    Text = item.Name,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                });
                copy.Children.Add(new TextBlock
                {
                    Text = item.Type + " · " + item.Author,
                    Classes = { "muted" }
                });
                var remove = new Button { Content = "删除", Classes = { "ghost" }, IsEnabled = canManage };
                var position = index;
                remove.Click += async (_, _) => await RemoveRecommendation(position);
                RecommendationManagePanel.Children.Add(SeparatedRow(copy, remove));
            }
    }

    async Task RemoveRecommendation(int index)
    {
        if (!canManage || index < 0 || index >= community.Recommendations.Count)
            return;
        ManageStatus.Text = "正在保存推荐列表…";
        var remaining = community.Recommendations.Where((_, position) => position != index).ToArray();
        ManageStatus.Text = (await service!.SaveRecommendationsAsync(remaining)).Message;
    }

    static Control SeparatedRow(Control copy, Control action)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        row.Children.Add(copy);
        Grid.SetColumn(action, 1);
        action.Margin = new Thickness(12, 0, 0, 0);
        action.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(action);
        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#E3EAF0")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 6),
            Child = row
        };
    }

    static TextBlock Muted(string text) => new() { Text = text, Classes = { "muted" } };
}
