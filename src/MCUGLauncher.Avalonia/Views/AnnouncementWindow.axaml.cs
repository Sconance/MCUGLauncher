using Avalonia.Controls;
using MCUGLauncher.Contracts;
using Markdown.Avalonia;

namespace MCUGLauncher.Avalonia.Views;

internal partial class AnnouncementWindow : Window
{
    public AnnouncementWindow(AnnouncementInfo announcement)
    {
        InitializeComponent();
        CloseButton.Click += (_, _) => Close();
        Title = announcement.Title.Length > 0 ? announcement.Title : "公告";
        AnnouncementTitle.Text = announcement.Title;
        AnnouncementMeta.Text = string.Join(" · ", new[]
        {
            announcement.Tag,
            announcement.Author,
            announcement.PublishedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? ""
        }.Where(value => value.Length > 0));
        MarkdownHost.Content = new MarkdownScrollViewer
        {
            Markdown = announcement.Markdown
        };
    }
}
