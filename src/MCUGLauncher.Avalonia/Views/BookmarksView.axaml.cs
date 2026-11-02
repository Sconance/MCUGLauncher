using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MCUGLauncher.Avalonia.Views;

internal partial class BookmarksView : UserControl
{
    LauncherFacade? facade;
    LauncherState? state;

    public BookmarksView()
    {
        InitializeComponent();
    }

    public BookmarksView(LauncherFacade facade) : this()
    {
        Attach(facade);
    }

    public void Attach(LauncherFacade value)
    {
        facade = value;
        facade.StateChanged += ApplyState;
        facade.ToastReceived += info => Status(info.Message);
        facade.ErrorReceived += message => Status("操作失败：" + message);
        ApplyState(facade.State);
    }

    void ApplyState(LauncherState? snapshot)
    {
        state = snapshot;
        if (snapshot is null)
            return;
        FavoritesPanel.Children.Clear();
        if (snapshot.Favorites.Count == 0)
        {
            FavoritesPanel.Children.Add(Muted("没有收藏的资源。在下载页搜索并收藏后会出现在这里。"));
            return;
        }
        foreach (var favorite in snapshot.Favorites)
        {
            var copy = new StackPanel { Spacing = 2 };
            copy.Children.Add(new TextBlock { Text = favorite.Name, FontWeight = FontWeight.SemiBold });
            copy.Children.Add(new TextBlock
            {
                Text = KindLabel(favorite.Kind) + " · 项目 ID " + favorite.Id,
                Classes = { "muted" }
            });
            if (favorite.Description.Length > 0)
                copy.Children.Add(new TextBlock
                {
                    Text = favorite.Description,
                    TextWrapping = TextWrapping.Wrap,
                    Classes = { "muted" }
                });
            var remove = new Button { Content = "取消收藏", Classes = { "ghost" } };
            var id = favorite.Id;
            var name = favorite.Name;
            var description = favorite.Description;
            var kind = favorite.Kind;
            remove.Click += async (_, _) =>
            {
                await facade!.ToggleBookmarkAsync(id, name, description, "", kind);
                Status("已取消收藏：" + name);
            };
            FavoritesPanel.Children.Add(new Border
            {
                BorderBrush = new SolidColorBrush(Color.Parse("#E3EAF0")),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 6),
                Child = Row(copy, remove)
            });
        }
    }

    void Status(string message) => StatusText.Text = message;

    static string KindLabel(string kind) => kind switch
    {
        "mod" => "模组",
        "modpack" => "整合包",
        "resourcepack" => "资源包",
        "shader" => "光影",
        "datapack" => "数据包",
        _ => kind
    };

    static Control Row(Control copy, Control action)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        row.Children.Add(copy);
        Grid.SetColumn(action, 1);
        action.Margin = new Thickness(12, 0, 0, 0);
        action.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(action);
        return row;
    }

    static TextBlock Muted(string text) => new() { Text = text, Classes = { "muted" } };
}
