using System.Text.Json;
using BlockLaunch;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

// Reuses bounded skin download and PNG validation. Skin selection changes launcher appearance only.
internal sealed class AvatarCommands(LauncherContext context)
{
    public async Task ReadAsync()
    {
        var revision = context.AccountRevision;
        var mode = context.Config.AccountType;
        var label = "默认头像";
        var image = AvatarImage.Encode(null);
        try
        {
            byte[]? bytes = null;
            if (mode == "offline" && File.Exists(context.Config.SkinFile))
            {
                bytes = await ReadLocalAsync(context.Config.SkinFile);
                label = Path.GetFileName(context.Config.SkinFile);
            }
            else if (mode == "microsoft")
            {
                var saved = context.Microsoft?.ReadSavedAccount();
                if (!string.IsNullOrWhiteSpace(saved?.SkinUrl))
                {
                    bytes = await AvatarRenderer.Download(context.Http, AvatarRenderer.SkinUri(saved.SkinUrl), context.Token);
                    label = "正版皮肤";
                }
            }
            else if (mode == "mua" && context.Mua?.Read() is { } saved)
            {
                var metadata = await context.Mua.Metadata(context.Token);
                var uri = await context.Mua.Skin(saved, metadata, context.Token);
                if (uri != null)
                    bytes = await AvatarRenderer.Download(context.Http, uri, context.Token);
                label = bytes == null ? label : "MUA 皮肤";
            }
            if (bytes != null)
                image = AvatarImage.Encode(bytes);
        }
        catch { label = "皮肤读取失败，使用默认头像"; }
        if (revision != context.AccountRevision)
            return;
        context.Send(new
        {
            type = "avatar",
            image,
            label,
            account = mode,
            player = context.Config.Player
        });
    }

    public async Task SetAsync(JsonElement message)
    {
        if (context.Config.AccountType != "offline")
            throw new InvalidOperationException("本地皮肤仅可在离线模式选择；在线账号读取认证站皮肤。");
        var path = ReadString(message, "path");
        if (path.Length == 0)
            context.Config.SkinFile = "";
        else
        {
            var bytes = await ReadLocalAsync(path);
            AvatarImage.Encode(bytes);
            var destination = Path.Combine(context.Store.DataDirectory, "offline-skin.png");
            await File.WriteAllBytesAsync(destination, bytes, context.Token);
            context.Config.SkinFile = destination;
        }
        context.Store.Save(context.Config);
        await ReadAsync();
    }

    async Task<byte[]> ReadLocalAsync(string path)
    {
        await using var input = File.OpenRead(path);
        if (input.Length > 2 * 1024 * 1024)
            throw new IOException("皮肤文件不能超过 2 MB。");
        using var output = new MemoryStream();
        await input.CopyToAsync(output, context.Token);
        return output.ToArray();
    }
}
