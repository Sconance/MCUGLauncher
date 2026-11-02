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
    public Control? DialogActions
    {
        get; protected set;
    }

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
        var heading = Ui.Heading(title);
        heading.Classes.Add("page-heading");
        Body.Children.Add(heading);
        if (detail.Length > 0)
            Body.Children.Add(Ui.Text(detail));
    }

    internal void PrepareDialog()
    {
        var heading = Body.Children.FirstOrDefault(c => c.Classes.Contains("page-heading"));
        if (heading != null)
            Body.Children.Remove(heading);
    }

    protected Task Send(object value) => Session.SendAsync(value);
    protected Window Owner => (Window)TopLevel.GetTopLevel(this)!;

    public virtual void Dispose() => Session.MessageReceived -= Receive;
}
