# 前端 UI 移植说明

本文件记录 Tauri 版前端对 `origin/`（迁移前的 Avalonia 实现）的 UI 移植约定、目录结构与进度。只移植界面，不迁移业务逻辑。

## 1. 范围与原则

- **只做前端**：页面布局、样式、交互状态与本地演示数据；业务由后续的 Tauri command 提供。
- **视觉对齐 `origin`**：颜色、字号、圆角、间距来自 `origin/src/MCUGLauncher.Avalonia/Styles/Theme.axaml`，统一收敛到 `src/styles/tokens.css`。
- **页面与外壳分离**：外壳负责标题栏、导航和全局提示；页面只负责自己的内容。
- **数据边界固定**：页面不直接读假数据文件，统一调用 `src/api/launcher.ts`；接入后端时只替换该文件的实现。
- **每完成一页提交一次**，提交信息为 `feat: 移植<页面>页面 UI`。

## 2. 目录结构

```
src/
├── api/
│   ├── launcher.ts      # 业务门面：当前返回演示数据，之后替换为 invoke(...)
│   └── window.ts        # Tauri 窗口操作（最小化/最大化/关闭），浏览器下静默降级
├── components/
│   ├── Field.tsx        # 表单行：标题 + 控件 + 说明
│   └── Modal.tsx        # 居中弹层，Esc 关闭
├── config.ts            # 应用版本等常量
├── data/
│   └── mock.ts          # 演示数据（接入后端后删除）
├── pages/
│   ├── HomePage.tsx         # 启动主页
│   ├── DownloadPage.tsx     # 下载：搜索/原版/加载器/整合包
│   ├── SettingsPage.tsx     # 设置：11 个分区
│   ├── ExtrasPage.tsx       # 「更多」容器：左侧分区导航
│   ├── InstancesPage.tsx    # 实例维护
│   ├── AccountPage.tsx      # 账号与皮肤
│   ├── TasksPage.tsx        # 下载任务
│   ├── GamesPage.tsx        # 运行中的游戏
│   ├── BookmarksPage.tsx    # 我的收藏
│   ├── CommunityPage.tsx    # 社区管理
│   └── PagePlaceholder.tsx  # 未移植页的过渡占位
├── shell/
│   ├── Shell.tsx        # 标题栏 + 顶部导航 + 页面宿主 + Toast
│   ├── Shell.css
│   └── toast.ts         # ToastContext / useToast
├── styles/
│   ├── tokens.css       # 设计令牌（对应 Theme.axaml 的色值与圆角）
│   ├── base.css         # 重置、字体、细滚动条
│   └── components.css   # 按钮/表单/卡片/标签页/列表/弹层等公共样式
└── types/
    └── launcher.ts      # 与 App/Contracts 对应的前端模型
```

## 3. 样式约定

- 颜色、圆角、阴影只允许用 `tokens.css` 的变量；页面 CSS 不写死十六进制色值（`HomePage` 的少量卡片色除外，后续统一）。
- 公共控件用 `components.css` 的类：`.btn`（`.primary`/`.soft`/`.ghost`/`.link`/`.topnav`/`.icon`/`.launch`/`.avatar`）、`.card`、`.input`、`.select`、`.tabs`/`.tab`、`.list`/`.list-item`、`.progress`、`.modal-*`、`.toast`。
- 排版类：`.page-title`（22 Bold）、`.h2`（15 Bold）、`.muted`（10.5）、`.small`（10）、`.status`（10.5）。
- 页面骨架：`.page`（20/3/20/20 内边距 + 纵向 flex）、`.page-scroll`（滚动区）、`.subpage`（「更多」内的子页，无额外内边距）。
- 滚动条为 6px 细线，滑块 4px，悬停/按下颜色与 `App.axaml` 的 `ScrollBar` 模板一致。

## 4. 页面移植对照

| 目标文件 | 原实现 | 状态 |
| --- | --- | --- |
| `shell/Shell.tsx` | `ShellWindow.axaml` + `.cs` | 已完成（标题栏、导航、Toast、窗口控制） |
| `pages/ExtrasPage.tsx` | `Views/ExtrasView.axaml` | 已完成（左侧分区导航） |
| `pages/HomePage.tsx` | `Views/HomeView.axaml` + `.cs` | 已完成（账号卡、实例选择、服务器、公告、推荐、语录） |
| `pages/DownloadPage.tsx` | `Views/DownloadView.axaml` + `.cs` | 已完成（搜索/详情/版本、原版分页、加载器、整合包） |
| `pages/SettingsPage.tsx` | `Views/SettingsView.axaml` + `.cs` | 已完成（11 个分区：启动/Java/数据源/外观/服务器/存档/恢复/日志/诊断/社区/关于） |
| `pages/InstancesPage.tsx` | `Views/InstancesView.axaml` + `.cs` | 已完成（模组、启动设置、克隆/导出/卸载/更新） |
| `pages/AccountPage.tsx` | `Views/AccountView.axaml` + `.cs` | 已完成（账号卡、模式切换、MUA 登录/角色、本地皮肤） |
| `pages/TasksPage.tsx` | `Views/TasksView.axaml` + `.cs` | 已完成（任务列表、进度、暂停/继续/取消/删除） |
| `pages/GamesPage.tsx` | `Views/GamesView.axaml` + `.cs` | 已完成（运行中游戏列表、关闭游戏） |
| `pages/CommunityPage.tsx` | `Views/CommunityView.axaml` + `.cs` | 待移植 |
| `pages/BookmarksPage.tsx` | `Views/BookmarksView.axaml` + `.cs` | 待移植 |

## 5. 运行与构建

```bash
npm install          # 首次
npm run dev          # 浏览器预览（http://localhost:1420）
npm run build        # tsc + vite 构建，提交前必须通过
npm run tauri dev    # 桌面窗口（Windows 需 Rust 工具链）
```

窗口为无边框模式（`decorations: false`），标题栏使用 `data-tauri-drag-region` 拖动，按钮调用 `src/api/window.ts`。

## 6. 与后端的衔接

`src/api/launcher.ts` 中每个方法对应一个 `ILauncherService` 强类型调用。接入时：

1. 在 `src-tauri` 注册 command；
2. 把方法体替换为 `invoke<T>("命令名", 参数)`；
3. `data/mock.ts` 删除，`types/launcher.ts` 保持与 `App/Contracts` 同步。

页面不感知这一步，`launcher` 之外不出现后端调用。

## 7. 已知差异（后续补齐）

- 公告正文使用纯文本展示，原实现用 Markdown 渲染。
- 右键「收藏」菜单、拖拽导入覆盖层、原生文件对话框尚未移植。
- 微软登录窗口（WebView2）属于后端登录流程，不在 UI 移植范围内。
- 窗口尺寸记忆、任务栏图标等桌面特性待接入 Tauri 配置。
