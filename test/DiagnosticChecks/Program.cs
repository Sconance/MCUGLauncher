using MCUGLauncher;
using System.IO.Compression;
using System.Text;
using System.Diagnostics;

if (args.Contains("--crash-fixture"))
{
    Console.WriteLine("java.lang.OutOfMemoryError: Synthetic failure, not Minecraft");
    Console.Error.WriteLine("access_token=child-process-fixture-secret");
    Console.Error.WriteLine("Authorization: Bearer unknown-child-bearer-value");
    Environment.ExitCode = 37;
    return;
}

var root = Path.Combine(Path.GetTempPath(), "MCUGLauncher-diagnostics-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(root, "logs"));
Directory.CreateDirectory(Path.Combine(root, "crash-reports"));
Directory.CreateDirectory(Path.Combine(root, "mods"));
void Check(bool ok, string name)
{
    if (!ok)
        throw new Exception(name);
    Console.WriteLine(name + "_OK");
}

try
{
    var latest = Path.Combine(root, "logs", "latest.log");
    var started = DateTime.UtcNow.AddSeconds(-1);
    File.WriteAllText(latest, "java.lang.OutOfMemoryError: Java heap space\nAuthorization: Bearer secret-bearer-value\naccess_token=secret-token-value\n--username PrivateName --uuid private-uuid\ncontact=private@example.com\nC:\\Users\\PrivateUser\\Desktop\\game\n127.0.0.1:25565\ncustom-known-secret\n");
    File.WriteAllText(Path.Combine(root, "crash-reports", "crash-old.txt"), "UnsupportedClassVersionError");
    File.SetLastWriteTimeUtc(Path.Combine(root, "crash-reports", "crash-old.txt"), DateTime.UtcNow.AddDays(-1));
    File.WriteAllText(Path.Combine(root, "mods", "example.jar"), "do not include jar body");
    File.WriteAllText(Path.Combine(root, "mods", "disabled.jar.disabled"), "hidden mod body");
    File.WriteAllText(Path.Combine(root, "options.txt"), "secret config not allowed");
    File.WriteAllText(Path.Combine(root, "microsoft-account.dat"), "secret account not allowed");
    var snapshot = Diagnostics.Capture(new(root, "1.21.1", StartedUtc: started, Exit: new(1, false), Secrets: ["custom-known-secret"]));
    Check(snapshot.Findings.Count == 1 && snapshot.Findings[0].Title.Contains("内存") && snapshot.Findings[0].Evidence.Contains("OutOfMemoryError"), "OOM_HAS_EVIDENCE_AND_ADVICE");
    Check(snapshot.Logs.Count == 1 && snapshot.Notices.Any(n => n.Contains("早于本次")), "STALE_CRASH_NOT_ATTRIBUTED_TO_NEW_SESSION");
    var historic = Diagnostics.Capture(new(root, "1.21.1"));
    Check(historic.Findings.Count == 2 && historic.Summary.Contains("较早"), "MANUAL_ANALYSIS_LABELS_HISTORIC_LOGS");
    File.Delete(Path.Combine(root, "crash-reports", "crash-old.txt"));
    var texts = string.Join('\n', Diagnostics.Preview(snapshot, new()).Select(d => d.Text));
    Check(!new[] { "secret-bearer-value", "secret-token-value", "private@example.com", "PrivateUser", "private-uuid", "PrivateName", "127.0.0.1", "custom-known-secret", "secret config not allowed", "secret account not allowed", "do not include jar body" }.Any(texts.Contains), "FEEDBACK_REDACTS_AND_EXCLUDES_PRIVATE_DATA");
    Check(snapshot.Mods.Contains("example.jar") && snapshot.Mods.Contains("disabled.jar.disabled"), "MOD_LIST_HAS_ENABLED_AND_DISABLED_NAMES");
    var withoutLogs = Diagnostics.Preview(snapshot, new(false, false, false));
    Check(withoutLogs.Count == 2 && !string.Join('\n', withoutLogs.Select(d => d.Text)).Contains("OutOfMemoryError"), "OPT_OUT_REMOVES_LOGS_EVIDENCE_MODS_ENVIRONMENT");
    var target = Path.Combine(root, "feedback.zip");
    var preview = Diagnostics.Preview(snapshot, new());
    await Diagnostics.Export(preview, target);
    using (var zip = ZipFile.OpenRead(target))
    {
        Check(zip.Entries.Count == preview.Count, "ZIP_FILE_COUNT_MATCHES_PREVIEW");
        foreach (var document in preview)
        {
            using var reader = new StreamReader(zip.GetEntry(document.Name)!.Open());
            Check(reader.ReadToEnd() == document.Text, "ZIP_EXACT_PREVIEW_" + document.Name.Replace('/', '_'));
        }
    }

    File.WriteAllText(latest, "ERROR random irrelevant message\n");
    var healthy = Diagnostics.Capture(new(root, "1.21.1", Exit: new(0, false)));
    Check(healthy.Findings.Count == 0 && healthy.Summary.Contains("正常退出"), "ERROR_ALONE_IS_NOT_MOD_CONFLICT");
    File.WriteAllText(latest, "OutOfMemoryError");
    Check(Diagnostics.Capture(new(root, "1.21.1", Exit: new(-1, true))).Findings.Count == 0, "REQUESTED_STOP_NOT_AUTOMATIC_CRASH");
    var rules = new[] {
        "UnsupportedClassVersionError",
        "Incompatible mods found!",
        "MixinApplyError",
        "GLFW error 65542",
        "java.util.zip.ZipException: zip END header not found",
        "No space left on device",
        "EXCEPTION_ACCESS_VIOLATION"
    };
    foreach (var rule in rules)
        Check(Diagnostics.Analyze([new("fixture.log", rule)]).Count == 1, "RULE_" + rule.Split(' ')[0]);
    File.WriteAllText(latest, new string('x', 2 * 1024 * 1024) + "\nOutOfMemoryError\n");
    var bounded = Diagnostics.Capture(new(root, "1.21.1"));
    Check(bounded.Logs.All(l => Encoding.UTF8.GetByteCount(l.Text) <= 1024 * 1024) && bounded.Findings.Count == 1 && bounded.Notices.Any(n => n.Contains("1 MiB")), "LARGE_LOG_TAIL_BOUNDED_WITH_RECENT_EVIDENCE");
    var oldZip = File.ReadAllBytes(target);
    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    try
    {
        await Diagnostics.Export(preview, target, cancelled.Token);
        throw new Exception("Export should cancel");
    }
    catch (OperationCanceledException)
    {
    }

    Check(File.ReadAllBytes(target).SequenceEqual(oldZip) && !Directory.GetFiles(root, "*.tmp").Any(), "CANCEL_PRESERVES_EXISTING_TARGET_AND_CLEANS_TEMP");
    try
    {
        await Diagnostics.Export([new("../escape.txt", "x")], target);
        throw new Exception("Unsafe path accepted");
    }
    catch (IOException)
    {
    }

    Check(File.ReadAllBytes(target).SequenceEqual(oldZip), "INVALID_ARCHIVE_ENTRY_REJECTED");
    var jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJzZWNyZXQifQ.signature";
    Check(!Diagnostics.Sanitize("Authorization: Bearer " + jwt).Contains("signature") && !Diagnostics.Sanitize("https://user:pass@example.com/file").Contains("user:pass"), "JWT_AND_URL_CREDENTIALS_REDACTED");
    Check(!Diagnostics.Sanitize("password=\"two private words\" refresh_token='a private token'").Contains("private"), "QUOTED_CREDENTIALS_WITH_SPACES_REDACTED");
    var outside = Path.Combine(root, "outside.log");
    File.WriteAllText(outside, "outside-secret");
    File.Delete(latest);
    try
    {
        File.CreateSymbolicLink(latest, outside);
        var linked = Diagnostics.Capture(new(root, "1.21.1"));
        Check(linked.Logs.Count == 0 && linked.Notices.Any(n => n.Contains("链接")), "SYMLINK_LOG_NOT_READ");
    }
    catch (UnauthorizedAccessException)
    {
        Console.WriteLine("SYMLINK_TEST_SKIPPED_OS_PERMISSION");
    }

    var childRoot = Path.Combine(root, "child-instance");
    Directory.CreateDirectory(childRoot);
    var processLog = Path.Combine(childRoot, "process-output.log");
    var child = new Process
    {
        StartInfo = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }
    };
    if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        child.StartInfo.ArgumentList.Add(typeof(Diagnostics).Assembly.Location.Replace("MCUGLauncher.Core.dll", "DiagnosticChecks.dll"));
    child.StartInfo.ArgumentList.Add("--crash-fixture");
    var childStarted = DateTime.UtcNow;
    var session = new GameSession(child, processLog, _ =>
    {
    }, ["child-process-fixture-secret"]);
    session.Start();
    var exit = await session.Completion.WaitAsync(TimeSpan.FromSeconds(15));
    var captured = Diagnostics.Capture(new(childRoot, "fixed-launch-profile", processLog, childStarted, exit, ["child-process-fixture-secret"]));
    Check(exit.Code == 37 && !exit.Requested && captured.Findings.Count == 1 && captured.Findings[0].Source == "process.log" && captured.Environment.Contains("fixed-launch-profile"), "REAL_CHILD_EXIT_AND_CAPTURED_STDERR_PIPELINE");
    Check(!captured.Logs.Any(l => l.Text.Contains("child-process-fixture-secret") || l.Text.Contains("unknown-child-bearer-value")), "CHILD_PROCESS_SECRET_NOT_IN_FEEDBACK");
    Console.WriteLine("ALL_DIAGNOSTIC_CHECKS_PASSED");
}
finally
{
    Directory.Delete(root, true);
}
