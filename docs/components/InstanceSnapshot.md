# InstanceSnapshot（B）

冻结启动及实例操作的目录、要求与启动参数。不持有窗口或当前选择的动态引用。

- AppConfig.Root/LegacyPreferences.Launch/AutoMemory/Ram/Java/Width/Height：通过 GlobalSettings(AppConfig) 统一合并默认选项。
- InstanceRequirements.Read(string,string)：要求读取适配，整合包记录优先，其余交给 Core。
- Core InstanceCatalog.GetOrCreate(string)/DirectoryFor(string)、InstanceDescriptor.DisplayName/Launch：实例目标和覆盖设置。
- Core LaunchSettings.Copy()：独立选项副本。
- Core InstanceOperationRequest：输出实例操作请求。

Capture(string,string,LaunchSettings) 接收显式根目录，预览保存不必复制整个 AppConfig（含账号配置）。保留 Capture(AppConfig,string) 与 SettingsFor(AppConfig,InstanceDescriptor) 既有调用契约。没有新增服务依赖。
