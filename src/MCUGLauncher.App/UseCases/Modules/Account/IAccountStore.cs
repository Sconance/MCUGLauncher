using MCUGLauncher.Models;
using MCUGLauncher.Services;

namespace MCUGLauncher.UseCases;

/// <summary>账号模块持久化所需的账号字段，不暴露完整 AppConfig。</summary>
internal sealed record AccountRecord(string Mode, string Player, string OfflinePlayer);

/// <summary>账号领域持久化端口；由基础设施适配 AppConfig/ConfigStore。</summary>
internal interface IAccountStore
{
    AccountRecord Load();

    /// <summary>更新内存账号记录；persist=true 时同时写入配置。</summary>
    void Save(AccountRecord record, bool persist = true);
}

/// <summary>把账号记录映射到现有 AppConfig，兼容旧配置字段。</summary>
internal sealed class ConfigAccountStore(AppConfig config, ConfigStore store) : IAccountStore
{
    public AccountRecord Load() => new(config.AccountType, config.Player, config.OfflinePlayer);

    public void Save(AccountRecord record, bool persist = true)
    {
        config.AccountType = record.Mode;
        config.Player = record.Player;
        config.OfflinePlayer = record.OfflinePlayer;
        if (persist)
            store.Save(config);
    }
}
