using System.Text.Json;
using BlockLaunch;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

internal sealed class InstanceMaintenanceCommands(LauncherContext context)
{
    public void Clone(JsonElement root)
    {
        var instance = InstanceSnapshot.Capture(context.Config, ReadString(root, "version"));
        var request = instance.Request();
        request.Name = ReadString(root, "name").Trim();
        if (request.Name.Length is 0 or > 100)
            throw new ArgumentException("实例名称需为 1–100 个字符。");
        request.Clone = new CloneOptions(ReadBool(root, "saves"), false, ReadBool(root, "servers"));
        context.Tasks.Enqueue("克隆 " + instance.Name, instance.Root, instance.Directory,
            new DownloadRequest { Kind = "clone", Instance = request });
    }

    public void Export(JsonElement root)
    {
        var instance = InstanceSnapshot.Capture(context.Config, ReadString(root, "version"));
        if (instance.Requirements.Loader is not ("vanilla" or "fabric" or "quilt" or "forge" or "neoforge"))
            throw new IOException("此加载器暂不支持导出兼容整合包。");
        var destination = Path.GetFullPath(ReadString(root, "path"));
        if (!destination.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            throw new IOException("导出文件必须为 ZIP。");
        if (File.Exists(destination))
            throw new IOException("为保护已有文件，请选择新的 ZIP 文件名。");
        var request = instance.Request();
        request.Export = new ExportOptions(true, ReadBool(root, "servers"), true, true, ReadBool(root, "saves"));
        context.Tasks.Enqueue("导出 " + instance.Name, instance.Root, instance.Directory,
            new DownloadRequest { Kind = "export", Instance = request, Destination = destination });
    }

    static bool ReadBool(JsonElement root, string name) => root.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.True;
}
