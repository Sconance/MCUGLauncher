# 多游戏目录与实例定位

## 行为与兼容

复用 `AppConfig.GameRoots` 保存目录列表，`Root` 保存当前目录；当前目录与 `LastInstance`/`LastVersion` 共同组成已选实例，不另存一份可能失配的选择状态。
配置重载保留断开磁盘的 Root，扫描返回不可用原因，不静默切换到默认游戏目录。
添加已有绝对路径，规范化 `.`/`..`、末尾分隔符，按运行平台比较路径并去重；允许空游戏目录作为后续安装目标。
扫描只枚举各根目录的 `versions`，不递归搜索整块磁盘。没有版本的目录正常返回空列表；损坏元数据或不可访问目录单独返回错误。
移除只注销记录，不删除任何目录或文件；至少保留一个目录。已排队任务保持原快照，即使目录记录随后移除，也不会改成当前目录。

`InstanceTarget(Root, Name)` 明确定位实例；两个根目录内相同名称属于不同实例。
显式目标必须包含两个字段，且目录在管理列表中；不把空字段当成当前选择。
`LauncherState.GameDirectories` 保存所有目录及各自实例、错误，实例 DTO 的 `Root` 为所属根目录。
旧 `LauncherState.Instances`、`VersionGroups`、`Mods` 保留当前目录语义，避免旧页面将同名实例混淆。

## 接口

所有新增入口都是 `ILauncherService` 上的默认强类型功能方法，由 FeatureDispatcher 自动发现 handler，不增加消息桥分支。

```csharp
ILauncherService service = /* 现有注入的服务 */;
await service.AddGameDirectoryAsync(@"D:\Minecraft");
await service.AddGameDirectoryAsync(@"E:\Games\.minecraft");
var directories = await service.ScanGameDirectoriesAsync();
var target = new InstanceTarget(directories[0].Root, directories[0].Instances[0].Name);
await service.SelectManagedInstanceAsync(target);
var result = await service.LaunchManagedInstanceAsync(target);
// 指定服务器只覆盖本次启动参数：
var serverResult = await service.LaunchManagedInstanceAsync(target, "play.example:25565");
```

- `ScanGameDirectoriesAsync()`：复制目录列表，在后台线程扫描并返回结果；可独立手动刷新。
- `AddGameDirectoryAsync(root, select=false)`、`RemoveGameDirectoryAsync(root)`：保存配置并刷新状态。
- `SelectManagedInstanceAsync(target)`：校验实例后同时保存目录和实例键。
- `LaunchManagedInstanceAsync(target, serverAddress=null)`：复用 GameCommands 和 Core；地址为 null 时保留实例原启动设置。
- `CloneManagedInstanceAsync(target,name,saves,servers)`、`ExportManagedInstanceAsync(target,path,servers,saves)`：冻结实例快照并提交现有持久队列。
- `PreviewManagedUninstallAsync(target)`：返回路径预览 token；随后仍通过原有 `ApplyUninstallAsync(token)` 经过原生确认。
- `ListManagedModsAsync(target)`、`ToggleManagedModAsync(target,file,enabled)`、`DeleteManagedModAsync(target,file)`：仅操作明确目标内枚举出的模组文件。

启动程序、添加/移除目录、安装完成及 RequestState 刷新时，状态构建扫描所有目录。无实时文件监听。

## 收藏服务器

`SavedServer.GameRoot`、`SavedServerInfo.GameRoot` 与 Instance 共同固定目标。
旧配置加载时将已绑定收藏的空 GameRoot 固定为当时的 Root，不要求离线目录在线。
`SaveServerAsync(..., instance, gameRoot)` 支持明确目录；省略参数的旧调用保留既有绑定目录，清空实例则清空目录。
旧主页按 Instance/Address 调用 `LaunchToServerAsync` 时，后端按规范化服务器地址和实例名找到收藏绑定目录，避免切换 Root 后误启动同名实例。
显式 `LaunchManagedInstanceAsync` 则始终使用传入目录，不用收藏或当前选择替代。

## 组件与逐成员依赖

