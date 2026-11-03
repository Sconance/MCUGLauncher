using System.Text.Json;
using MCUGLauncher;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

/// <summary>读取和缓存头像，合并自动刷新；手动刷新绕过缓存，不参与账号授权。</summary>
/// <remarks>
/// B 类：读取 LauncherContext.Config.AccountType/Player/SkinFile、AccountRevision、OnlineSession.UUID；
/// Token/Http 用于取消和有界下载，Microsoft.ReadSavedAccount、Mua.Read/Metadata/Skin 提供皮肤来源；
/// Store.DataDirectory/Save(AppConfig) 保存离线皮肤设置，Send(object) 回推快照。
/// 编码由构造参数提供，无控件依赖；成员和缓存规则见 docs/components/AvatarCommands.md。
/// </remarks>
internal sealed class AvatarCommands(LauncherContext context, Func<byte[]?, string> encode)
{
    sealed record Portrait(string Image, string Label, bool Fallback, DateTimeOffset Loaded);
    readonly Dictionary<string, Portrait> cache = [];
    readonly Dictionary<string, Task<Portrait>> pending = [];
    string? defaultImage;

    public async Task ReadAsync(bool force = true)
    {
        var revision = context.AccountRevision;
        var mode = context.Config.AccountType;
        var player = context.Config.Player;
        var skinFile = context.Config.SkinFile;
        var key = mode + ":" + player + ":" + context.OnlineSession?.UUID + ":" + (mode == "offline" ? skinFile : "");
        if (!force && cache.TryGetValue(key, out var saved) && DateTimeOffset.UtcNow - saved.Loaded <
            (saved.Fallback ? TimeSpan.FromSeconds(15) : TimeSpan.FromMinutes(5)))
        {
            Send(saved);
            return;
        }
        if (force || !pending.TryGetValue(key, out var work))
        {
            work = LoadAsync(mode, skinFile);
            pending[key] = work;
        }
        Portrait portrait;
        try
        {
            portrait = await work;
        }
        finally { if (pending.TryGetValue(key, out var current) && ReferenceEquals(current, work)) pending.Remove(key); }
        if (revision != context.AccountRevision)
            return;
        cache[key] = portrait;
        if (cache.Count > 24)
            cache.Remove(cache.Keys.First());
        Send(portrait);

        void Send(Portrait value) => context.Send(new
        {
            type = "avatar",
            image = value.Image,
            label = value.Label,
            fallback = value.Fallback,
            account = mode,
            player
        });
    }

    async Task<Portrait> LoadAsync(string mode, string skinFile)
    {
        // Dispatch account selection notifications before starting local metadata and skin work.
        await Task.Yield();
        var label = "史蒂夫默认头像";
        try
        {
            byte[]? bytes = null;
            if (mode == "offline" && File.Exists(skinFile))
            {
                bytes = await ReadLocalAsync(skinFile);
                label = Path.GetFileName(skinFile);
            }
            else if (mode == "microsoft")
            {
                var saved = context.Microsoft?.ReadSavedAccount();
                if (!string.IsNullOrWhiteSpace(saved?.SkinUrl))
                {
                    bytes = await SkinAssets.Download(context.Http, SkinAssets.SkinUri(saved.SkinUrl), context.Token);
                    label = "正版皮肤";
                }
            }
            else if (mode == "mua" && context.Mua?.Read() is { } saved)
            {
                var metadata = await context.Mua.Metadata(context.Token);
                var uri = await context.Mua.Skin(saved, metadata, context.Token);
                if (uri != null)
                    bytes = await SkinAssets.Download(context.Http, uri, context.Token);
                if (bytes != null)
                    label = "MUA 皮肤";
            }
            if (bytes != null)
                return new(encode(bytes), label, false, DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (context.Token.IsCancellationRequested) { throw; }
        catch { label = "皮肤读取失败，使用史蒂夫头像"; }
        return new(defaultImage ??= encode(null), label, true, DateTimeOffset.UtcNow);
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
            encode(bytes);
            var destination = Path.Combine(context.Store.DataDirectory, "offline-skin.png");
            await File.WriteAllBytesAsync(destination, bytes, context.Token);
            context.Config.SkinFile = destination;
        }
        Interlocked.Increment(ref context.AccountRevision);
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
