using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace MCUGLauncher.Avalonia.Views;

// Brief application feedback replaces the persistent footer; no operations live in this view.
internal sealed class NotificationView : Border, IDisposable
{
    readonly TextBlock text = Ui.Text("");
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(8) };
    public NotificationView()
    {
        IsVisible = false;
        Background = global::Avalonia.Media.Brushes.White;
        BorderBrush = global::Avalonia.Media.Brush.Parse("#D3DFE7");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(8);
        Padding = new Thickness(12);
        var close = Ui.Action("×", Hide);
        close.Classes.Add("link");
        global::Avalonia.Automation.AutomationProperties.SetName(close, "关闭提示");
        Child = Ui.Beside(text, close);
        timer.Tick += Expire;
    }
    public void Show(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
        text.Text = message;
        IsVisible = true;
        timer.Stop();
        timer.Start();
    }
    void Hide()
    {
        timer.Stop();
        IsVisible = false;
    }
    void Expire(object? sender, EventArgs args) => Hide();
    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= Expire;
    }
}
