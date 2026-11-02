using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace MCUGLauncher.Avalonia.Views;
internal static class Ui
{
    public static TextBlock Text(string text, double size = 13) => new()
    {
        Text = text,
        FontSize = size,
        TextWrapping = TextWrapping.Wrap
    };
    public static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontSize = 23,
        FontWeight = FontWeight.Bold,
        Margin = new(0, 0, 0, 8)
    };
    public static StackPanel Stack(params Control[] controls)
    {
        var panel = new StackPanel
        {
            Spacing = 10
        };
        foreach (var item in controls)
            panel.Children.Add(item);
        return panel;
    }

    public static WrapPanel Row(params Control[] controls)
    {
        var panel = new WrapPanel
        {
            Orientation = Orientation.Horizontal
        };
        foreach (var item in controls)
        {
            item.Margin = new(0, 0, 8, 8);
            panel.Children.Add(item);
        }

        return panel;
    }

    // Use a measured grid for fixed actions beside stretchable content; WrapPanel is only for wrapping toolbars.
    public static Grid Beside(Control content, Control actions, double gap = 10)
    {
        actions.VerticalAlignment = VerticalAlignment.Center;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = gap };
        grid.Children.Add(content);
        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);
        return grid;
    }
    public static TextBlock Muted(string text, double size = 11)
    {
        var block = Text(text, size);
        block.Foreground = Brush.Parse("#5F717D");
        return block;
    }
    public static Border Card(Control child) => new()
    {
        Child = child,
        Padding = new(18),
        CornerRadius = new(12),
        Background = Brushes.White,
        BorderBrush = Brush.Parse("#E3EAF0"),
        BorderThickness = new(1),
        Margin = new(0, 0, 0, 12)
    };
    public static TextBox Input(string value = "", string hint = "") => new()
    {
        Text = value,
        Watermark = hint,
        MinWidth = 160,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    public static CheckBox Check(string label, bool value = false) => new()
    {
        Content = label,
        IsChecked = value
    };
    public static ComboBox Select<T>(IEnumerable<T> items, int index = 0) => new()
    {
        ItemsSource = items.ToArray(),
        SelectedIndex = index,
        MinWidth = 160,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    public static Button Button(string label, Func<Task> action, bool primary = false)
    {
        var button = new Button
        {
            Content = label
        };
        if (primary)
            button.Classes.Add("primary");
        var busy = false;
        button.Click += async (_, _) =>
        {
            if (busy)
                return;
            busy = true;
            button.Classes.Add("busy");
            try
            {
                await action();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                await Error(button, ex.Message);
            }
            finally
            {
                busy = false;
                button.Classes.Remove("busy");
            }
        };
        return button;
    }

    public static Button Action(string label, Action action, bool primary = false) => Button(label, () =>
        {
            action();
            return Task.CompletedTask;
        }, primary);
    public static Control Field(string label, Control input) => Stack(Text(label), input);
    public static Bitmap? Bitmap(string data)
    {
        try
        {
            if (data.StartsWith("data:image/png;base64,"))
                return new Bitmap(new MemoryStream(Convert.FromBase64String(data[(data.IndexOf(',') + 1)..])));
        }
        catch (FormatException)
        {
        }
        catch (ArgumentException)
        {
        }

        return null;
    }

    public static Image Avatar(string data, double size = 80)
    {
        var image = new Image
        {
            Source = Bitmap(data),
            Width = size,
            Height = size
        };
        RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
        return image;
    }

    public static Image Logo(double size = 34) => new()
    {
        Source = new Bitmap(AssetLoader.Open(new Uri("avares://MCUGLauncher/Assets/MCUGLogo.png"))),
        Width = size,
        Height = size
    };
    public static async Task Error(Control control, string text)
    {
        if (TopLevel.GetTopLevel(control) is Window owner)
        {
            var dialog = new Window
            {
                Title = "操作提示",
                Width = 460,
                Height = 230,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            dialog.Content = new Border
            {
                Padding = new(24),
                Child = Stack(Text(text), Action("知道了", () => dialog.Close()))
            };
            await dialog.ShowDialog(owner);
        }
    }

    public static Task<bool> Confirm(Window owner, string title, string text)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 460,
            Height = 250,
            MinWidth = 360,
            MinHeight = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        dialog.Content = new Border
        {
            Padding = new(24),
            Child = Stack(Text(text), Row(Action("取消", () => dialog.Close(false)), Action("确认", () => dialog.Close(true), true)))
        };
        return dialog.ShowDialog<bool>(owner);
    }

    public static async Task Show(Window owner, string title, Control content, double width = 680, double height = 650)
    {
        var dialog = new Window
        {
            Title = title,
            Width = width,
            Height = height,
            MinWidth = Math.Min(440, width),
            MinHeight = Math.Min(340, height),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        dialog.Content = new ScrollViewer
        {
            Content = new Border
            {
                Padding = new(22),
                Child = content
            }
        };
        await dialog.ShowDialog(owner);
        if (content is IDisposable disposable)
            disposable.Dispose();
    }
}
