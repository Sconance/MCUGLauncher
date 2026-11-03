using Avalonia.Controls;

namespace MCUGLauncher.Avalonia.Views;

internal abstract class NativePage : UserControl, IDisposable
{
    protected readonly ILauncherService Session;
    protected readonly UiContext UI;
    protected readonly StackPanel Body = new() { Spacing = 12 };
    protected LauncherState State => Session.State;
    readonly List<Action> detach = [];
    public Control? DialogActions
    {
        get; protected set;
    }
    protected NativePage(ILauncherService session, UiContext ui)
    {
        Session = session;
        UI = ui;
        Content = Body;
        session.StateChanged += StateChanged;
    }
    void StateChanged(LauncherState state) => OnStateChanged();
    protected virtual void OnStateChanged()
    {
    }
    protected void UnsubscribeWith(Action action) => detach.Add(action);
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
    protected Window Owner => (Window)TopLevel.GetTopLevel(this)!;
    public virtual void Dispose()
    {
        Session.StateChanged -= StateChanged;
        foreach (var action in detach)
            action();
        detach.Clear();
    }
}
