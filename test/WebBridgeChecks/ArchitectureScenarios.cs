using MCUGLauncher;
using MCUGLauncher.Models;
using MCUGLauncher.Services;

internal static class ArchitectureScenarios
{
    static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }

    public static void Run(string fixture)
    {
        var root = Path.Combine(fixture, "architecture");
        void Profile(string id, object metadata) => AtomicFile.WriteJson(
            Path.Combine(root, "versions", id, id + ".json"), metadata);
        Profile("1.20.1", new
        {
            id = "1.20.1"
        });
        Profile("renamed", new
        {
            id = "renamed",
            inheritsFrom = "1.20.1",
            libraries = new[] { new { name = "net.fabricmc:fabric-loader:0.16.9" } }
        });
        Profile("fabric-name-only", new
        {
            id = "fabric-name-only",
            clientVersion = "1.20.1"
        });
        Profile("quilt-compat", new
        {
            id = "quilt-compat",
            inheritsFrom = "1.20.1",
            libraries = new[] { new { name = "net.fabricmc:fabric-loader:0.16.9" }, new { name = "org.quiltmc:quilt-loader:0.27.0" } }
        });
        Profile("forge-renamed", new
        {
            id = "forge-renamed",
            inheritsFrom = "1.20.1",
            libraries = new[] { new { name = "net.minecraftforge:forge:1.20.1-47.3.0" } }
        });
        foreach (var (key, loader, label) in new[]
        {
            ("renamed", "fabric", "Fabric"), ("fabric-name-only", "vanilla", "原版"),
            ("quilt-compat", "quilt", "Quilt"), ("forge-renamed", "forge", "Forge")
        })
        {
            var expected = ProfileInspector.Read(root, key);
            var actual = InstanceRequirements.Read(root, key);
            var displayed = VersionCatalog.ReadDetails(root, key);
            Check(actual == expected && actual.Loader == loader && displayed.Loader == label && displayed.Minecraft == actual.Minecraft,
                "catalog and launch share Core profile interpretation: " + key);
        }
        Check(InstanceRequirements.Read(root, "forge-renamed").LoaderVersion == "47.3.0", "Core strips Forge Minecraft prefix once");
        Profile("cycle-a", new
        {
            id = "cycle-a",
            inheritsFrom = "cycle-b"
        });
        Profile("cycle-b", new
        {
            id = "cycle-b",
            inheritsFrom = "cycle-a"
        });
        Check(!VersionCatalog.ReadDetails(root, "cycle-a").Complete, "invalid inherited profile remains visible as incomplete");
        try
        {
            InstanceRequirements.Read(root, "cycle-a");
            throw new Exception("cycle accepted");
        }
        catch (IOException) { Check(true, "launch rejects inheritance cycle using Core validation"); }

        AtomicFile.WriteJson(Path.Combine(root, "instances", "pack", ".mcuglauncher-pack.json"),
            new InstalledPack("Fixture pack", "1.20.1", "fabric", "0.16.9", "renamed", "pack"));
        Check(InstanceRequirements.Read(root, "pack").Loader == "fabric", "pack receipt remains authoritative without profile reinterpretation");

        var config = new AppConfig { Root = root, Ram = 2048, Width = 1024, Java = "manual-java" };
        config.LegacyPreferences.Launch.AutoChinese = false;
        config.LegacyPreferences.Launch.WindowTitle = "Original";
        var defaults = InstanceSnapshot.GlobalSettings(config);
        Check(defaults.RamMb == 2048 && defaults.Width == 1024 && defaults.Java == "manual-java" && !defaults.AutoChinese,
            "global options merge preserves application overrides and Core preferences");
        var descriptor = new InstanceDescriptor { Launch = defaults.Copy() };
        var first = InstanceSnapshot.SettingsFor(config, descriptor);
        first.WindowTitle = "Edited";
        first.AutoChinese = true;
        Check(descriptor.Launch.WindowTitle == "Original" && !descriptor.Launch.AutoChinese,
            "editing instance settings cannot mutate persisted source");
        var snapshot = InstanceSnapshot.Capture(root, "renamed", defaults);
        defaults.WindowTitle = "Later change";
        Check(snapshot.Settings.WindowTitle == "Original", "captured launch options remain isolated from later edits");
        var modsDirectory = Path.Combine(snapshot.Directory, "mods");
        Directory.CreateDirectory(modsDirectory);
        var modPath = Path.Combine(modsDirectory, "example.jar");
        File.WriteAllText(modPath, "fixture bytes");
        ModInfo Mod() => VersionCatalog.ReadMods(root, "renamed").Single();
        VersionCatalog.SetEnabled(Mod(), false);
        VersionCatalog.SetEnabled(Mod(), false);
        Check(File.Exists(modPath + ".disabled"), "explicit disable is idempotent through Core content operations");
        VersionCatalog.SetEnabled(Mod(), true);
        var configStore = new ConfigStore(Path.Combine(fixture, "architecture-config"));
        configStore.Save(new AppConfig { Root = root, LastInstance = "renamed" });
        using (var context = new MCUGLauncher.UseCases.LauncherContext(_ => { }, configStore))
        {
            context.MarkPreparing(root, snapshot.Directory);
            try
            {
                new MCUGLauncher.UseCases.InstanceCommands(context).DeleteMod("example.jar");
                throw new Exception("preparing instance modified");
            }
            catch (IOException) { Check(File.Exists(modPath), "mod mutation refuses an instance preparing to launch"); }
            context.ReleasePreparing(snapshot.Directory);
            new MCUGLauncher.UseCases.InstanceCommands(context).DeleteMod("example.jar");
        }
        Check(!File.Exists(modPath) && Directory.GetFiles(Path.Combine(snapshot.Directory, ".mcuglauncher", "backups"), "example.jar", SearchOption.AllDirectories).Length == 1,
            "legacy remove command now preserves Core content backup");
        var dependencyPath = Path.Combine(modsDirectory, "dependency.jar");
        File.WriteAllText(dependencyPath, "required fixture");
        File.WriteAllText(Path.Combine(modsDirectory, "consumer.jar"), "consumer fixture");
        AtomicFile.WriteJson(Path.Combine(snapshot.Directory, ".mcuglauncher-content.json"), new[]
        {
            new ManagedContent { ProjectId = "dependency", VersionId = "dep-v1", RelativePath = "mods/dependency.jar" },
            new ManagedContent
            {
                ProjectId = "consumer", VersionId = "consumer-v1", RelativePath = "mods/consumer.jar",
                Dependencies = [new ContentDependency { ProjectId = "dependency", DependencyType = "required" }]
            }
        });
        var dependency = VersionCatalog.ReadMods(root, "renamed").Single(item => item.File == "dependency.jar");
        foreach (var remove in new[] { false, true })
        {
            try
            {
                if (remove)
                    VersionCatalog.Delete(dependency);
                else
                    VersionCatalog.SetEnabled(dependency, false);
                throw new Exception("required dependency modified");
            }
            catch (IOException) { Check(File.Exists(dependencyPath), "legacy mod adapter preserves required dependency: remove=" + remove); }
        }
        var copied = snapshot.Settings.Copy();
        Check(typeof(LaunchSettings).GetProperties().All(property =>
            (property.PropertyType.IsValueType || property.PropertyType == typeof(string)) &&
            Equals(property.GetValue(copied), property.GetValue(snapshot.Settings))),
            "copy preserves every option and detects future mutable members requiring deep copy");
    }
}
