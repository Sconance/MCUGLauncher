# 类型化应用边界迁移

参考 `0.22.3-Avalonia` 的 `08f2d847fef0a85d70e838682335a24b175d647a`。本轮在现有 `0.24.0-codex` 上适配，没有 rebase、改写参考分支，也没有用参考布局覆盖产品布局。

## 调用方向

`Views → Contracts.ILauncherService → UseCases.LauncherService → 既有 Commands / Services → Core`

- Contracts：语义化状态、请求、结果和事件，不引用控件或可变 AppConfig。
- LauncherService（C）：统一装配、转发与回执关联；旧 JSON 命令仅留在 App 内部兼容层。按 requestId/token 关联结果，失败抛出异常，认证 pending 不误作完成，结束后解除临时订阅。
- LauncherStateBuilder（B）：读取 AppConfig 的账号、目录、启动设置、数据源和 LegacyPreferences；调用 VersionCatalog.Load/ReadMods；接收 BookmarkCommands.Snapshot 的资源快照，直接构造 LauncherState。
- BookmarkCommands（B）：保留原存储和切换行为，Snapshot 改为 ResourceInfo[]，不返回匿名协议对象。
- SettingsCommands / LaunchOptionsCommands / LauncherApplication：复用 Contracts.LaunchLimits 的内存、尺寸和名称边界；业务流程沿用已有组件。
- UiContext：纯展示状态，保存导航、头像缓存和提示；不存储业务配置，不构建命令。
- ShellDesktopHost：系统选择器只返回路径，业务回执由 App 生成。

## 与参考实现的差异

本分支有更多已实现功能，因此契约保留自动简体中文、网络和外观设置、数据源保存与使用分离、拖拽预览确认、安装回执和请求隔离。需要成功后关闭弹窗的操作返回明确结果；状态刷新和领域事件独立发布。没有把按钮对应业务重新写到视图里。

社区未知字段由 App 以不透明元数据保留，避免编辑时丢失；视图不解析 JSON。状态快照复制配置值和集合，后续配置修改不会反向改变已发布的快照。

## 验证

LauncherChecks 使用真实本地用例，检查状态、配置持久化、独立快照、自动中文、错误回执及服务器操作。NativeUiChecks 检查页面入口、资源点击、拖拽确认、弹窗完成和失败行为、布局及滚动。WebBridgeChecks 保留内部协议兼容性回归；其测试夹具为历史断言补 type，不影响产品契约。

边界脚本检查 UI 无通用命令/JSON接口，Contracts 无 UI 或可变配置依赖，App/Core 不反向依赖前端。静态检查不能证明所有实际账号、网络和游戏流程正确；本轮不宣称已完成真实微软/MUA账号或游戏验收。

本轮实际结果：LauncherChecks、MuaChecks、DownloadChecks、QueueChecks、CompatibilityChecks、WebBridgeChecks、NativeUiChecks 均通过；格式、布局、版本、原生架构边界检查通过。已查看 home-small 与 resource-detail 的 Skia 截图。
