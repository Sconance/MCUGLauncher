# ConfigStore（B）

职责：选择应用配置路径、补全无效字段、保存配置。Core 不应知道应用默认目录，因此保留本适配。

- AppConfig.Root/GameRoots/Player/OfflinePlayer/AccountType/LastVersion/LastInstance：读取、补全本地配置。
- AppConfig.Java/Ram/Width/Height/LegacyPreferences：同步既有 Preferences 兼容字段。
- Core Preferences.Root/Player/Java/Ram/Width/Height/LastVersion：接收兼容值。
- Core AtomicFile.ReadJson<AppConfig>(string)/WriteJson<AppConfig>(string,AppConfig)：读快照和原子提交。

没有其他服务、Commands 或窗口依赖。无效 JSON 或 null 文件保留 .broken 副本后返回默认配置。写入采用 Core 的唯一同目录临时文件、持久化刷新、原子替换和失败清理；不再维护第二套固定 .tmp 写入。

异步保存补充：SaveAsync(AppConfig) 在调用线程同步兼容字段并创建 JsonElement 独立快照；线程池只处理快照，通过同一 writeGate 调用 AtomicFile.WriteJson<JsonElement>。同步与异步保存共用提交序号，已提交的新快照不会被较旧任务覆盖。后台不持有可变 AppConfig。关闭窗口的同步保存同样参与排序。
