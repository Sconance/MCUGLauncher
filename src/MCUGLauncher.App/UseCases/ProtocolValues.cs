using System.Security.Cryptography;
using System.Text.Json;
using System.Text;

namespace MCUGLauncher.UseCases;

/// <summary>校验协议字段、实例定位和离线身份值。</summary>
/// <remarks>B 类。ReadInstanceTarget(JsonElement) 依赖 Contracts.InstanceTarget(string,string)；
/// 其余字段和身份校验仅依赖 BCL。不读配置或访问 UI。见 docs/components/GameDirectories.md。</remarks>
internal static class ProtocolValues
{
    public static MCUGLauncher.Contracts.InstanceTarget ReadInstanceTarget(JsonElement arguments)
    {
        var target = arguments.GetProperty("target");
        var root = ReadString(target, "root");
        var name = ReadString(target, "name");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("必须明确指定游戏根目录和实例名称。");
        return new(root, name);
    }

    // Validate field kinds before handlers mutate configuration or touch files.
    public static void ValidateMessage(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(ReadString(root, "type")))
            throw new ArgumentException("缺少消息类型。");
        foreach (var field in root.EnumerateObject())
        {
            var expected = field.Name switch
            {
                "enabled" or "anonymous" or "saves" or "servers" or "inherit" or "autoMemory" or "autoChinese" => JsonValueKind.True,
                "ram" or "width" or "height" => JsonValueKind.Number,
                "dataSources" or "settings" => JsonValueKind.Object,
                "items" or "projects" => JsonValueKind.Array,
                _ => JsonValueKind.Undefined
            };
            if (expected == JsonValueKind.True && field.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || expected is JsonValueKind.Number or JsonValueKind.Object or JsonValueKind.Array && field.Value.ValueKind != expected)
                throw new ArgumentException("消息字段类型错误：" + field.Name);
            if (expected == JsonValueKind.Undefined && field.Name != "items" && field.Value.ValueKind != JsonValueKind.String)
                throw new ArgumentException("消息字段必须是字符串：" + field.Name);
        }
    }

    public static string InstancePath(string root, string version)
    {
        if (string.IsNullOrWhiteSpace(version) || version is "." or ".."
            || version.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("无效的实例名称。");
        return Path.Combine(root, "versions", version);
    }

    public static string ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    public static string OfflineUuid(string player)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes("OfflinePlayer:" + player));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

}
