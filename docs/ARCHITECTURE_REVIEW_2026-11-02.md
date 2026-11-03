# 本分支架构复查（2026-11-02）

范围：0.24.0-codex 的 Core、Services、UseCases、原生壳与 Views。按用户要求不做插件化、动画或图标缩放，不改变菜单布局。

## 已修正

1. 版本列表通过名字包含 fabric/forge 判断加载器，并自写继承遍历；InstanceRequirements 又实现了一套库坐标解析。统一调用既有 Core ProfileInspector.Read，保留整合包安装记录优先。重命名、Quilt 兼容 Fabric、Forge 前缀与循环继承不再各处判定不同。
2. InstanceSnapshot 与 LaunchOptionsCommands 重复序列化 LaunchSettings、重复合并全局字段，后者还复制整个 AppConfig 只为替换根目录。Core 提供明确 Copy，已有 InstanceSnapshot 统一合并默认；保存预览直接传冻结根目录，不复制账号配置。
3. VersionCatalog 模组入口直接改名/删除文件。现在调用既有 Core Toggle/MoveToBackup，获得锁、依赖检查及移除备份；InstanceCommands 复用现有 IsInstanceInUse，阻止修改启动准备中或运行中的实例。旧协议与按钮不变。
4. 边界脚本补查 Core 对 App/界面层的反向引用，防止 Views 直接引入 Services/UseCases。不是仅靠格式或拆文件认定架构合格。

没有新增 Manager、任务队列、下载器或认证流程。旧适配方法保留，减少外部调用变化；移除模组保留备份是本次有意改善的行为。

## 检查与保留

ShellWindow 保持装配/导航；Views 不构建网络客户端、配置存储或下载器。DownloadExecutor 复用 Core 安装器、事务及校验；头像与账号流程仍走现有适配，未为此次整理重写认证。

仍有可改进处：LauncherContext 是较宽的现有协作入口；VersionCatalog.ReadMods 的名称/版本摘要仍来自文件名，不是 jar 元数据解析，不能视为依赖真相。当前没有为了降低行数把这些逻辑搬入新空壳接口，后续应围绕具体功能与测试缩小依赖，而不是宣称全仓库已经完全解耦。

## 验证

WebBridgeChecks 新增：改名加载器、纯名字误判、Quilt、Forge、循环继承、整合包记录优先、启动副本字段与隔离、显式禁用幂等、准备中禁止模组修改、移除保留备份。原有预览目标冻结与任务检查保留。

执行 NativeUiChecks、CompatibilityChecks、ContentSafetyChecks、DependencyChecks、QueueChecks、SourceFormatter、布局、版本与边界检查。测试使用隔离文件和假进程，不代表真实游戏/微软或 MUA 授权验收。
