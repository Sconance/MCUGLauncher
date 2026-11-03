# VersionCatalog（B）

本地目录到界面值对象的适配器，不实现版本依赖解析或内容事务。

- Models.VersionGroup(string,int,List<string[]>)、InstanceInfo(string,string,int,int) 及其 DisplayName/Minecraft、ModInfo 与 VersionDetails：输出本地列表。InstanceInfo.Name 用于恢复选择。
- Core ProfileInspector.Read(string,string)：统一基础版本与加载器判定。
- Core InstanceCatalog.MetadataDirectory(string)/DirectoryFor(string)、InstanceDescriptor.DisplayName、AtomicFile.ReadJson<T>(string)、ContentService.SafeFile(string)：目录与显示名读取。
- Core ContentService.Toggle(LocalContent)/MoveToBackup(string,LocalContent)：统一内容锁、依赖检查与备份。

列表遇到损坏或循环继承返回 Complete=false；启动要求读取仍抛出具体错误。模组文件名摘要与旧协议保留，不把本地文件名版本猜测当作真实依赖元数据。
