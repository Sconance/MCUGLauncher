using MCUGLauncher;
using System.IO.Compression;

var root = Path.Combine(Path.GetTempPath(), "MCUGLauncher-library-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var instance = Path.Combine(root, "instance");
var original = Path.Combine(instance, "saves", "ExistingWorld");
Directory.CreateDirectory(Path.Combine(original, "region"));
File.WriteAllText(Path.Combine(original, "level.dat"), "level-fixture");
File.WriteAllText(Path.Combine(original, "region", "r.0.0.mca"), "region-fixture");
void Assert(bool condition, string label)
{
    if (!condition)
        throw new Exception(label);
    Console.WriteLine(label + "_OK");
}

Assert(WorldArchives.List(instance).Single().Name == "ExistingWorld", "WORLD_LIST");
var backup = Path.Combine(root, "backup.zip");
await WorldArchives.Backup(original, backup);
using (var zip = ZipFile.OpenRead(backup))
    Assert(zip.GetEntry("level.dat") != null && zip.GetEntry("region/r.0.0.mca") != null && zip.GetEntry("session.lock") == null, "BACKUP_CONTENT_AND_LOCK_EXCLUSION");
var imported = await WorldArchives.Import(instance, backup, "ExistingWorld");
Assert(imported != original && File.ReadAllText(Path.Combine(imported, "region", "r.0.0.mca")) == "region-fixture" && File.ReadAllText(Path.Combine(original, "level.dat")) == "level-fixture", "ROUNDTRIP_PRESERVES_EXISTING_WORLD");
var wrapped = Archive("wrapped", ("MyWorld/level.dat", "wrapped-level"), ("MyWorld/data/test.dat", "data"));
var wrappedWorld = await WorldArchives.Import(instance, wrapped);
Assert(File.Exists(Path.Combine(wrappedWorld, "data", "test.dat")), "SINGLE_WRAPPED_FOLDER_IMPORT");
var count = WorldArchives.List(instance).Count;
foreach (var (label, file) in new[] {
    ("ZIP_TRAVERSAL", Archive("traversal", ("level.dat", "level"), ("../escape.txt", "bad"))),
    ("CASE_COLLISION", Archive("duplicate", ("level.dat", "level"), ("data/A.dat", "a"), ("data/a.dat", "b"))),
    ("NO_LEVEL", Archive("no-level", ("config.txt", "none"))),
    ("MULTIPLE_WORLDS", Archive("multiple", ("A/level.dat", "one"), ("B/level.dat", "two")))
}

)
{
    try
    {
        await WorldArchives.Import(instance, file);
        throw new Exception("Unsafe archive accepted");
    }
    catch (IOException)
    {
    }

    Assert(WorldArchives.List(instance).Count == count && !Directory.EnumerateDirectories(Path.Combine(instance, ".mcuglauncher-world-staging")).Any(), label + "_REJECTED_NO_COMMIT");
}

var symlink = Archive("symlink", ("level.dat", "fixture"), ("link", "../outside"));
using (var zip = ZipFile.Open(symlink, ZipArchiveMode.Update))
    zip.GetEntry("link")!.ExternalAttributes = unchecked((int)(0xA1FF << 16));
try
{
    await WorldArchives.Import(instance, symlink);
    throw new Exception("Symlink accepted");
}
catch (IOException)
{
}

Assert(WorldArchives.List(instance).Count == count, "SYMLINK_ARCHIVE_REJECTED");
var lockedTarget = Path.Combine(root, "locked.zip");
File.WriteAllText(lockedTarget, "old-backup");
using (var session = new FileStream(Path.Combine(original, "session.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
{
    try
    {
        await WorldArchives.Backup(original, lockedTarget);
        throw new Exception("Locked world backed up");
    }
    catch (IOException)
    {
    }

    Assert(File.ReadAllText(lockedTarget) == "old-backup", "ACTIVE_WORLD_REFUSED_AND_PREVIOUS_BACKUP_PRESERVED");
}

using (var cancel = new CancellationTokenSource())
{
    try
    {
        await WorldArchives.Backup(original, lockedTarget, _ => cancel.Cancel(), cancel.Token);
        throw new Exception("Cancel ignored");
    }
    catch (OperationCanceledException)
    {
    }

    Assert(File.ReadAllText(lockedTarget) == "old-backup" && !Directory.EnumerateFiles(root, ".mcuglauncher-world-*.tmp").Any(), "CANCEL_BACKUP_PRESERVES_TARGET");
}

using (var cancel = new CancellationTokenSource())
{
    try
    {
        await WorldArchives.Import(instance, backup, progress: _ => cancel.Cancel(), token: cancel.Token);
        throw new Exception("Cancel ignored");
    }
    catch (OperationCanceledException)
    {
    }

    Assert(WorldArchives.List(instance).Count == count, "CANCEL_IMPORT_NO_LIVE_WORLD");
}

try
{
    await WorldArchives.Backup(original, Path.Combine(original, "recursive.zip"));
    throw new Exception("Recursive backup allowed");
}
catch (IOException)
{
    Console.WriteLine("BACKUP_INSIDE_WORLD_REFUSED_OK");
}

var resource = Archive("resource", ("pack.mcmeta", "{}"), ("assets/test/file", "a"));
var dataPack = Archive("datapack", ("pack.mcmeta", "{}"), ("data/test/file", "d"));
var shader = Archive("shader", ("shaders/main.fsh", "shader"));
var modpack = Archive("modpack", ("modrinth.index.json", "{}"));
Assert(LocalImportClassifier.Identify(backup) == LocalImportKind.World && LocalImportClassifier.Identify(resource) == LocalImportKind.ResourcePack && LocalImportClassifier.Identify(dataPack) == LocalImportKind.DataPack && LocalImportClassifier.Identify(shader) == LocalImportKind.ShaderPack && LocalImportClassifier.Identify(modpack) == LocalImportKind.Modpack && LocalImportClassifier.Identify("example.jar") == LocalImportKind.Mod, "DRAG_IMPORT_CLASSIFICATION");
var shots = Path.Combine(instance, "screenshots");
Directory.CreateDirectory(shots);
File.WriteAllText(Path.Combine(shots, "test.png"), "fixture");
File.WriteAllText(Path.Combine(shots, "ignored.txt"), "text");
Assert(WorldArchives.Screenshots(instance).Single().Name == "test.png", "SCREENSHOT_LIST_FILTER");
Console.WriteLine("ALL_LIBRARY_CHECKS_PASSED");
string Archive(string name, params (string Path, string Content)[] entries)
{
    var file = Path.Combine(root, name + ".zip");
    using var zip = ZipFile.Open(file, ZipArchiveMode.Create);
    foreach (var entry in entries)
    {
        using var writer = new StreamWriter(zip.CreateEntry(entry.Path).Open());
        writer.Write(entry.Content);
    }

    return file;
}
