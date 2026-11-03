using System.Security.Cryptography;
using System.Text.Json;
using BlockLaunch;
using MCUGLauncher.Contracts;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

/// <summary>为当前账号读取并缓存头像，或设置离线皮肤。</summary>
/// <remarks>
/// 组件级别：B。
/// 依赖：
/// - LauncherContext.AccountRevision、AvatarIdentity、Config、Http、Microsoft、Mua、Store、Token、Log；
/// - AvatarDiskCache.ReadAsync(string, CancellationToken)、ReadSourceAsync(string, CancellationToken)、SaveAsync(string, string, string, long, CancellationToken, DateTimeOffset?)、IsFresh(Entry)；
/// - SkinAssets.SkinUri(string)、Download(HttpClient, Uri, CancellationToken)；
/// - ProtocolValues.ReadString(JsonElement, string)。
/// 不依赖 View、窗口控件、LauncherService 或 LauncherApplication。
/// </remarks>
internal sealed class AvatarCommands(LauncherContext context, Func<byte[]?, string> encode)
{
    sealed record Portrait(
        string Image,
        string Label,
        bool Fallback,
        string Source = "",
        DateTimeOffset? SourceChecked = null);

    sealed record PendingPortrait(Task<Portrait> Work, bool Forced);

    readonly AvatarDiskCache disk = new(Path.Combine(context.Store.DataDirectory, "cache", "avatars", "v1"));
    readonly Dictionary<string, PendingPortrait> pending = [];
    readonly Dictionary<string, long> generations = [];
    readonly object requestGate = new();
    readonly Dictionary<string, long> latestReads = [];
    long nextGeneration;
    long nextRead;
    string? defaultImage;

    public async Task<AvatarInfo?> ReadAsync(bool force = false)
    {
        var revision = context.AccountRevision;
        var mode = context.Config.AccountType;
        var player = context.Config.Player;
        var skinFile = context.Config.SkinFile;
        var key = context.AvatarIdentity;
        long read;
        lock (requestGate)
            latestReads[key] = read = ++nextRead;

        bool IsCurrent()
        {
            lock (requestGate)
                return revision == context.AccountRevision && latestReads.GetValueOrDefault(key) == read;
        }

        var cached = await disk.ReadAsync(key, context.Token);
        if (!IsCurrent())
            return null;
        bool hasPending;
        lock (requestGate)
            hasPending = pending.ContainsKey(key);
        if (!force && !hasPending && cached is not null && AvatarDiskCache.IsFresh(cached))
            return ToInfo(cached.Image, CachedLabel(mode, skinFile), false);

        Task<Portrait> work;
        long generation;
        lock (requestGate)
        {
            if (!pending.TryGetValue(key, out var current) || (force && !current.Forced))
            {
                generations[key] = ++nextGeneration;
                work = LoadAsync(mode, skinFile, force);
                pending[key] = new(work, force);
            }
            else
                work = current.Work;
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
            {
                if (pending.TryGetValue(key, out var current) && ReferenceEquals(current.Work, work))
                    pending.Remove(key);
            }
        }

        if (!IsCurrent())
            return null;
        if (portrait.Fallback && cached is not null)
            return ToInfo(cached.Image, CachedLabel(mode, skinFile), false);
        if (!portrait.Fallback)
        {
            try
            {
                await disk.SaveAsync(key, portrait.Source, portrait.Image, generation, context.Token, portrait.SourceChecked);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
            {
                context.Log("头像缓存未能保存 [" + error.GetType().Name + "]，本次显示不受影响");
            }
        }
        return ToInfo(portrait.Image, portrait.Label, portrait.Fallback);

        AvatarInfo ToInfo(string image, string label, bool fallback) =>
            new(image, label, fallback, mode, player);
    }

    async Task<Portrait> LoadAsync(string mode, string skinFile, bool force)
    {
        await Task.Yield();
        byte[] bytes;
        string source;
        string label;
        Uri? uri = null;
        try
        {
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
                if (saved is null || context.Mua is null)
                    return Default();
                var metadata = await context.Mua.Metadata(context.Token);
                uri = await context.Mua.Skin(saved, metadata, context.Token);
                if (uri is null)
                    return Default();
                source = uri.AbsoluteUri;
                label = "MUA 皮肤";
            }

            if (!force && await disk.ReadSourceAsync(source, context.Token) is { } existing
                && AvatarDiskCache.IsFresh(existing))
                return new(existing.Image, label, false, source, existing.Checked);

            bytes = uri is null
                ? await ReadLocalAsync(skinFile)
                : await SkinAssets.Download(context.Http, uri, context.Token);
        }
        catch (OperationCanceledException) when (context.Token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (IsExpectedSourceFailure(error))
        {
            context.Log("头像来源读取失败 [" + error.GetType().Name + "]，保留已有头像或使用默认图");
            return Default();
        }

        return new(encode(bytes), label, false, source);
    }

    static bool IsExpectedSourceFailure(Exception error) =>
        error is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or HttpRequestException
            or ArgumentException
            or OperationCanceledException
            or AccountStorageException
            or CryptographicException
            or JsonException;

    static string CachedLabel(string mode, string skinFile) => mode switch
    {
        "offline" => Path.GetFileName(skinFile) is { Length: > 0 } name ? name : "离线皮肤",
        "microsoft" => "正版皮肤",
        _ => "MUA 皮肤"
    };

    Portrait Default()
    {
        lock (requestGate)
            return new(defaultImage ??= encode(null), "史蒂夫默认头像", true);
    }

    public async Task<AvatarInfo?> SetAsync(JsonElement message)
    {
        if (context.Config.AccountType != "offline")
            throw new InvalidOperationException("本地皮肤仅可在离线模式选择；在线账号读取认证站皮肤。");
        var path = ReadString(message, "path");
        if (path.Length == 0)
            context.Config.SkinFile = "";
        else
        {
            var bytes = await ReadLocalAsync(path);
            _ = encode(bytes);
            var destination = Path.Combine(context.Store.DataDirectory, "offline-skin.png");
            await File.WriteAllBytesAsync(destination, bytes, context.Token);
            context.Config.SkinFile = destination;
        }
        Interlocked.Increment(ref context.AccountRevision);
        context.Store.Save(context.Config);
        return await ReadAsync(force: true);
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
