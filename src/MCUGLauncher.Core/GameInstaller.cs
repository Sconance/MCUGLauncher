using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;

namespace BlockLaunch;

/// <summary>
/// 游戏与加载器安装器。
/// </summary>
/// <remarks>
/// 依赖 HttpClient、游戏根目录、Java 路径与 NetworkSettings；通过 progress 回调报告下载/解压进度，失败时抛出异常并保留可恢复状态。
/// </remarks>
public sealed class GameInstaller(HttpClient http, string root, string java, Action<string>? progress = null, CancellationToken token = default, NetworkSettings? network = null, Action<double>? overallProgress = null)
{
    /// <summary>
    /// 安装。
    /// </summary>
    /// <param name="request">要安装的版本与加载器要求</param>
    /// <returns>安装完成后的版本目录</returns>
    public async Task<string> Install(PackRequirements request)
    {
        token.ThrowIfCancellationRequested();
        if (request.Loader is not ("vanilla" or "fabric" or "quilt" or "forge" or "neoforge"))
            throw new IOException("不支持的加载器。");
        if (string.IsNullOrWhiteSpace(request.Minecraft) || request.Loader != "vanilla" && string.IsNullOrWhiteSpace(request.LoaderVersion))
            throw new IOException("游戏或加载器版本不能为空。");
        if (request.Loader == "neoforge" && request.Minecraft is "1.20" or "1.20.1")
            throw new IOException("此早期 NeoForge 使用不同安装格式，暂不支持自动安装；1.20.1 建议使用 Forge，或手动导入官方安装器。");
        foreach (var componentVersion in new[] {
            request.Minecraft,
            request.LoaderVersion
        }.Where(v => v.Length > 0))
            if (!System.Text.RegularExpressions.Regex.IsMatch(componentVersion, @"^[A-Za-z0-9_.+\-]+$") || componentVersion.Contains(".."))
                throw new IOException("无效的加载器或游戏版本号。");
        MinecraftLauncher CreateLauncher(string profile)
        {
            var path = new MinecraftPath(root);
            var parameters = MinecraftLauncherParameters.CreateDefault(path, http);
            if (File.Exists(Path.Combine(root, "versions", profile, profile + ".json")))
                parameters.VersionLoader = new CmlLib.Core.VersionLoader.LocalJsonVersionLoader(path);
            return new MinecraftLauncher(parameters);
        }

        var baseWeight = request.Loader == "vanilla" ? InstallationStages.BeforeCommit : InstallationStages.BaseGameFiles;
        void Track(MinecraftLauncher current, double start, double weight)
        {
            current.FileProgressChanged += (_, e) =>
            {
                if (e.TotalTasks > 0)
                    overallProgress?.Invoke(start + weight * Math.Clamp((double)e.ProgressedTasks / e.TotalTasks, 0, 1));
                progress?.Invoke($"{e.Name} · {e.ProgressedTasks}/{e.TotalTasks}");
            };
        }
        overallProgress?.Invoke(0);
        var launcher = CreateLauncher(request.Minecraft);
        Track(launcher, 0, baseWeight);
        await VersionInstallation.Run(root, request.Minecraft, ct => launcher.InstallAsync(request.Minecraft, ct).AsTask(), token);
        overallProgress?.Invoke(baseWeight);
        if (request.Loader == "vanilla")
            return request.Minecraft;
        if (request.Loader is "fabric" or "quilt")
        {
            var endpoint = request.Loader == "fabric" ? "https://meta.fabricmc.net/v2" : "https://meta.quiltmc.org/v3";
            var profile = await http.GetStringAsync($"{endpoint}/versions/loader/{Uri.EscapeDataString(request.Minecraft)}/{Uri.EscapeDataString(request.LoaderVersion)}/profile/json", token);
            using var json = JsonDocument.Parse(profile);
            var id = ContentService.SafeFile(json.RootElement.GetProperty("id").GetString()!);
            await VersionInstallation.Run(root, id, async ct =>
            {
                ct.ThrowIfCancellationRequested();
                AtomicFile.WriteJson(Path.Combine(root, "versions", id, id + ".json"), json.RootElement);
                var loaded = CreateLauncher(id);
                Track(loaded, InstallationStages.BaseGameFiles, InstallationStages.BeforeCommit - InstallationStages.BaseGameFiles);
                await loaded.InstallAsync(id, ct);
            }, token);
            overallProgress?.Invoke(InstallationStages.BeforeCommit);
            return id;
        }

        if (request.Loader is not ("forge" or "neoforge"))
            throw new IOException("不支持的加载器。");
        var neo = request.Loader == "neoforge";
        var version = neo ? request.LoaderVersion : request.Minecraft + "-" + request.LoaderVersion;
        var artifact = neo ? "neoforge" : "forge";
        var url = neo ? $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{version}/neoforge-{version}-installer.jar" : $"https://maven.minecraftforge.net/net/minecraftforge/forge/{version}/forge-{version}-installer.jar";
        var cache = Path.Combine(root, ".blocklaunch-installers");
        Directory.CreateDirectory(cache);
        var jar = Path.Combine(cache, artifact + "-" + ContentService.SafeFile(version) + "-installer.jar");
        progress?.Invoke("获取官方 " + request.Loader + " 安装器…");
        var expected = (await http.GetStringAsync(url + ".sha1", token)).Trim().Split(' ', '\t', '\r', '\n')[0];
        if (expected.Length != 40 || expected.Any(c => !Uri.IsHexDigit(c)))
            throw new IOException("官方安装器校验信息无效。");
        await ReliableDownloader.Download(http, new Uri(url), jar, expected, sha1: true, progress: progress, token: token);
        overallProgress?.Invoke(InstallationStages.LoaderDownloaded);
        using var zip = ZipFile.OpenRead(jar);
        var profileEntry = zip.GetEntry("version.json") ?? throw new IOException("此旧版 Forge 安装器暂不支持自动安装。");
        using var profileJson = await JsonDocument.ParseAsync(profileEntry.Open());
        var launchId = ContentService.SafeFile(profileJson.RootElement.GetProperty("id").GetString()!);
        await VersionInstallation.Run(root, launchId, async ct =>
        {
            var launchOptions = new MLaunchOption
            {
                Session = MSession.CreateOfflineSession("Installer")
            };
            // The installer is a separate tool: a modern JRE avoids obsolete TLS in old game runtimes.
            // The game's Java selection remains based on its own version metadata.
            var installerJava = !string.IsNullOrWhiteSpace(java) ? java : await JavaDiscovery.FindMajor(21, ct) ?? await JavaDiscovery.FindMajor(17, ct) ?? await JavaDiscovery.FindCompatible(root, request.Minecraft, ct);
            if (!string.IsNullOrWhiteSpace(installerJava))
                launchOptions.JavaPath = installerJava;
            using var vanillaProcess = await launcher.BuildProcessAsync(request.Minecraft, launchOptions, token);
            var javaExe = vanillaProcess.StartInfo.FileName;
            if (javaExe.EndsWith("javaw.exe", StringComparison.OrdinalIgnoreCase) && File.Exists(javaExe[..^9] + "java.exe"))
                javaExe = javaExe[..^9] + "java.exe";
            var marker = Path.Combine(root, "launcher_profiles.json");
            if (!File.Exists(marker))
                await File.WriteAllTextAsync(marker, "{\"profiles\":{}}");
            var info = new ProcessStartInfo(javaExe)
            {
                WorkingDirectory = cache,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            AddProxy(info, network);
            info.ArgumentList.Add("-jar");
            info.ArgumentList.Add(jar);
            info.ArgumentList.Add("--installClient");
            info.ArgumentList.Add(root);
            using var process = Process.Start(info) ?? throw new IOException("无法启动官方安装器。");
            var output = Capture(process.StandardOutput);
            var error = Capture(process.StandardError);
            async Task<string> Capture(StreamReader reader)
            {
                var log = new System.Text.StringBuilder();
                var last = DateTime.MinValue;
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (log.Length < 2_000_000)
                        log.AppendLine(line);
                    if ((DateTime.UtcNow - last).TotalSeconds >= 1)
                    {
                        progress?.Invoke("官方安装器：" + line[..Math.Min(line.Length, 220)]);
                        last = DateTime.UtcNow;
                    }
                }

                return log.ToString();
            }

            overallProgress?.Invoke(InstallationStages.OfficialInstallerStarted);
            progress?.Invoke("官方安装器运行中，请稍候…");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromMinutes(15));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(true);
                await process.WaitForExitAsync();
                token.ThrowIfCancellationRequested();
                throw new IOException("官方加载器安装器超时，请检查网络。");
            }

            await File.WriteAllTextAsync(Path.Combine(cache, artifact + "-last-install.log"), await output + "\n" + await error);
            if (process.ExitCode != 0 || !File.Exists(Path.Combine(root, "versions", launchId, launchId + ".json")))
                throw new IOException("官方加载器安装失败，详细日志位于 " + cache);
            // Recreate to refresh the version metadata cache after the external installer.
            var loaded = CreateLauncher(launchId);
            Track(loaded, InstallationStages.LoaderFinalFiles, InstallationStages.BeforeCommit - InstallationStages.LoaderFinalFiles);
            overallProgress?.Invoke(InstallationStages.LoaderFinalFiles);
            await loaded.InstallAsync(launchId, ct);
        }, token);
        overallProgress?.Invoke(InstallationStages.BeforeCommit);
        return launchId;
    }

    static void AddProxy(ProcessStartInfo info, NetworkSettings? network)
    {
        if (network?.ProxyMode == "直连")
        {
            info.ArgumentList.Add("-Djava.net.useSystemProxies=false");
            return;
        }

        if (network?.ProxyMode == "自定义代理")
        {
            network.Validate();
            var proxy = new Uri(network.ProxyAddress);
            foreach (var scheme in new[] {
                "http",
                "https"
            }

            )
            {
                info.ArgumentList.Add($"-D{scheme}.proxyHost={proxy.Host}");
                info.ArgumentList.Add($"-D{scheme}.proxyPort={proxy.Port}");
            }

            return;
        }
        // Java does not read the HTTP(S)_PROXY variables used by HttpClient.
        foreach (var scheme in new[] {
            "http",
            "https"
        }

        )
        {
            var value = Environment.GetEnvironmentVariable(scheme.ToUpperInvariant() + "_PROXY") ?? Environment.GetEnvironmentVariable(scheme + "_proxy");
            if (!Uri.TryCreate(value, UriKind.Absolute, out var proxy) || proxy.Scheme != "http" || proxy.UserInfo.Length > 0)
                continue;
            info.ArgumentList.Add($"-D{scheme}.proxyHost={proxy.Host}");
            info.ArgumentList.Add($"-D{scheme}.proxyPort={proxy.Port}");
        }

        var exclusions = Environment.GetEnvironmentVariable("NO_PROXY") ?? Environment.GetEnvironmentVariable("no_proxy");
        if (!string.IsNullOrWhiteSpace(exclusions))
        {
            var hosts = exclusions.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Where(h => !h.Contains('/') && !h.Contains(':')).Select(h => h.StartsWith('.') ? "*" + h : h);
            var pattern = string.Join('|', hosts);
            if (pattern.Length > 0)
                info.ArgumentList.Add("-Dhttp.nonProxyHosts=" + pattern);
        }
    }
}
