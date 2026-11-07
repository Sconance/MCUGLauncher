using BlockLaunch;

namespace MCUGLauncher.Services;

// Reads installed metadata, never guesses loader versions from the display name.
internal static class InstanceRequirements
{
    public static PackRequirements Read(string root, string key)
    {
        ContentService.SafeFile(key);
        var pack = Path.Combine(root, "instances", key, ".blocklaunch-pack.json");
        if (File.Exists(pack))
        {
            var installed = AtomicFile.ReadJson<InstalledPack>(pack);
            return new(installed.Minecraft, installed.Loader, installed.LoaderVersion);
        }
        return ProfileInspector.Read(root, key);
    }
}
