using MCUGLauncher;
using System.Text.Json;

var testRoot = Path.Combine(Path.GetTempPath(), "MCUGLauncher-uninstall-" + Guid.NewGuid().ToString("N"));
var defaults = new LaunchSettings();
void Check(bool condition, string name)
{
    if (!condition)
        throw new Exception(name);
    Console.WriteLine("PASS " + name);
}

void FileAt(string path, string text = "fixture")
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, text);
}

string Fixture(string name)
{
    var root = Path.Combine(testRoot, name);
    FileAt(Path.Combine(root, "versions", "selected", "selected.json"), "{\"id\":\"selected\"}");
    var data = new InstanceCatalog(root).MetadataDirectory("selected");
    new InstanceCatalog(root).GetOrCreate("selected");
    foreach (var child in new[] {
        "saves/world/level.dat",
        "mods/example.jar",
        "config/example.toml",
        "screenshots/example.png",
        "logs/latest.log"
    }

    )
        FileAt(Path.Combine(data, child));
    FileAt(Path.Combine(root, "libraries", "shared.jar"));
    FileAt(Path.Combine(root, "assets", "shared.asset"));
    return root;
}

void Reject(Action action, string name)
{
    try
    {
        action();
    }
    catch (IOException)
    {
        Console.WriteLine("PASS " + name);
        return;
    }

    throw new Exception("Not rejected: " + name);
}

var root = Fixture("independent");
var catalog = new InstanceCatalog(root);
var data = catalog.DirectoryFor("selected");
FileAt(Path.Combine(root, "instances", "other", "saves", "world", "level.dat"));
var plan = InstanceUninstaller.Plan(root, "selected", defaults);
Check(plan.Paths.Contains(data) && !plan.SharedData, "private saves included in preview");
InstanceUninstaller.Delete(plan, defaults);
Check(!Directory.Exists(data) && !Directory.Exists(Path.Combine(root, "versions", "selected")), "version saves mods config screenshots logs permanently removed");
Check(File.Exists(Path.Combine(root, "libraries", "shared.jar")) && File.Exists(Path.Combine(root, "assets", "shared.asset")), "shared assets and libraries preserved");
Check(File.Exists(Path.Combine(root, "instances", "other", "saves", "world", "level.dat")), "other instance world preserved");
Check(!Directory.Exists(Path.Combine(root, ".mcuglauncher-trash")), "no hidden backup of deleted game data");
root = Fixture("shared");
catalog = new(root);
var descriptor = catalog.GetOrCreate("selected");
descriptor.Launch = new()
{
    Isolation = "共享根目录"
};
catalog.Save(descriptor);
FileAt(Path.Combine(root, "saves", "shared-world", "level.dat"));
plan = InstanceUninstaller.Plan(root, "selected", defaults);
Check(plan.SharedData && !plan.DeleteSharedData && !plan.Paths.Contains(Path.Combine(root, "saves")), "shared worlds excluded by default");
InstanceUninstaller.Delete(plan, defaults);
Check(File.Exists(Path.Combine(root, "saves", "shared-world", "level.dat")), "default uninstall preserves shared world");
root = Fixture("explicit-shared");
catalog = new(root);
descriptor = catalog.GetOrCreate("selected");
descriptor.Launch = new()
{
    Isolation = "共享根目录"
};
catalog.Save(descriptor);
FileAt(Path.Combine(root, "saves", "shared-world", "level.dat"));
FileAt(Path.Combine(root, "options.txt"));
plan = InstanceUninstaller.Plan(root, "selected", defaults, true);
Check(plan.DeleteSharedData && plan.Paths.Contains(Path.Combine(root, "saves")), "explicit shared cleanup preview includes worlds");
InstanceUninstaller.Delete(plan, defaults);
Check(!Directory.Exists(Path.Combine(root, "saves")) && !File.Exists(Path.Combine(root, "options.txt")), "explicit shared cleanup removes listed data");
root = Fixture("dependency");
FileAt(Path.Combine(root, "versions", "child", "child.json"), "{\"inheritsFrom\":\"selected\"}");
Reject(() => InstanceUninstaller.Plan(root, "selected", defaults), "dependent profile blocks uninstall");
Check(Directory.Exists(Path.Combine(root, "instances", "selected", "saves")), "dependency rejection leaves saves untouched");
root = Fixture("jar-dependency");
FileAt(Path.Combine(root, "versions", "child", "child.json"), "{\"jar\":\"selected\"}");
Reject(() => InstanceUninstaller.Plan(root, "selected", defaults), "jar dependency protected");
root = Fixture("external");
catalog = new(root);
descriptor = catalog.GetOrCreate("selected");
descriptor.DataDirectory = testRoot;
catalog.Save(descriptor);
Reject(() => InstanceUninstaller.Plan(root, "selected", defaults), "external broad data path rejected");
root = Fixture("identity");
catalog = new(root);
plan = InstanceUninstaller.Plan(root, "selected", defaults);
descriptor = catalog.GetOrCreate("selected");
descriptor.InstanceId = Guid.NewGuid().ToString("N");
catalog.Save(descriptor);
Reject(() => InstanceUninstaller.Delete(plan, defaults), "changed instance identity requires new confirmation");
root = Fixture("legacy");
catalog = new(root);
descriptor = catalog.GetOrCreate("selected");
descriptor.DataDirectory = Path.Combine(root, "versions", "selected");
catalog.Save(descriptor);
FileAt(Path.Combine(descriptor.DataDirectory, "saves", "old-world", "level.dat"));
InstanceUninstaller.Delete(InstanceUninstaller.Plan(root, "selected", defaults), defaults);
Check(!Directory.Exists(descriptor.DataDirectory), "legacy version-local saves removed");
root = Fixture("shared-private-path");
catalog = new(root);
var other = catalog.GetOrCreate("other");
other.DataDirectory = Path.Combine(catalog.DirectoryFor("selected"), "saves");
catalog.Save(other);
Reject(() => InstanceUninstaller.Plan(root, "selected", defaults), "another instance referencing private directory protected");
Console.WriteLine("ALL_UNINSTALL_CHECKS_PASSED");
