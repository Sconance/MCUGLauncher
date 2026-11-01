# Avalonia 迁移计划（0.22.3-Avalonia）

## 目标

把 WPF 宿主迁移到 Avalonia 12/11 技术栈，保留现有 WebView2 + HTML/CSS/JS 界面与 `UseCases` 业务层，最终删除 `UseWPF` 与 `System.Windows` 依赖。

迁移分支：`0.22.3-Avalonia`。

## 阶段划分

### 阶段 1：Avalonia 外壳原型（已完成）

- 新增 `src/MCUGLauncher.Avalonia` 项目（`net8.0-windows`），引用 `MCUGLauncher.Core`；
- 使用 **WebView2.Avalonia**（MIT，`1.0.3240.44-preview.250526.1`）承载 WebView2；未使用商业包 `Avalonia.Controls.WebView`（需要 AvaloniaUI 商业许可证）；
- `App`/`MainWindow` 为 Avalonia 版本，加载 `Ui/index.html`，提供最小 `state` 与窗口命令；
- 新增共享 `Ui/bridge.js`：在 `invokeCSharpAction` 可用时把 `chrome.webview.postMessage` 适配到 Avalonia WebView2 的消息通道；WPF 下不生效。

阶段 1 不替换 WPF 主程序，保证现有 `MCUGLauncher` 仍可构建、发布。

### 阶段 2：业务层去 WPF（进行中）

- 新建 `src/MCUGLauncher.App`（`net8.0-windows`，无 WPF）：
  - 迁入 `UseCases/`（22 文件）、`Services/`（13 个非 UI 服务）、`Models/AppConfig`、非 UI 的 `Authentication/`（微软/MUA 服务、DPAPI 存储、认证检查点）；
  - WPF 项目保留 `AvatarRenderer`、`AvatarImage`、`MicrosoftLoginWindow`、`MicrosoftLoginWebUi` 等 UI 适配。
- `AvatarCommands` 改为注入 `Func<byte[]?, string>` 头像编码；WPF 注入 `AvatarImage.Encode`，Avalonia 后续注入自己的实现。
- 新增 `SkinAssets`（地址校验 + 有界下载），归属 App 库，避免 AvatarCommands 依赖 WPF 渲染类。
- WPF、Avalonia 项目均引用 `MCUGLauncher.App`；`AuthChecks` 改为引用 App 库并保留 UI 文件链接。

### 阶段 2b：Avalonia 接入业务层（已完成基础）

- Avalonia `MainWindow` 接入 `LauncherApplication`：`ready` 后转发全部消息，`Send` 走 `CoreWebView2.PostWebMessageAsJson`；
- 实现 Avalonia 版 `IDesktopHost`：Win32 同步确认框、打开/保存/文件夹对话框，窗口命令与拖动；
- 窗口尺寸/最大化状态读写 `AppConfig`；
- 实现 Avalonia 头像编码（`AvaloniaAvatarImage`，PNG 皮肤裁剪 + 史蒂夫默认头像）与微软登录窗口（`MicrosoftLoginWindow`/`MicrosoftLoginWebUi`，WebView2 导航拦截）；
- 尚未完成：窗口圆角/DWM、真实账号与 WebView2 人工验收。

### 阶段 3：Avalonia 主窗口功能对齐

- 接入 `LauncherApplication`/`LauncherContext`，恢复 68 条消息；
- 窗口拖动、缩放、DWM 圆角、窗口状态记忆改用 Avalonia API；
- 登录窗口、文件/目录选择、确认框全部 Avalonia 化；
- 头像、离线皮肤、系统信息等恢复。

### 阶段 4：移除 WPF 与切换发布

- 删除 `src/MCUGLauncher` 的 WPF 文件与 `UseWPF`；
- 用 Avalonia 项目替换主程序（重命名/移动）；
- 更新 `test/*Checks`、`scripts/build.ps1`、CI、文档与发布说明；
- 重新验证 Debug/Release、WebBridge/WebUi 检查与真实 WebView2。

## 已知风险

| 风险 | 说明 |
| --- | --- |
| WebView 控件选择 | Avalonia 官方 `Avalonia.Controls.WebView` 需要商业许可证；当前使用 MIT 的 `WebView2.Avalonia`，API 与 WebView2 基本一致，但维护活跃度需评估 |
| 对话框异步化 | `IDesktopHost` 目前同步，改用 `StorageProvider` 会波及多个 UseCase 调用点 |
| 登录窗口 | 现有微软登录依赖 WebView2 导航拦截，Avalonia 版本需重新实现并验证 |
| 测试 | `AuthChecks` 使用 WPF 图像类型，需改 Avalonia；`WebBridgeChecks` 依赖 WPF 目标需调整 |
| 单文件发布 | Avalonia + WebView2 的原生库更多，需要重新验证单文件解压与体积 |

## 验证

阶段 1：

```powershell
dotnet build src/MCUGLauncher.Avalonia/MCUGLauncher.Avalonia.csproj -c Debug
```

启动后应显示 Avalonia 窗口并加载现有 UI；WPF 主程序构建保持不变。
