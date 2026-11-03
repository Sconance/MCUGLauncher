namespace MCUGLauncher;

// Stage weights describe work completed, not elapsed time. Only the queue commits 100%.
public static class InstallationStages
{
    public const double BeforeCommit = 99;
    public const double BaseGameFiles = 65;
    public const double LoaderDownloaded = 70;
    public const double OfficialInstallerStarted = 75;
    public const double LoaderFinalFiles = 90;
    public const double PackFiles = 40;
    public const double ContentFiles = 95;
}
