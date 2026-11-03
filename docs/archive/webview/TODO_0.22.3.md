> 历史记录，不作为当前开发规范。当前入口见 [架构说明](../../ARCHITECTURE.md)。

# 路线图（0.22.3 及之后）

0.22.3 把界面迁移到 **WebView2 + HTML/CSS/JS**，C# 只保留服务与消息桥接。`0.22.3-fix` 修复了原分支的编译失败、SDK 固定、CI 覆盖与 VS Code 配置问题；当前 `0.22.3-codex` 的分层、验收范围与剩余缺口见 [整理记录](REVIEW_0.22.3-codex.md)。

## 已完成（0.22.3-fix）

- 移除旧 WPF 界面（`Components/`、`Views/`、`Presentation/`、`Composition/`、`Navigation/`、`Builders/`、`Styling/`、`Initialization/` 与旧 Models），`MainWindow` 只保留 WebView2 宿主。
- 保留并继续使用 `Authentication/`、`Services/`、`Models/AppConfig`、`Ui/` 资源；消息协议由 `Ui/host.js`、`UseCases/LauncherApplication` 分发，窗口仅接收消息。
- 恢复 0.22.2 的 .NET SDK 固定版本，修复 `SourceFormatter` 的 CS1705。
- CI 覆盖 `0.22.3` 与 `0.22.3-fix`；退役依赖旧界面的 11 个检查项目与 4 个预览工具。
- 修复 VS Code 的 CS1061/CS0103：设计时构建补 `BuildingInsideVisualStudio`，新增根目录解决方案与工作区设置。

## 0.22.3-codex 已完成

- MainWindow 业务迁出到按职责划分的 `UseCases`；原 HTML/CSS/JS 外观保持不变。
- 新增隔离的 `WebBridgeChecks` 和可执行的窗口/业务边界检查，接入 CI。
- 修复固定模组版本/加载器、快照版本过滤、离线 UUID 与未验证在线账号启动。
- 原生拖动交给 Windows；消息来源限制为本地 UI；下载临时文件独立并清理。
- 开发入口见 [WEBVIEW_UI](WEBVIEW_UI.md)，检查与限制见 [整理记录](REVIEW_0.22.3-codex.md)。

## 待办

### 1. 新架构的测试
- [x] 初步协议级检查覆盖状态、Mod、服务器、社区与配置；继续补真实 WebView2 与异常/并发矩阵。
- [ ] 真实 WebView2 环境回归：页面加载、导航、主题切换、错误提示与更新流程。
- [ ] 保留的 Core/Auth 检查继续在 CI 运行；新增检查项目时同步 `run-checks` 与 CI 列表。

### 2. 文档
- [x] 补 `docs/WEBVIEW_UI.md`：分层、消息协议与检查方式。
- [ ] 清理 `docs/` 下旧 WPF 文档引用；历史材料已归档到 `docs/archive/legacy-wpf/`。
- [ ] README 的截图与功能说明随新界面更新。

### 3. 发布
- [ ] 版本号 0.22.3 的 CHANGELOG/README 一致性（`python scripts/check-version.py`）。
- [ ] 合并到 `main` 后打 `v0.22.3` 标签，从标签构建完整版与 Lite 版并记录哈希。
- [ ] 在 GitHub Releases 上传附件与发布说明（发布说明以 CHANGELOG 对应小节为准）。

## 审查约定

- 小步提交：一个提交只做一类改动（配置、删除、测试、文档），便于逐条审查。
- 删除旧文件属于迁移的一部分，不需要逐个文件评审；重点审查 `MainWindow.xaml.cs` 的消息分发与 `Services/` 的数据边界。
- 版本号只改 `Directory.Build.props` 的 `McugVersion`，规则见 [版本号控制](../../VERSIONING.md)。

## 2026-11-01 报告修复

任务队列、加载器/整合包导入、运行游戏、资源收藏、存档备份恢复、响应式布局与回归检查已在 0.22.3-codex 接入。逐项状态和未完成范围以 [REPORT_FIX_STATUS](REPORT_FIX_STATUS.md) 为准，前面的历史清单不代表新分支已完成所有迁移。

后续维护修订已加入克隆、导出、卸载与内容更新两阶段流程，并拆出前端页面及演示数据。剩余整合包升级合并、旧数据全面迁移与真实游戏验收见 REPORT_FIX_STATUS。
