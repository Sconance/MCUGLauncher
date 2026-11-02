# 路线图

## 0.22.3-Avalonia：原生界面迁移（已完成主体）

- 主界面已从 **WebView2 + HTML/CSS/JS** 迁移到 **原生 Avalonia**：
  - `Views/HomeView`、`DownloadView`、`InstancesView`、`AccountView`、`GamesView`、
    `BookmarksView`、`CommunityView`、`SettingsView`；
  - `LauncherFacade` 提供类型化命令与状态事件；
  - `Ui/`、`MainWindow`、`bridge.js`、`WebUiChecks` 与 Web 边界脚本已删除；
  - WebView2 仅保留在 `MicrosoftLoginWindow` 中完成微软 OAuth。
- 已接回下载任务、加载器、整合包导入、实例维护、收藏、存档、社区发布与数据源设置。
- 迁移矩阵与剩余缺口见 [NATIVE_UI_PARITY](docs/NATIVE_UI_PARITY.md)。

## 待办

### 1. 原生界面收尾

- [x] 公告远程 `http(s)` 配图加载（有界下载；本地 data URL 同样支持）。
- [x] 按 0.22.3-fix 前端样式重构顶栏、画布、卡片、按钮、输入框、列表与分段导航。
- [ ] 逐页视觉验收：960×640、1280×800、1920×1080 截图与滚动可达性（多 DPI 待复核）。
- [ ] 将 `ShellWindow` 中剩余的任务页渲染迁移为独立 `TasksView`（如继续拆分）。

### 2. 验收

- [ ] 真实微软/MUA 登录与多角色选择。
- [ ] 真实 Modrinth 下载、加载器安装、整合包导入。
- [ ] 真实游戏启动/多开/关闭与实例卸载。
- [ ] Windows 多 DPI 与键盘操作。

### 3. 发布

- [ ] 版本号、CHANGELOG、README 一致性（`python scripts/check-version.py`）。
- [ ] 发布包不再含 `Ui/` 资源；微软登录窗口可独立完成 OAuth。
- [ ] 合并到发布分支后打标签并记录哈希。

## 审查约定

- 小步提交：一个提交只做一类改动，便于逐条审查。
- 删除旧文件属于迁移的一部分；重点审查 `Views` 的状态与事件、`LauncherFacade` 的命令映射与
  `UseCases` 的数据边界。
- 版本号只改 `Directory.Build.props` 的 `McugVersion`，规则见 [版本号控制](docs/VERSIONING.md)。

## 历史记录

- 0.22.3 / 0.22.3-fix / 0.22.3-codex 的 WebView2 阶段记录保留在
  [整理记录](docs/REVIEW_0.22.3-codex.md) 与 [历史架构快照](docs/archive/0.22.3-fix-architecture.md)。
