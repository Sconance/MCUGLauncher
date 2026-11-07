using System.Diagnostics;
using System.Reflection;
using System.Security;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.Avalonia.Views;

internal static class EmptyServiceFallback
{
    static readonly ILauncherService instance =
        DispatchProxy.Create<ILauncherService, EmptyServiceProxy>();

    public static ILauncherService Instance => instance;
}

public class EmptyServiceProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var methodName = targetMethod?.Name ?? "unknown";
        Log($"UI 调用了空服务：ILauncherService.{methodName}");

        var returnType = targetMethod?.ReturnType;
        if (returnType is null || returnType == typeof(void))
            return null;
        if (returnType == typeof(Task))
            return Task.CompletedTask;
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = returnType.GetGenericArguments()[0];
            var result = CreateResult(resultType);
            return typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType)
                .Invoke(null, [result]);
        }

        if (returnType == typeof(CommunityData))
            return CommunityData.Empty;
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            return Array.CreateInstance(returnType.GetGenericArguments()[0], 0);
        return DefaultValue(returnType);
    }

    static object? DefaultValue(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;

    static object? CreateResult(Type type)
    {
        if (type == typeof(byte[]))
            return Array.Empty<byte>();
        if (type == typeof(AuthResultInfo))
            return new AuthResultInfo(false, false, "服务未就绪。");
        if (type == typeof(AccountSwitchResultInfo))
            return new AccountSwitchResultInfo("", false, "服务未就绪。");
        if (type == typeof(ServerResultInfo))
            return new ServerResultInfo(false, "服务未就绪。");
        if (type == typeof(ServerPingInfo))
            return new ServerPingInfo("", false, 0, 0, 0, "", "", "服务未就绪。");
        if (type == typeof(KeyResultInfo))
            return new KeyResultInfo("", false, "服务未就绪。");
        if (type == typeof(SavedInfo))
            return new SavedInfo("服务未就绪。");
        if (type == typeof(SystemInfoInfo))
            return new SystemInfoInfo("", "", "", "", "", "");
        if (type == typeof(LaunchOptionsInfo))
            return new LaunchOptionsInfo(
                "",
                "",
                "",
                "",
                true,
                new LaunchSettingsInfo(true, false, 4096, 1280, 720, "", "", "", ""));
        if (type == typeof(UninstallPreviewInfo))
            return new UninstallPreviewInfo("", "", "", "服务未就绪。", Array.Empty<string>(), false);
        if (type == typeof(ContentUpdatesInfo))
            return new ContentUpdatesInfo("", "", Array.Empty<ContentUpdateInfo>());
        if (type == typeof(ContentUpdatePreviewInfo))
            return new ContentUpdatePreviewInfo("", "", 0, Array.Empty<ContentChangeInfo>());
        if (type == typeof(ContentSearchResultInfo))
            return new ContentSearchResultInfo("", "", Array.Empty<ContentItem>(), "服务未就绪。");
        if (type == typeof(ResourceVersionsResultInfo))
            return ResourceVersionsResultInfo.Failure("服务未就绪。");
        if (type == typeof(LoaderListResultInfo))
            return LoaderListResultInfo.Failure("服务未就绪。");
        if (type == typeof(VanillaVersionsResultInfo))
            return VanillaVersionsResultInfo.Failure("服务未就绪。");
        if (type == typeof(PackPreviewResultInfo))
            return PackPreviewResultInfo.Failure("服务未就绪。");
        return DefaultValue(type);
    }

    static void Log(string message)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MCUGLauncher");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "launcher.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"无法记录空服务调用：{ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"无法记录空服务调用：{ex.Message}");
        }
        catch (SecurityException ex)
        {
            Debug.WriteLine($"无法记录空服务调用：{ex.Message}");
        }
    }
}
