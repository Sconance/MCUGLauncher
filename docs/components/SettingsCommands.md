# SettingsCommands（B）

验证全局偏好、保存数据源草稿与启用状态、扫描 Java，不构建界面。

- LauncherContext.Config.Root/GameRoots/Player/OfflinePlayer/AccountType/Anonymous/WriteEndpoint/DataSources/CustomDataSources/DataSourceMode/EffectiveDataSourceMode/AutoMemory/Ram/Java/Width/Height/LegacyPreferences：设置读写。
- LauncherContext.Store.Save(AppConfig)：持久化。
- LauncherContext.Send(object)/RefreshState()：回推结果和快照。
- LauncherContext.Token：取消扫描。
- AppConfig、DataSourceConfig：配置值对象。
- Core JavaDiscovery.Scan(string,IEnumerable<string>,CancellationToken)：扫描运行环境。
- Core NetworkSettings.Validate()：下载源、代理与限速校验。

新增偏好先在克隆值中验证。网络对象原地同步，保持活动 HTTP 策略的源与限速引用；代理处理器须重启生效。删除目录记录不删除磁盘文件。未暴露未实现的自动备份或主题选项。

依赖较多主要来自配置字段，此次没有新增服务，继续复用既有配置入口，避免为每个页面新建管理器。

草稿复制现调用 Core Preferences.Copy()，启动、网络与集合均独立；数据源用 DataSourceConfig.Copy()/HasSameAddresses(DataSourceConfig)，不以 JSON 序列化代替值操作。
