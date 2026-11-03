# LaunchOptionsCommands（B）

读取、验证并保存全局或实例启动选项，全局页面与实例弹窗共用契约。

- LauncherContext.Config.Root/LegacyPreferences/AutoMemory/Ram/Java/Width/Height：读取与同步全局配置。
- LauncherContext.Store.Save(AppConfig)：保存全局配置。
- LauncherContext.Send(object)/RefreshState()：回推预览和保存结果。
- InstanceSnapshot.Capture(AppConfig,string)/Capture(string,string,LaunchSettings).Settings、GlobalSettings(AppConfig)：读取冻结目标和统一全局默认。
- PreviewCache<Target>.Add(Target)/Get(string,bool)：绑定并消费目标令牌。
- 不再序列化整个 AppConfig 来替换根目录；直接传入预览根目录和启动选项副本。
- Core InstanceCatalog.GetOrCreate/Save、LaunchSettings.SplitArguments：存储与参数校验。

AutoChinese 随 LaunchSettings 读写；旧调用没有该字段时保留当前值。继承模式不复制独立配置。没有窗口、视图或其他 Commands 依赖。