路径规范化和比较是跨组件领域能力，放在 Core.GameDirectoryPath，不依赖应用配置或 UI。
GameDirectoryCatalog 必须连接既有 VersionCatalog 的 Models 与返回给前端的 Contracts DTO，因此属于 B 类适配组件；不能将应用 DTO 下沉 Core，也不另写一套扫描器。功能适配器复用现有流程，其余业务不新增 Manager。
其余流程优先复用原组件。SettingsCommands 使用超过五个成员属于既有设置协调职责；路径规则已下沉 Core，不再创建转发 Manager。

| B 组件 | 实际依赖成员 |
| --- | --- |
| GameDirectoryCatalog | `AppConfig.Root/GameRoots`；`VersionCatalog.Load(string,string).Instances`；Models.InstanceInfo 的 Name/Loader/Mods/Updates/DisplayName/Minecraft/IsPlaceholder；Contracts.GameDirectoryInfo/InstanceInfo 构造函数和 Root；Core.GameDirectoryPath.Normalize(string)/Comparer |
| InstanceSnapshot | `AppConfig.Root/GameRoots/LegacyPreferences.Launch/AutoMemory/Ram/Java/Width/Height`；`GameDirectoryCatalog.RequireRoot(AppConfig,string)`；`InstanceRequirements.Read(string,string)`；Core.InstanceCatalog.GetOrCreate(string)/DirectoryFor(string)；实例描述的 DisplayName/Launch；LaunchSettings 克隆 |
| LauncherStateBuilder | `AppConfig` 已有状态字段；`GameDirectoryCatalog.Roots(AppConfig)/Scan(AppConfig,CancellationToken)`；`VersionCatalog.ReadMods(string,string)`；Contracts.LauncherState、VersionGroupInfo、VersionChoice、ModInfo、SavedServerInfo、DataSourceSet 构造函数；传入的 FavoriteInfo 列表 |
| SettingsCommands | `LauncherContext.Config/RefreshState/Token`、`Store.Save(AppConfig)`；`GameDirectoryCatalog.Roots(AppConfig)/RequireRoot(AppConfig,string)`；`InstanceSnapshot.Capture(AppConfig,string,string)`；`AppConfig.Servers` 的 Instance/GameRoot；既有 `JavaDiscovery.Scan(string,IEnumerable<string>,CancellationToken)` |
| ProtocolValues | 新增 `ReadInstanceTarget(JsonElement)` 使用 `Contracts.InstanceTarget(string,string)`；原有 BCL 字段与 UUID 校验不变，因此从 A 重新登记为 B |
| InstanceCommands | `LauncherContext.Config/RefreshState/Store.DataDirectory/Store.Save(AppConfig)`；SelectVersion(string)/SelectInstance(string) 保存当前目录内的选择；`VersionCatalog.Load(string,string)/InstanceDirectory(string,string)/ReadMods(string,string)/SetEnabled(ModInfo,bool)/Delete(ModInfo)`；Models.ModInfo.File；`InstanceSnapshot.Capture(AppConfig,string,string)` 的 Root/Key |
| InstanceMaintenanceCommands | `LauncherContext.Config/Tasks.Enqueue(string,string,string,DownloadRequest)`；`InstanceSnapshot.Capture(AppConfig,string,string)` 的 Root/Directory/Name/Requirements/Request()；`ProtocolValues.ReadString(JsonElement,string)` |
| UninstallCommands | `LauncherContext.Config/Games.UsesDirectory(string)/Token/Tasks.Enqueue(string,string,string,DownloadRequest)`；`InstanceSnapshot.Capture(AppConfig,string,string)` 的 Root/Key/Settings/Directory/Name/Request()；`PreviewCache<Confirmation>.For(object)/Add(Confirmation)/Get(string,bool)`；Core.InstanceUninstaller.Plan(string,string,LaunchSettings) |
| GameCommands、CommunityCommands | 现有详细依赖分别在 GameCommands.md、CommunityCommands.md；增加读取 SavedServer.GameRoot/Instance/Address，CommunityCommands 调用 GameDirectoryCatalog.RequireRoot(AppConfig,string)，GameCommands 的 Capture 接收显式目录 |

