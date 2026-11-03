using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace MCUGLauncher.Avalonia.Views;

// Shared presentation and window lifetime only; actions remain with their feature views.
internal class DialogWindow : Window
{
    readonly ContentControl body = new() { Name = "DialogBody", HorizontalContentAlignment = HorizontalAlignment.Stretch };
    readonly Border footer = new();
    readonly ScrollViewer scroll = new();
    readonly TextBlock heading = Ui.Text("", 20);
    readonly TextBlock description = Ui.Muted("");
    public DialogWindow()
    {
        SystemDecorations = SystemDecorations.BorderOnly;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = global::Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome;
        ExtendClientAreaTitleBarHeightHint = 68;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        heading.FontWeight = global::Avalonia.Media.FontWeight.SemiBold;
        var close = Ui.Action("×", () => Close());
        close.Classes.Add("window-control");
        close.Classes.Add("close-window");
        global::Avalonia.Automation.AutomationProperties.SetName(close, "关闭弹窗");
        ToolTip.SetTip(close, "关闭");
        var titles = Ui.Stack(heading, description);
        titles.Spacing = 4;
        var header = new Border { Padding = new Thickness(22, 14), Background = global::Avalonia.Media.Brushes.White, Child = Ui.Beside(titles, close) };
        header.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || e.Source is Button
                || (e.Source as Visual)?.GetVisualAncestors().OfType<Button>().Any() == true)
                return;
            BeginMoveDrag(e);
        };
        scroll.Content = body;
        scroll.HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
        // Padding belongs to the scrollable content, so its bottom is part of the extent.
        // ScrollViewer padding otherwise leaves the final controls under the fixed footer.
        body.Padding = new Thickness(22, 18);
        scroll.SizeChanged += (_, _) =>
        {
            var width = Math.Max(0, scroll.Bounds.Width);
            body.MaxWidth = width;
            if (body.Content is Control content)
                content.MaxWidth = Math.Max(0, width - body.Padding.Left - body.Padding.Right);
        };
        footer.Padding = new Thickness(22, 14);
        footer.Background = global::Avalonia.Media.Brushes.White;
        footer.BorderBrush = global::Avalonia.Media.Brush.Parse("#E3EAF0");
        footer.BorderThickness = new Thickness(0, 1, 0, 0);
        footer.Name = "DialogFooter";
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        layout.Children.Add(header);
        Grid.SetRow(scroll, 1);
        layout.Children.Add(scroll);
        Grid.SetRow(footer, 2);
        layout.Children.Add(footer);
        Content = layout;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
    }
    public void SetBody(string title, Control content, string detail = "", bool scrollable = true)
    {
        Title = title;
        heading.Text = title;
        description.Text = detail;
        description.IsVisible = detail.Length > 0;
        body.Content = content;
        scroll.VerticalScrollBarVisibility = scrollable ? global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto : global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
    }
    public void SetActions(Control actions)
    {
        footer.Child = actions;
        actions.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        MinWidth = Math.Max(MinWidth, actions.DesiredSize.Width + 44);
    }
    public static StackPanel Actions(params Control[] actions)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var action in actions)
        {
            action.VerticalAlignment = VerticalAlignment.Center;
            panel.Children.Add(action);
        }
        return panel;
    }
}
