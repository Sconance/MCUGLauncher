using System.Security.Cryptography;

namespace BlockLaunch;

/// <summary>
/// 表示 Authlib启动 数据。
/// </summary>
public record AuthlibLaunch(string Jar, string Metadata)
{
    public const string Api = "https://skin.mualliance.ltd/api/yggdrasil/";
    public const string Version = "1.2.8";
    public const string Sha256 = "9c7f4343e6c82034958ffb48c14a2cb0c85928be7283103ce17da00c6d5a7b10";

    /// <summary>
    /// 执行 ValidJar。
    /// </summary>
    public static bool ValidJar(string path)
    {
        if (!File.Exists(path))
            return false;
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).Equals(Sha256, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 准备。
    /// </summary>
    public static async Task<string> Prepare(string directory, HttpClient http, CancellationToken token)
    {
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "authlib-injector-" + Version + ".jar");
        if (ValidJar(file))
            return file;
        var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using var response = await http.GetAsync("https://authlib-injector.yushi.moe/artifact/56/authlib-injector-1.2.8.jar", HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            await using (var source = await response.Content.ReadAsStreamAsync(token))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                int size;
                long total = 0;
                while ((size = await source.ReadAsync(buffer, token)) > 0)
                {
                    total += size;
                    if (total > 5 * 1024 * 1024)
                        throw new IOException("MUA 认证组件超出大小限制。");
                    await output.WriteAsync(buffer.AsMemory(0, size), token);
                }
            }

            if (!ValidJar(temporary))
                throw new IOException("MUA 认证组件 SHA-256 校验失败，已停止启动。");
            token.ThrowIfCancellationRequested();
            File.Move(temporary, file, true);
            return file;
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    /// <summary>
    /// 执行 Arguments。
    /// </summary>
    public IReadOnlyList<string> Arguments()
    {
        if (!ValidJar(Jar))
            throw new IOException("MUA 认证组件缺失或校验失败。");
        if (Metadata.Length > 1024 * 1024)
            throw new IOException("MUA 认证配置过大。");
        using var json = System.Text.Json.JsonDocument.Parse(Metadata);
        if (!json.RootElement.TryGetProperty("signaturePublickey", out _))
            throw new IOException("MUA 认证配置无效。");
        return ["-javaagent:" + Path.GetFullPath(Jar) + "=" + Api, "-Dauthlibinjector.side=client", "-Dauthlibinjector.yggdrasil.prefetched=" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Metadata))];
    }
}
