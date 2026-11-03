# 0.24.0-codex：AI 迁移框架尝试

## 基线

产品基线是 `origin/0.22.3-codex` 的 `6c091c6`。此前从 `origin/0.22.3-Avalonia` 的 `7f1dc70` 复用框架适配和 App/Core 拆分后，错误地重排了页面，不能把祖先关系当成产品对齐证明。本轮按 [对照修订](UI_PARITY_0.22.3-codex.md) 恢复位置与遗漏入口。保留历史以便追溯，没有合入 `0.24.0-deepseek.zip`。本次不触碰 0.23.x，也不修改 Avalonia 起步分支。

## 迁移范围

| 原页面/行为 | 原生入口 | 仍使用的后端 |
|---|---|---|
| 账号卡、实例分组、目录选择、搜索、启动 | HomePage | selectVersion/selectInstance/launch/saveSettings |
| 正版/离线/MUA、角色选择、头像、移除账号 | AccountDialogs / CommunityOptionsPage | AccountCommands/AccountModeCommands/MuaProfileSelection/AvatarCommands |
| 原版与加载器安装 | DownloadPage/InstallDialogs | ResourceCommands/InstallationCommands |
| 资源搜索、收藏、详情、版本/实例/目录选择 | DownloadPage/ResourceDialog/BookmarksPage | ResourceChoiceCommands/BookmarkCommands |
| 整合包导入确认、改名与目录 | InstallDialogs | previewPack/confirmPack |
| 全局/单实例启动设置与继承 | LaunchOptionsPage | LaunchOptionsCommands |
| Mod 启用禁用删除、克隆导出、更新预览、卸载预览 | InstancesPage/MaintenanceDialogs | InstanceCommands/InstanceMaintenanceCommands/ContentUpdateCommands/UninstallCommands |
| 下载整体进度、暂停、继续、取消、历史删除 | TasksPage/顶部进度 | LauncherTaskQueue |
| 独立游戏会话与关闭确认 | GamesPage | RunningGameRegistry/GameCommands/IDesktopHost |
| 公告、推荐、语录、配图、服务器状态 | CommunityPage/ServersPage/RemoteImage | CommunityCommands/CommunityService/ServerPingService |
| 本地公告与推荐编辑 | EditorPage | verifyKey/publish/saveAnnouncements/saveRecommendations |
| 默认/自定义源、自动/手动测试、保存与使用分离 | SourcesPage | SettingsCommands/CommunityCommands |
| Java、内存、目录、匿名、日志、系统信息 | SettingsPage/JavaPage/UtilitySettingsPage | 现有相应 Commands |
| 存档备份与恢复 | SavesPage | SaveCommands/Core SaveProtection |

不迁移旧前端中仅用于浏览器演示的假统计、假版本、假操作提示。后端原本未实现的功能也不会因为换框架而宣称完成，例如游戏内离线皮肤/披风上传、整合包升级合并。资源详情展示实际搜索结果描述和兼容版本，未凭空补造远端评价或截图。

## 架构

ShellWindow 是组合入口；功能页面在 Views 中，控件构造留在视图。LauncherFacade 负责类型化快照、消息适配、账号头像缓存和通知。它内部仍调用原有 JSON 协议；本次没有宣称已经把全部后端改成强类型接口，也没有把所有业务搬进一个大 ViewModel。

NavigationCatalog 是菜单注册点，新增栏目提供 Id/Label/Group/Create，可选 IconPath（avares URI）。无图片时显示符号，未来替换图片不用改变业务。

App/Core 继续负责下载与安装，不依赖视图。新增 readImage 使用原有网络策略、有界读取、允许的 HTTPS 图片主机、超时；控件只显示 PNG/JPEG，限制字节和尺寸。

主界面不再复制或打包旧 Ui 目录；WebUiChecks 被 NativeUiChecks 替代。WebView2.Avalonia 只用于微软官方授权页面，因此当前包仍可能需要 WebView2 Runtime 才能进行该登录。窗口本身、菜单和功能页均为 Avalonia。

## 兼容与限制

- 配置与任务历史使用 `%LOCALAPPDATA%/MCUGLauncher`。当前名称统一修订包含账号存储标识及实例元数据文件名变化，相关缓存需要重新登录或重新建立，见 [命名与数据位置](NAMING.md)。
- 测试 EXE 必须单独命名；本次不覆盖正式或旧 codex EXE。
- Windows x64 实验，Core 仍含 Windows 进程/路径能力，不宣称跨平台。
- 没有自动合并其他版本的新功能；本分支基线及限制在 README 明示。
- 现有本机管理员验证仍沿用旧后端，不等同于服务器端身份授权。
- 已存在的本地公告优先于远端公告的策略仍沿用后端，本次没有改写同步冲突策略。

## 验证

NativeUiChecks 在真正的 Avalonia 控件树与 Skia 渲染器上运行，使用模拟应用响应：

- 所有菜单可创建，账号切换发出正确命令；
- 数据源自动/手动测试、独立保存与启用；
- 空任务进度和旧模组空数字兼容；
- 资源选定版本发送正确 ID，任务暂停仍由应用处理；
- 960×640、1280×800、1920×1080 渲染并输出截图。

App/Core 仍运行 MuaChecks、DownloadChecks、QueueChecks、CompatibilityChecks、WebBridgeChecks。真实 Microsoft/MUA 登录、真实 Minecraft 多开、复杂存档、实际安装和本机文件对话框还需要人工验收。请勿将这些自动化检查描述为“保证没有 Bug”。


### 实际窗口冒烟

2026-11-02 使用独立 `--data-directory` 打开 Windows 原生窗口，检查首页、设置、数据源默认/自定义页签。四项真实 HTTPS 数据源测试返回连接结果，公告通过原生文本控件显示。没有执行账号登录、游戏安装、存档恢复或进程终止；正式配置与 EXE 不变。

确认契约在本分支改为 ConfirmStopGameAsync / ConfirmUninstallAsync；应用分发 await 确认结果，取消不会执行关闭或卸载。所有原生确认和文件选择都由 Avalonia 提供，不再使用自写 Win32 消息框。

单文件 Lite 已在独立空目录启动；据此补齐空数据源与空实例列表边界，空目录不再注入假实例。数据源启用后重新拉取对应内容，停用/无效源不保留上一源的远端列表。
