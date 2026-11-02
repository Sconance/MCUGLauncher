using MCUGLauncher;
using CmlLib.Core.Auth;
using System.Diagnostics;

if (args.Contains("-version"))
{
    if (Environment.GetEnvironmentVariable("MCUGLAUNCHER_PROBE_FIXTURE") == "hang")
        await Task.Delay(30000);
    Console.Error.WriteLine("java version \"17.0.1\"");
    return;
}

if (args.Contains("--emit"))
{
    Console.WriteLine("fixture-stdout");
    Console.Error.WriteLine("fixture-stderr");
    return;
}

var root = Path.Combine(Path.GetTempPath(), "MCUGLauncher 兼容与恢复 " + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
void Check(bool value, string name)
{
    if (!value)
        throw new Exception(name);
    Console.WriteLine("PASS " + name);
}

Check(await JavaDiscovery.ProbeMajor(Environment.ProcessPath!, CancellationToken.None) == 17, "manual Java probe drains stderr and parses version");
var inspection = await JavaDiscovery.InspectCandidates([Environment.ProcessPath!, Environment.ProcessPath!]);
Check(inspection.Count == 1 && inspection[0].Major == 17 && inspection[0].Error.Length == 0, "Java candidate validation deduplicates and probes actual process");
var discovery = Path.Combine(root, "java-search");
Directory.CreateDirectory(Path.Combine(discovery, "中文 空格", "bin"));
File.WriteAllText(Path.Combine(discovery, "中文 空格", "bin", "java.exe"), "passive fixture");
File.WriteAllText(Path.Combine(discovery, "中文 空格", "bin", "javaw.exe"), "passive fixture");
var found = JavaDiscovery.SearchDirectories([discovery]);
Check(found.Paths.Count == 1 && found.Paths[0].EndsWith("java.exe") && !found.LimitReached, "recursive search finds nested Unicode path without duplicate console/windowed runtime");
Check(JavaDiscovery.SearchDirectories([discovery], maxDirectories: 1).LimitReached, "bounded search explicitly reports incomplete traversal");
using (var stopped = new CancellationTokenSource())
{
    stopped.Cancel();
    try
    {
        JavaDiscovery.SearchDirectories([discovery], stopped.Token);
        throw new Exception("Search ignored cancellation");
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("PASS full Java search cancels before traversal");
    }
}

Check(JavaDiscovery.SearchDirectories([Path.Combine(root, "missing")]).Paths.Count == 0, "missing Java search roots are skipped");
AtomicFile.WriteJson(Path.Combine(root, "versions", "1.20.1", "1.20.1.json"), new { id = "1.20.1", javaVersion = new { majorVersion = 17 } });
foreach (var fixture in new[] {
    ("neo-old", "net.neoforged:forge:1.20.1-47.1.106", "neoforge", "47.1.106"),
    ("neo-modern", "net.neoforged:neoforge:21.1.80", "neoforge", "21.1.80"),
    ("fabric-custom", "net.fabricmc:fabric-loader:0.16.10", "fabric", "0.16.10"),
    ("forge-custom", "net.minecraftforge:forge:1.20.1-47.2.0", "forge", "47.2.0")
}

)
{
    AtomicFile.WriteJson(Path.Combine(root, "versions", fixture.Item1, fixture.Item1 + ".json"), new
    {
        id = fixture.Item1,
        inheritsFrom = "1.20.1",
        libraries = new[] { new { name = fixture.Item2 } }
    });
    var metadata = ProfileInspector.Read(root, fixture.Item1);
    Check(metadata.Minecraft == "1.20.1" && metadata.Loader == fixture.Item3 && metadata.LoaderVersion == fixture.Item4, "renamed metadata loader recognition " + fixture.Item1);
}

AtomicFile.WriteJson(Path.Combine(root, "versions", "neo-args", "neo-args.json"), new { id = "neo-args", arguments = new { game = new[] { "--fml.neoForgeVersion", "21.1.80", "--fml.mcVersion", "1.21.1" } } });
Check(ProfileInspector.Read(root, "neo-args") == new PackRequirements("1.21.1", "neoforge", "21.1.80"), "standalone NeoForge arguments reveal Minecraft and loader versions");
Check(JavaDiscovery.RequiredMajor(root, "neo-args") == 21, "standalone renamed profile derives legacy Java requirement from Minecraft metadata");
AtomicFile.WriteJson(Path.Combine(root, "versions", "quilt-custom", "quilt-custom.json"), new { id = "quilt-custom", inheritsFrom = "1.20.1", libraries = new[] { new { name = "net.fabricmc:fabric-loader:0.16.0" }, new { name = "org.quiltmc:quilt-loader:0.26.0" } } });
Check(ProfileInspector.Read(root, "quilt-custom").Loader == "quilt", "Quilt compatibility Fabric library does not mislabel instance");
AtomicFile.WriteJson(Path.Combine(root, "versions", "cycle", "cycle.json"), new { id = "cycle", inheritsFrom = "cycle" });
try
{
    ProfileInspector.Read(root, "cycle");
    throw new Exception("Cycle accepted");
}
catch (IOException)
{
    Console.WriteLine("PASS profile metadata inheritance cycle rejected");
}

var legacy = System.Text.Json.JsonSerializer.Deserialize<Preferences>("{\"SchemaVersion\":2,\"BackgroundPath\":\"old.png\"}")!;
Check(legacy.BackgroundPath == "old.png" && legacy.BackgroundOpacity == 0.22 && legacy.BackgroundFit == "裁剪填充" && legacy.BackgroundImages.Count == 0, "old background configuration receives compatible defaults");
var packFile = Path.Combine(root, "preview.mrpack");
using (var zip = System.IO.Compression.ZipFile.Open(packFile, System.IO.Compression.ZipArchiveMode.Create))
{
    using var writer = new StreamWriter(zip.CreateEntry("modrinth.index.json").Open());
    writer.Write("{\"formatVersion\":1,\"game\":\"minecraft\",\"name\":\"预检整合包\",\"dependencies\":{\"minecraft\":\"1.21.1\",\"neoforge\":\"21.1.80\"},\"files\":[]}");
}

var overview = PackService.ReadOverview(packFile);
Check(overview.Name == "预检整合包" && overview.Format == "Modrinth" && overview.Requirements == new PackRequirements("1.21.1", "neoforge", "21.1.80"), "Modrinth pack preflight displays exact Minecraft and NeoForge requirements");
var mmcFile = Path.Combine(root, "preview.zip");
using (var zip = System.IO.Compression.ZipFile.Open(mmcFile, System.IO.Compression.ZipArchiveMode.Create))
{
    using var writer = new StreamWriter(zip.CreateEntry("nested/mmc-pack.json").Open());
    writer.Write("{\"formatVersion\":1,\"components\":[{\"uid\":\"net.minecraft\",\"version\":\"1.20.1\"},{\"uid\":\"net.fabricmc.fabric-loader\",\"version\":\"0.16.10\"}]}");
}

Check(PackService.ReadOverview(mmcFile).Requirements == new PackRequirements("1.20.1", "fabric", "0.16.10"), "nested MultiMC preflight shares importer component parser");
var unknownFile = Path.Combine(root, "unknown.zip");
using (var zip = System.IO.Compression.ZipFile.Open(unknownFile, System.IO.Compression.ZipArchiveMode.Create))
{
    zip.CreateEntry("readme.txt");
}

try
{
    PackService.ReadOverview(unknownFile);
    throw new Exception("Unknown pack accepted");
}
catch (IOException)
{
    Console.WriteLine("PASS unsupported archive fails before queueing or instance creation");
}

AtomicFile.WriteJson(Path.Combine(root, "versions", "fixture", "fixture.json"), new { id = "fixture", javaVersion = new { majorVersion = 21 } });
try
{
    await LaunchService.Build(new(root, root, "fixture", Environment.ProcessPath!, 1024, 800, 600, MSession.CreateOfflineSession("Fixture")), CancellationToken.None);
    throw new Exception("Wrong Java accepted");
}
catch (IOException ex)
{
    Check(ex.Message.Contains("Java 21") && ex.Message.Contains("Java 17"), "wrong manual Java blocked before game launch with useful message");
}

Environment.SetEnvironmentVariable("MCUGLAUNCHER_PROBE_FIXTURE", "hang");
try
{
    using var cancellation = new CancellationTokenSource(200);
    try
    {
        await JavaDiscovery.ProbeMajor(Environment.ProcessPath!, cancellation.Token);
        throw new Exception("Probe cancellation ignored");
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("PASS Java probe cancellation terminates child");
    }
}
finally
{
    Environment.SetEnvironmentVariable("MCUGLAUNCHER_PROBE_FIXTURE", null);
}

try
{
    await JavaDiscovery.ProbeMajor(Path.Combine(root, "missing.exe"), CancellationToken.None);
    throw new Exception("Missing Java accepted");
}
catch (IOException)
{
    Console.WriteLine("PASS invalid manual Java gives readable error");
}

using var http = new HttpClient();
foreach (var request in new[] {
    new PackRequirements("1.21.1", "unknown", "1"),
    new PackRequirements("", "vanilla", ""),
    new PackRequirements("1.21.1", "fabric", "")
}

)
{
    try
    {
        await new GameInstaller(http, Path.Combine(root, "invalid"), "").Install(request);
        throw new Exception("Invalid install accepted");
    }
    catch (IOException)
    {
    }
}

Check(!Directory.Exists(Path.Combine(root, "invalid")), "invalid loader requests rejected before download and filesystem changes");
try
{
    await VersionInstallation.Run(root, "partial-loader", _ =>
    {
        Check(!VersionInstallation.IsReady(root, "partial-loader"), "install marked unready before publishing profile");
        AtomicFile.WriteJson(Path.Combine(root, "versions", "partial-loader", "partial-loader.json"), new
        {
            id = "partial-loader"
        });
        throw new IOException("simulated interrupted library download");
    }, CancellationToken.None);
}
catch (IOException)
{
}

Check(!VersionInstallation.IsReady(root, "partial-loader"), "partial loader profile remains unready after failure");
await VersionInstallation.Run(root, "partial-loader", _ => Task.CompletedTask, CancellationToken.None);
Check(VersionInstallation.IsReady(root, "partial-loader"), "retry commits loader readiness");
var output = Path.Combine(root, "log-is-a-directory");
Directory.CreateDirectory(output);
using var process = new Process
{
    StartInfo = new ProcessStartInfo(Environment.ProcessPath!)
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    }
};
process.StartInfo.ArgumentList.Add("--emit");
var lines = new System.Collections.Concurrent.ConcurrentQueue<string>();
var session = new GameSession(process, output, lines.Enqueue, []);
session.Start();
var exit = await session.Completion.WaitAsync(TimeSpan.FromSeconds(10));
Check(exit.Code == 0 && lines.Any(l => l.Contains("fixture-stdout")) && lines.Any(l => l.Contains("fixture-stderr")), "unwritable log path keeps both output streams draining and exit observation alive");
Console.WriteLine("ALL_COMPATIBILITY_CHECKS_PASSED");
if (args.Contains("--live-java"))
{
    var live = await JavaDiscovery.Scan(root, Array.Empty<string>());
    var valid = live.FirstOrDefault(j => j.Error.Length == 0);
    Check(valid != null, "real machine automatic scan discovers an installed Java runtime");
    AtomicFile.WriteJson(Path.Combine(root, "versions", "live-java", "live-java.json"), new
    {
        id = "live-java",
        javaVersion = new
        {
            majorVersion = valid!.Major
        }
    });
    var selected = await JavaDiscovery.FindCompatible(root, "live-java", CancellationToken.None);
    Check(selected != null && await JavaDiscovery.ProbeMajor(selected, CancellationToken.None) == valid.Major, "real automatic Java selection matches declared game major");
    Console.WriteLine("LIVE_JAVA_MAJORS " + string.Join(",", live.Where(j => j.Error.Length == 0).Select(j => j.Major).Distinct().Order()));
}
