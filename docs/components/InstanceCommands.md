# InstanceCommands（B）

打开选中实例目录；启停或移除已枚举的模组文件。

- LauncherContext.Config.Root/LastInstance：当前实例选择。
- LauncherContext.IsInstanceInUse(string)：阻止修改运行中或准备中的实例。
- LauncherContext.RefreshState()：提交后刷新状态。
- VersionCatalog.Load(string,string).Selected、InstanceDirectory(string,string)、ReadMods(string,string)：解析实例及定位已枚举文件。
- VersionCatalog.SetEnabled(ModInfo,bool)/Delete(ModInfo)：适配 Core 内容操作。
- ModInfo.File/Path：匹配文件与取得其所属目录。

只通过公开契约访问这些成员，无窗口依赖。目录打开保留原行为；模组修改不再直接 File.Move/File.Delete，统一使用 Core 锁、依赖检查和移除备份。
