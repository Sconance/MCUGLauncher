using System.Diagnostics;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;

namespace BlockLaunch;

/// <summary>
/// 表示 启动Request 数据。
/// </summary>
public record LaunchRequest(string Root, string InstanceDirectory, string Profile, string Java, int Ram, int Width, int Height, MSession Session, LaunchSettings? Settings = null, AuthlibLaunch? ExternalAuthentication = null);

/// <summary>
/// 表示 GameExit 数据。
/// </summary>
public record GameExit(int Code, bool Requested);

/// <summary>
/// 构建游戏启动参数与进程。
/// </summary>
/// <remarks>
/// 依赖 CmlLib.Core、JavaDiscovery、AuthlibLaunch、LaunchSettings 与 LaunchRequest 中的账号/内存/窗口信息；只负责构建 Process，实际启动由 GameSession.Start 完成。
/// </remarks>
public static class LaunchService
{
    /// <summary>
    /// 构建。
    /// </summary>
    /// <param name="request">启动请求：根目录、实例目录、Java、内存、窗口、会话等</param>
    /// <param name="token">取消令牌</param>
    /// <param name="http">可选 HttpClient，用于加载器/资源请求</param>
    /// <returns>已配置好 StartInfo 的 Process 实例</returns>
    public static async Task<Process> Build(LaunchRequest request, CancellationToken token, HttpClient? http = null)
    {
        var shared = new MinecraftPath(request.Root);
        var instance = new MinecraftPath(request.InstanceDirectory)
        {
            Library = shared.Library,
            Versions = shared.Versions,
            Assets = shared.Assets,
            Runtime = shared.Runtime,
            Resource = shared.Resource
        };
        var parameters = http == null ? MinecraftLauncherParameters.CreateDefault(shared) : MinecraftLauncherParameters.CreateDefault(shared, http);
        parameters.VersionLoader = new CmlLib.Core.VersionLoader.LocalJsonVersionLoader(shared);
        var options = new MLaunchOption
        {
            Session = request.Session,
            MaximumRamMb = request.Ram,
            ScreenWidth = request.Width,
            ScreenHeight = request.Height,
            GameLauncherName = "MCUGLauncher",
            Path = instance
        };
        var java = request.Java.Length > 0 ? request.Java : await JavaDiscovery.FindCompatible(request.Root, request.Profile, token);
        if (request.Java.Length > 0)
        {
            var required = JavaDiscovery.RequiredMajor(request.Root, request.Profile);
            var actual = await JavaDiscovery.ProbeMajor(java!, token);
            if (required > 0 && actual != required)
                throw new IOException($"此游戏版本要求 Java {required}，当前选择的是 Java {actual}。请在版本设置中改为自动管理或选择 Java {required}。");
        }

        if (!string.IsNullOrEmpty(java))
        {
            if (JavaDiscovery.Architecture(java) == "x86" && request.Ram > 1400)
                throw new IOException("当前 Java 为 32 位，无法可靠使用此内存分配。请在 Java 管理中选择 64 位 Java，或降低内存。");
            options.JavaPath = java;
        }

        if (request.Settings is
            {
            } settings)
        {
            settings.Validate();
            options.FullScreen = settings.FullScreen;
            options.VersionType = settings.VersionInfo;
            options.ExtraJvmArguments = LaunchSettings.SplitArguments(settings.JvmArguments).Select(x => new MArgument(x));
            if (settings.Server.Length > 0)
            {
                var endpoint = ServerEndpoint.Parse(settings.Server);
                options.ServerIp = endpoint.Host.Contains(':') ? "[" + endpoint.Host + "]" : endpoint.Host;
                options.ServerPort = endpoint.Port;
            }
        }

        if (request.ExternalAuthentication is
            {
            } external)
        {
            if (request.Session.UserType != "mojang" || string.IsNullOrWhiteSpace(request.Session.AccessToken))
                throw new IOException("MUA 会话无效。");
            var custom = request.Settings == null ? [] : LaunchSettings.SplitArguments(request.Settings.JvmArguments);
            if (custom.Any(a => a.StartsWith("-Dauthlibinjector.", StringComparison.OrdinalIgnoreCase) || a.StartsWith("-javaagent:", StringComparison.OrdinalIgnoreCase) && a.Contains("authlib", StringComparison.OrdinalIgnoreCase)))
                throw new IOException("请移除自定义 JVM 参数中的重复 authlib-injector 配置。");
            options.ExtraJvmArguments = custom.Concat(external.Arguments()).Select(a => new MArgument(a));
        }

        var process = await new MinecraftLauncher(parameters).BuildProcessAsync(request.Profile, options, token);
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        return process;
    }
}

/// <summary>
/// 提供 Game会话 相关功能。
/// </summary>
public sealed class GameSession(Process process, string outputFile, Action<string> output, IEnumerable<string> secrets)
{
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    static extern bool PostMessage(nint window, uint message, nuint wParam, nint lParam);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    public Process Process => process;

    public bool StopRequested
    {
        get;
        private set;
    }
    /// <summary>
    /// 获取或设置 Completion。
    /// </summary>
    public Task<GameExit> Completion { get; private set; } = Task.FromResult(new GameExit(0, false));

    /// <summary>
    /// 启动。
    /// </summary>
    public void Start()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
        process.Start();
        Completion = Observe();
    }

    async Task<GameExit> Observe()
    {
        StreamWriter writer;
        try
        {
            writer = new StreamWriter(new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            {
                AutoFlush = true
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            writer = new StreamWriter(Stream.Null);
            output("捕获日志文件无法创建，仍会读取游戏输出。");
        }

        using var log = writer;
        var gate = new SemaphoreSlim(1);
        var logWritable = true;
        async Task Pump(StreamReader reader)
        {
            while (await reader.ReadLineAsync() is
                {
                } line)
            {
                line = LogPrivacy.Redact(line, secrets);
                await gate.WaitAsync();
                try
                {
                    if (logWritable)
                    {
                        if (log.BaseStream.Length > 5 * 1024 * 1024)
                        {
                            await log.FlushAsync();
                            log.BaseStream.SetLength(0);
                            log.BaseStream.Position = 0;
                        }

                        await log.WriteLineAsync(line);
                    }
                }
                catch (IOException)
                {
                    logWritable = false;
                    output("捕获日志暂时无法写入，游戏输出仍在读取。");
                }
                finally
                {
                    gate.Release();
                }

                output(line);
            }
        }

        var stdout = Pump(process.StandardOutput);
        var stderr = Pump(process.StandardError);
        await process.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr);
        gate.Dispose();
        return new(process.ExitCode, StopRequested);
    }

    /// <summary>
    /// 执行 RequestStop。
    /// </summary>
    public bool RequestStop()
    {
        process.Refresh();
        if (process.HasExited)
            return false;
        var window = process.MainWindowHandle;
        // LWJGL 2 handles WM_SYSCOMMAND/SC_CLOSE, while WM_CLOSE alone bypasses its exit flag.
        // https://github.com/LWJGL/lwjgl/blob/master/src/java/org/lwjgl/opengl/WindowsDisplay.java
        var sent = false;
        if (window != 0)
        {
            GetWindowThreadProcessId(window, out var owner);
            if (owner != (uint)process.Id)
                return false;
            sent = PostMessage(window, 0x0112, 0xF060, 0);
        }

        if (!sent)
            sent = process.CloseMainWindow();
        if (sent)
            StopRequested = true;
        return sent;
    }
}
