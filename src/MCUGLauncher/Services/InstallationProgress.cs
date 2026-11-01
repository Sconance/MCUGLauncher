using BlockLaunch;

namespace MCUGLauncher.Services;

// Stage-weighted installation progress, separate from the current file's byte progress.
// Files contribute equally within their stage; only successful task completion means 100%.
internal sealed class InstallationProgress(OperationContext task)
{
    readonly object gate = new();
    double percent;
    string message = "正在准备安装…";

    public void Message(string value)
    {
        lock (gate)
        {
            message = value;
            task.Report(message, percent);
        }
    }

    public void Advance(double value)
    {
        lock (gate)
        {
            if (!double.IsFinite(value))
                return;
            percent = Math.Max(percent, Math.Clamp(value, 0, 99));
            task.Report(message, percent);
        }
    }
}
