using Avalonia.Controls;
using MCUGLauncher.Avalonia.Presentation;

namespace MCUGLauncher.Avalonia.Views;
// View lifetime is independent of downloads/games. Only presentation subscriptions are released.
internal abstract class NativePage : UserControl, IDisposable
{
    protected readonly LauncherFacade Session;
    protected readonly StackPanel Body = new()
    {
        Spacing = 12
    };
    protected LauncherState State => Session.State;

    protected NativePage(LauncherFacade session)
    {
        Session = session;
        Content = Body;
        session.MessageReceived += Receive;
    }

    protected virtual void Receive(NativeMessage message)
    {
    }

    protected void Title(string title, string detail = "")
    {
        Body.Children.Add(Ui.Heading(title));
        if (detail.Length > 0)
            Body.Children.Add(Ui.Text(detail));
    }

    protected Task Send(object value) => Session.SendAsync(value);
    protected Window Owner => (Window)TopLevel.GetTopLevel(this)!;

    public virtual void Dispose() => Session.MessageReceived -= Receive;
}
