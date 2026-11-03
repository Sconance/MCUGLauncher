using System.Text.Json;
using MCUGLauncher;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

/// <summary>读取和缓存头像，合并自动刷新；手动刷新绕过缓存，不参与账号授权。</summary>
/// <remarks>
/// B 类：读取 LauncherContext.Config.AccountType/Player/SkinFile、AccountRevision、AvatarIdentity；
/// Token/Http 用于取消和有界下载，Microsoft.ReadSavedAccount、Mua.Read/Metadata/Skin 提供皮肤来源；
/// Store.DataDirectory/Save(AppConfig) 保存离线皮肤设置，Send(object) 回推快照。
/// AvatarDiskCache.ReadAsync/ReadSourceAsync/SaveAsync 管理账号和来源索引及图片持久化。
/// 编码由构造参数提供，无控件依赖；成员和缓存规则见 docs/components/AvatarCommands.md。
/// </remarks>
internal sealed class AvatarCommands(LauncherContext context, Func<byte[]?, string> encode)
{
    sealed record Portrait(string Image, string Label, bool Fallback, string Source = "", DateTimeOffset? SourceChecked = null);
    readonly AvatarDiskCache disk = new(Path.Combine(context.Store.DataDirectory, "cache", "avatars", "v1"));
    readonly Dictionary<string, Task<Portrait>> pending = [];
    readonly Dictionary<string, long> generations = [];
    readonly object requestGate = new();
    readonly Dictionary<string, long> latestReads = [];
    long nextGeneration, nextRead;
    string? defaultImage;

    public async Task ReadAsync(bool force = true)
    {
        var revision = context.AccountRevision;
        var mode = context.Config.AccountType;
        var player = context.Config.Player;
        var skinFile = context.Config.SkinFile;
        var key = context.AvatarIdentity;
        long read;
        lock (requestGate)
            latestReads[key] = read = ++nextRead;
        bool Current()
        {
            lock (requestGate)
                return revision == context.AccountRevision && latestReads.GetValueOrDefault(key) == read;
        }
        var cached = await disk.ReadAsync(key, context.Token);
        if (!Current())
            return;
        if (cached != null)
        {
            Send(new(cached.Image, "本地缓存头像", false));
            lock (requestGate)
                if (!force && AvatarDiskCache.IsFresh(cached) && !pending.ContainsKey(key))
                    return;
        }
        else if (mode != "offline" || string.IsNullOrEmpty(skinFile))
            Send(Default());

        Task<Portrait> work;
        long generation;
        lock (requestGate)
        {
            if (force || !pending.TryGetValue(key, out work!))
            {
                generations[key] = ++nextGeneration;
                work = LoadAsync(mode, skinFile, force);
                pending[key] = work;
            }
            generation = generations[key];
        }
        Portrait portrait;
        try
        {
            portrait = await work;
        }
        finally
        {
            lock (requestGate)
                if (pending.TryGetValue(key, out var current) && ReferenceEquals(current, work))
                    pending.Remove(key);
        }
        if (!Current())
            return;
        // A failed refresh retains the last good on-disk portrait, including across application restarts.
        if (portrait.Fallback && cached != null)
            return;
        Send(portrait);
        if (!portrait.Fallback)
        {
            try
            {
                await disk.SaveAsync(key, portrait.Source, portrait.Image, generation, context.Token, portrait.SourceChecked);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException)
            {
                context.Log("头像缓存未能保存 [" + error.GetType().Name + "]，本次显示不受影响");
            }
        }

        void Send(Portrait value)
        {
            if (!Current())
                return;
            context.Send(new
            {
                type = "avatar",
                image = value.Image,
                label = value.Label,
                fallback = value.Fallback,
                avatarIdentity = key,
                account = mode,
                player
            });
        }
    }
    Portrait Default()
    {
        lock (requestGate)
            return new(defaultImage ??= encode(null), "史蒂夫默认头像", true);
    }

    async Task<Portrait> LoadAsync(string mode, string skinFile, bool force)
    {
        await Task.Yield();
        try
        {
            byte[]? bytes = null;
            string source;
            string label;
            Uri? uri = null;
            if (mode == "offline")
            {
                if (!File.Exists(skinFile))
                    return Default();
                source = "local:" + skinFile + ":" + File.GetLastWriteTimeUtc(skinFile).Ticks;
                label = Path.GetFileName(skinFile);
            }
            else if (mode == "microsoft")
            {
                var saved = await Task.Run(() => context.Microsoft?.ReadSavedAccount(), context.Token);
                if (string.IsNullOrWhiteSpace(saved?.SkinUrl))
                    return Default();
                uri = SkinAssets.SkinUri(saved.SkinUrl);
                source = uri.AbsoluteUri;
                label = "正版皮肤";
            }
            else
            {
                var saved = await Task.Run(() => context.Mua?.Read(), context.Token);
                if (saved == null || context.Mua == null)
                    return Default();
                var metadata = await context.Mua.Metadata(context.Token);
                uri = await context.Mua.Skin(saved, metadata, context.Token);
                if (uri == null)
                    return Default();
                source = uri.AbsoluteUri;
                label = "MUA 皮肤";
            }
            if (!force && await disk.ReadSourceAsync(source, context.Token) is { } existing && AvatarDiskCache.IsFresh(existing))
                return new(existing.Image, label, false, source, existing.Checked);
            bytes = uri == null ? await ReadLocalAsync(skinFile) : await SkinAssets.Download(context.Http, uri, context.Token);
            return new(encode(bytes), label, false, source);
        }
        catch (OperationCanceledException) when (context.Token.IsCancellationRequested) { throw; }
        catch (Exception error) when (error is IOException or InvalidDataException or HttpRequestException or UnauthorizedAccessException or ArgumentException or OperationCanceledException)
        {
            // Record a category, never a URL, token or account payload.
            context.Log("头像来源读取失败 [" + error.GetType().Name + "]，保留已有头像或使用默认图");
            return Default();
        }
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
