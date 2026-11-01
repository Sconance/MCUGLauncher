namespace BlockLaunch;

/// <summary>
/// 提供 Brand 相关功能。
/// </summary>
internal static class Brand
{
    public const string Name = "MCUG Launcher";

    // Keep the existing storage and credential entropy so upgrades retain accounts and instances.
    public const string StorageDirectory = "BlockLaunch";
}
