# 业务契约分层

`App/Contracts` 按领域拆分为窄接口；`ILauncherService` 只作为兼容聚合保留，供现有 View 与组合根使用。

## 领域接口

| 接口 | 负责 |
| --- | --- |
| `ILauncherCoreService` | 启动、状态刷新、窗口位置、全局 Toast/错误 |
| `IAccountService` | 微软/MUA/离线账号、登录结果、头像皮肤 |
| `IInstanceService` | 实例/版本选择、启动、启动选项 |
| `IInstanceMaintenanceService` | 克隆、导出、卸载、模组、内容更新 |
| `IDownloadService` | 资源搜索、版本、安装、整合包、本地导入 |
| `ITaskService` | 任务队列查询与暂停/继续/取消/删除 |
| `IGameService` | 运行中游戏、存档备份 |
| `ISettingsService` | 全局设置、Java 扫描、数据源 |
| `ICommunityService` | 公告/推荐、服务器、书签、Key 校验 |
| `IDiagnosticService` | 日志、系统信息、目录、图片下载 |
| `IWindowService` | 文件/文件夹选择、窗口复位 |

## 规则

1. 新代码只依赖所需领域接口，不新增对 `ILauncherService` 成员的调用；
2. `ILauncherService` 不再新增成员；需要扩展时先加到对应领域接口；
3. 事件跟随领域接口，不集中到单一事件类；
4. `FeatureDispatcher` 递归扫描 `ILauncherService` 继承的所有接口，识别 `[FeatureOperation]`；
5. 页面仍不得读取 `AppConfig`、拼 JSON、调用 `HandleAsync`；
6. 契约变更属于业务任务，UI 任务不得直接修改。

## 迁移状态

- 领域接口已拆分；
- `LauncherService` 继续实现聚合接口，行为不变；
- 现有 View 可继续使用 `ILauncherService`，后续逐页迁移到领域接口；
- `LauncherChecks` 已改为从继承接口发现 feature operation。

## 领域模块

账号领域已从 `LauncherService` 抽离为 `UseCases/Modules/Account/AccountModule`：

- `AccountModule` 负责微软/MUA/离线登录、账号切换、MUA 档案、头像皮肤；
- 账号消息（`authResult`、`accountState`、`accountSwitchResult`、`muaProfiles`）由模块处理；
- `LauncherService` 只保留事件转发与方法委托，`IAccountService` 契约不变；
- `AccountFeatureHandlers` 与模块同目录，后续新账号能力优先加在模块内。

其他领域模块按同样方式逐步抽离，保持 `ILauncherService` 聚合兼容。

## 账号模块边界（已完成）

```text
Modules/Account/
  AccountModule.cs            方法/事件门面
  AccountFeatureInvoker.cs    适配 LauncherApplication，断开与 LauncherService 的环
  IAccountContext.cs          账号模块最小上下文
  IAccountStore.cs            账号记录持久化端口与 AppConfig 适配器
  AccountCommands.cs          登录用例
  AccountModeCommands.cs      账号切换/忘记/恢复
  MuaProfileSelection.cs      MUA 角色选择
  AccountFeatureHandlers.cs   FeatureHandler
```

- `AccountModule` 只依赖 `IAccountFeatureInvoker` 和 `Contracts`，不再引用 `LauncherService`；
- 账号命令只依赖 `IAccountContext`，不再接收完整 `LauncherContext`；
- `SelectMuaProfileAsync` 已改为 `[FeatureOperation] Task<SavedInfo>`，删除 JSON 命令委托；
- `LauncherContext` 实现 `IAccountContext`，并用 `NextRevision()` 统一账号版本号递增；
- `AccountModule` 持有运行时账号状态 `AccountStateInfo`，通过 `StateChanged`/`AccountStateChanged` 发布；`IAccountService.AccountState` 暴露当前快照；
- `AccountModeCommands.Apply` 统一委托 `AccountModule.Apply`，账号模式、玩家名、会话与持久化写入只有一个入口；
- `AppConfig` 仍是持久化载体，模块写入后镜像到配置，保证旧配置与启动流程兼容；
- `LauncherStateBuilder` 优先使用模块状态，模块未装配时回退配置。

## 账号模块 P0/P1/P2 进展

- P0：`IAccountContext` 已移除 `Accounts`，新增 `IAccountStateWriter`，`AccountModeCommands` 通过写入端口提交状态，不再直接访问模块；
- P0：`AccountModule.StateChanged` 已删除，只保留 `AccountStateChanged`；
- P0：账号 `FeatureHandler` 改为注入 `IAccountContext`，`FeatureDispatcher` 支持 `LauncherContext`/`IAccountContext` 两种构造；`AccountCommands` 显式接收 `IAccountStateWriter`；
- P1：新增 `IAccountStateProvider` / `IAccountSessionProvider`，`LauncherStateBuilder` 通过状态提供者读取账号快照；
- P2（部分）：`AccountStateInfo` 扩展为 `Player/AccountType/OfflinePlayer/Authenticated/Revision`，保留默认参数兼容旧构造；
- P1：账号契约改为纯抽象方法，UI 只走 `LauncherService → AccountModule → IAccountFeatureInvoker` 单一调用路径；
- 启动会话：`AccountModule.PrepareLaunchSessionAsync` 统一生成离线会话、刷新在线会话并校验玩家名，`GameCommands` 不再自行构造 `MSession`；
- 会话：`OnlineSession` 已从 `LauncherContext`/`IAccountContext` 移除，`IAccountSessionStore` 由 `AccountModule` 唯一实现，启动与账号命令通过会话存储读写；
- P2：新增 `IAccountStore`/`ConfigAccountStore`，`AccountModule` 不再直接读写 `AppConfig`/`ConfigStore`，账号记录的旧字段兼容集中在适配器；`persist=false` 时仍更新内存配置，由调用方异步落盘。
