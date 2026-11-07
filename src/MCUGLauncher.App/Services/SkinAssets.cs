using System.Security.Cryptography;
using System.Text;

namespace MCUGLauncher.Services;

/// <summary>皮肤地址校验与有界下载；不依赖 UI。</summary>
internal static class SkinAssets
{
    public static Uri SkinUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Host != "textures.minecraft.net"
            || uri.Scheme is not ("https" or "http")
            || !uri.AbsolutePath.StartsWith("/texture/", StringComparison.Ordinal)
            || uri.UserInfo.Length > 0)
            throw new InvalidDataException("皮肤来源无效。");
        return new UriBuilder(Uri.UriSchemeHttps, "textures.minecraft.net")
        {
            Path = uri.AbsolutePath
        }.Uri;
    }

    public static string CachePath(string directory, Uri uri) =>
        Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri))) + ".png");

    public static async Task<byte[]> Download(HttpClient http, Uri uri, CancellationToken token)
    {
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 2 * 1024 * 1024)
            throw new InvalidDataException("皮肤文件过大。");
        await using var source = await response.Content.ReadAsStreamAsync(token);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await source.ReadAsync(buffer, token)) > 0)
        {
            if (output.Length + read > 2 * 1024 * 1024)
                throw new InvalidDataException("皮肤文件过大。");
            await output.WriteAsync(buffer.AsMemory(0, read), token);
        }
        return output.ToArray();
    }
}