`GameDirectoryFeatureHandlers.cs` 的十一项适配器：

| 类型 | 依赖成员 |
| --- | --- |
| ScanGameDirectoriesFeatureHandler | `LauncherContext.Config.Root/GameRoots`；AppConfig 构造与对应属性；`GameDirectoryCatalog.Scan(AppConfig,CancellationToken)` |
| AddGameDirectoryFeatureHandler | `SettingsCommands(LauncherContext).AddGameDirectory(string,bool)`；`ProtocolValues.ReadString(JsonElement,string)` |
| RemoveGameDirectoryFeatureHandler | `SettingsCommands(LauncherContext).RemoveGameDirectory(string)`；`ProtocolValues.ReadString(JsonElement,string)` |
| SelectManagedInstanceFeatureHandler | `SettingsCommands(LauncherContext).SelectManagedInstance(InstanceTarget)`；`ProtocolValues.ReadInstanceTarget(JsonElement)` |
| LaunchManagedInstanceFeatureHandler | `GameCommands(LauncherContext).LaunchManagedAsync(InstanceTarget,string)`；`ProtocolValues.ReadInstanceTarget(JsonElement)` |
| CloneManagedInstanceFeatureHandler | `InstanceMaintenanceCommands(LauncherContext).Clone(JsonElement,InstanceTarget)`；`ProtocolValues.ReadInstanceTarget(JsonElement)`；SavedInfo 构造函数 |
| ExportManagedInstanceFeatureHandler | `InstanceMaintenanceCommands(LauncherContext).Export(JsonElement,InstanceTarget)`；`ProtocolValues.ReadInstanceTarget(JsonElement)`；SavedInfo 构造函数 |
| PreviewManagedUninstallFeatureHandler | `UninstallCommands(LauncherContext).PreviewAsync(string,string)`；`ProtocolValues.ReadInstanceTarget(JsonElement)` |
| ListManagedModsFeatureHandler | `InstanceCommands(LauncherContext).Mods(InstanceTarget)`；`ProtocolValues.ReadInstanceTarget(JsonElement)`；Models.ModInfo 的 Id/Name/File/Version/Enabled/Path/Size；Contracts.ModInfo 构造函数 |
| ToggleManagedModFeatureHandler | `InstanceCommands(LauncherContext).ToggleMod(string,bool,InstanceTarget)`；`ProtocolValues.ReadString/ReadInstanceTarget(JsonElement)` |
| DeleteManagedModFeatureHandler | `InstanceCommands(LauncherContext).DeleteMod(string,InstanceTarget)`；`ProtocolValues.ReadString/ReadInstanceTarget(JsonElement)` |

以上组件均不依赖窗口、控件、WebView2、LauncherService 内部协议。

## UI 跟进与验收范围

TUI 已通过“游戏目录管理”和“实例与启动”接入：按目录浏览、添加/移除/刷新目录，并向明确目标的启动、克隆、导出、模组和卸载接口传递 InstanceTarget。
编辑自动进服设置时，菜单明确说明先选择目标实例，然后沿用原有启动设置 token 流程；其他明确目标操作不改变当前目录。
Avalonia 页面仍保持按当前目录展示；后续界面任务应消费 GameDirectories，并传递完整 InstanceTarget。
涉及 HomeView 的实例选择/收藏、InstancesView 和 SettingsView 的目录管理；不得仅以名称查找跨目录实例。后端实现与后续 TUI 接入均未修改 Avalonia 页面。
后台任务独立于页面生命周期。安装任务仍按原有入队时选定目录执行，不在执行时重新读取 Root。

检查入口：LauncherChecks 的 `--game-directories-only`，覆盖同名实例、多目录扫描、离线与损坏元数据隔离、路径去重、持久化、收藏目录迁移、模组定向、原生卸载确认、跨等待启动和真实队列导出。
模拟进程与确认拒绝不代替 Windows 上真实游戏、账号、界面和微软登录验收。
