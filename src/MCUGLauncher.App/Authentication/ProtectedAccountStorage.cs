using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using XboxAuthNet.Game.Accounts.JsonStorage;

namespace BlockLaunch;

/// <summary>
/// 提供 Protected账号Storage 相关功能。
/// </summary>
public sealed class ProtectedAccountStorage(string file) : IJsonStorage
{
    static readonly byte[] Entropy = Encoding.UTF8.GetBytes("BlockLaunch.Accounts.v1");

    /// <summary>
    /// 读取AsJsonNode。
    /// </summary>
    public JsonNode? ReadAsJsonNode()
    {
        if (!File.Exists(file))
            return null;
        byte[]? plain = null;
        try
        {
            plain = ProtectedData.Unprotect(File.ReadAllBytes(file), Entropy, DataProtectionScope.CurrentUser);
            return JsonNode.Parse(plain) ?? throw new JsonException("Empty account data");
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            throw new AccountStorageException("账号缓存无法解密或已损坏。请在账号管理中清除本地登录，再重新登录。原文件已保留。", ex);
        }
        finally
        {
            if (plain != null)
                CryptographicOperations.ZeroMemory(plain);
        }
    }

    /// <summary>
    /// 写入。
    /// </summary>
    public void Write(JsonNode node, JsonSerializerOptions? serializerOptions)
    {
        var plain = Encoding.UTF8.GetBytes(node.ToJsonString(serializerOptions));
        byte[] encrypted;
        try
        {
            encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(file))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, ".accounts-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(encrypted);
                stream.Flush(true);
            }

            File.Move(temporary, file, true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    /// <summary>
    /// 执行 Forget。
    /// </summary>
    public void Forget()
    {
        Write(new JsonObject(), null);
    }
}

/// <summary>
/// 提供 账号StorageException 相关功能。
/// </summary>
public class AccountStorageException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// 提供 账号LoginException 相关功能。
/// </summary>
public class AccountLoginException(string message, bool requiresInteractive = false) : Exception(message)
{
    /// <summary>
    /// 获取或设置 RequiresInteractive。
    /// </summary>
    public bool RequiresInteractive { get; } = requiresInteractive;
}

/// <summary>
/// 提供 Memory账号Storage 相关功能。
/// </summary>
internal sealed class MemoryAccountStorage : IJsonStorage
{
    public JsonNode? Value
    {
        get;
        private set;
    }

    /// <summary>
    /// 读取AsJsonNode。
    /// </summary>
    public JsonNode? ReadAsJsonNode() => Value?.DeepClone();
    /// <summary>
    /// 写入。
    /// </summary>
    public void Write(JsonNode node, JsonSerializerOptions? serializerOptions) => Value = node.DeepClone();
}
