using Avalonia.Controls;
using Avalonia.Layout;

namespace MCUGLauncher.Avalonia.Views;

public partial class HomeView : UserControl
{
    public event Action<string>? LaunchRequested;

    public HomeView()
    {
        InitializeComponent();
    }

    internal void Render(LauncherState? snapshot)
    {
        Account.Text = snapshot?.AccountLabel is { Length: > 0 } label ? label : "离线账号";
        Player.Text = "玩家：" + (snapshot?.Player ?? "");
        Version.Text = "当前版本：" + (snapshot?.SelectedVersion ?? "");
        Instances.Children.Clear();
        if (snapshot is not { Instances.Count: > 0 })
        {
            Instances.Children.Add(new TextBlock { Text = "尚未找到已安装实例。" });
            return;
        }
        foreach (var instance in snapshot.Instances)
        {
            if (instance.Name.Length == 0)
                continue;
            var button = new Button
            {
                Content = "启动  " + instance.Name + (instance.Loader.Length > 0 ? "  ·  " + instance.Loader : ""),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            button.Classes.Add("primary");
            button.Click += (_, _) => LaunchRequested?.Invoke(instance.Name);
            Instances.Children.Add(button);
        }
    }
}
