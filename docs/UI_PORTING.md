# 前端 UI 移植说明

本文件记录 Tauri 版前端对 `origin/`（迁移前的 Avalonia 实现）的 UI 移植约定、目录结构与进度。只移植界面，不迁移业务逻辑。

## 1. 范围与原则

- **只做前端**：页面布局、样式、交互状态；`src-tauri` 仅提供命令契约与内存演示数据，不包含真实业务实现。
- **视觉对齐 `origin`**：颜色、字号、圆角、间距来自 `origin/src/MCUGLauncher.Avalonia/Styles/Theme.axaml`，统一收敛到 `src/styles/tokens.css`。
- **页面与外壳分离**：外壳负责顶部悬浮胶囊导航与全局提示；页面只负责自己的内容。
- **数据边界固定**：页面不直接拼接 `invoke`，统一调用 `src/api/commands/*`；全局状态统一从 `useLauncher()` 读取。
- **每完成一页提交一次**，提交信息为 `feat: 移植<页面>页面 UI`。

## 2. 目录结构

```
src/
├── api/
│   ├── invoke.ts        # invoke 包装：非 Tauri 环境提示、错误归一化
│   ├── commands/        # 按领域拆分的命令封装（每个函数一次 invoke）
│   │   ├── state.ts     # 状态与实例选择
│   │   ├── launch.ts    # 启动
│   │   ├── servers.ts   # 服务器收藏与查询
│   │   ├── accounts.ts  # 账号、头像与 MUA
│   │   ├── settings.ts  # 设置、Java、数据源、诊断
│   │   ├── saves.ts     # 存档备份
│   │   ├── tasks.ts     # 任务队列
│   │   ├── games.ts     # 运行中游戏
│   │   ├── content.ts   # 搜索、版本、下载、收藏
│   │   ├── instances.ts # 克隆/导出/卸载/更新/启动设置
│   │   └── community.ts # 公告与推荐
│   └── window.ts        # Tauri 窗口操作（最小化/最大化/关闭）
├── components/
│   ├── Field.tsx        # 表单行：标题 + 控件 + 说明
│   ├── Markdown.tsx     # 公告用轻量 Markdown 渲染
│   ├── Modal.tsx        # 居中弹层，Esc 关闭
│   └── PathPrompt.tsx   # 路径输入弹层，替代原生文件对话框
├── config.ts            # 应用版本等常量
├── pages/
│   ├── HomePage.tsx         # 启动主页（角色卡 + 右下角启动按钮）
│   ├── DiscoverPage.tsx     # 发现：服务器收藏、公告、推荐、语录
│   ├── DownloadPage.tsx     # 下载：搜索/原版/加载器/整合包
│   ├── SettingsPage.tsx     # 设置：11 个分区
│   ├── ExtrasPage.tsx       # 「更多」容器：左侧分区导航
│   ├── InstancesPage.tsx    # 实例维护
│   ├── AccountPage.tsx      # 账号与皮肤
│   ├── TasksPage.tsx        # 下载任务
│   ├── GamesPage.tsx        # 运行中的游戏
│   ├── BookmarksPage.tsx    # 我的收藏
│   └── CommunityPage.tsx    # 社区管理
├── shell/
│   ├── Shell.tsx        # 标题栏 + 顶部导航 + 页面宿主 + 后端错误提示
│   ├── Shell.css
│   ├── navigation.ts    # 跨页导航（账号入口）
│   └── toast.tsx        # ToastProvider / useToast
├── state/
│   └── launcher.tsx     # 命令驱动的状态容器：提供 useLauncher() 与 refresh*
├── styles/
│   ├── tokens.css       # 设计令牌（对应 Theme.axaml 的色值与圆角）
│   ├── base.css         # 重置、字体、细滚动条
│   └── components.css   # 按钮/表单/卡片/标签页/列表/弹层等公共样式
└── types/
    └── launcher.ts      # 与 src-tauri/src/model.rs 一一对应
```

## 3. 样式约定

- 颜色、圆角、阴影只允许用 `tokens.css` 的变量；页面 CSS 不写死十六进制色值（`HomePage` 的少量卡片色除外，后续统一）。
- 公共控件用 `components.css` 的类：`.btn`（`.primary`/`.soft`/`.ghost`/`.link`/`.topnav`/`.icon`/`.launch`/`.avatar`）、`.card`、`.input`、`.select`、`.tabs`/`.tab`、`.list`/`.list-item`、`.progress`、`.modal-*`、`.toast`。
- 排版类：`.page-title`（22 Bold）、`.h2`（15 Bold）、`.muted`（10.5）、`.small`（10）、`.status`（10.5）。
- 页面骨架：`.page`（20/3/20/20 内边距 + 纵向 flex）、`.page-scroll`（滚动区）、`.subpage`（「更多」内的子页，无额外内边距）。
- 滚动条为 6px 细线，滑块 4px，悬停/按下颜色与 `App.axaml` 的 `ScrollBar` 模板一致。
- 外壳：`.page-stage` 承载页面，`plain`（启动页，无边框）与 `panel`（其余页面，统一的大圆角内容栏，`margin: 64px 20px 20px`、`padding: 14px`）两种形态；栏体画在 `::before` 上，避免面板自身成为 fixed 子元素的包含块。
- 过渡：页间切换由 `Shell` 的 `phase`（out/in）控制——与启动页互切时整块淡出淡入，其余页互切只淡 `.page-fade`；栏内分区切换用 `src/shell/fade.ts` 的 `useFadeSwap` + `.fade-swap.is-out`。
- 背景：`.shell` 用 `src/assets/TmpBackground.png` 铺满（`center / cover`）；卡片式构件（`.card`/`.modal-card`/浮层/胶囊/`.server-card`/`.player-card`）统一用 `--app-glass` 或 `--app-glass-strong` + `--app-glass-grain` 噪点做磨砂亚克力，无描边。

## 4. 页面移植对照

| 目标文件 | 原实现 | 状态 |
| --- | --- | --- |
| `shell/Shell.tsx` | `ShellWindow.axaml` + `.cs` | 已完成（悬浮胶囊导航；窗口按钮交由系统标题栏，Toast 由 `toast.tsx` 提供） |
| `pages/ExtrasPage.tsx` | `Views/ExtrasView.axaml` | 已完成（左侧分区导航） |
| `pages/HomePage.tsx` | `Views/HomeView.axaml` + `.cs` | 已完成（右下角「角色 · 实例 · 启动」三钮；左栏账号卡已移除） |
| `pages/DiscoverPage.tsx` | `Views/HomeView.axaml` 右栏 | 已重构（大圆角栏内左侧分区导航 + 右栏内容，群友名言常驻右栏底部） |
| `pages/DownloadPage.tsx` | `Views/DownloadView.axaml` + `.cs` | 已完成（搜索/详情/版本、原版分页、加载器、整合包） |
| `pages/SettingsPage.tsx` | `Views/SettingsView.axaml` + `.cs` | 已完成（11 个分区：启动/Java/数据源/外观/服务器/存档/恢复/日志/诊断/社区/关于） |
| `pages/InstancesPage.tsx` | `Views/InstancesView.axaml` + `.cs` | 已完成（模组、启动设置、克隆/导出/卸载/更新） |
| `pages/AccountPage.tsx` | `Views/AccountView.axaml` + `.cs` | 已完成（账号卡、模式切换、MUA 登录/角色、本地皮肤） |
| `pages/TasksPage.tsx` | `Views/TasksView.axaml` + `.cs` | 已完成（任务列表、进度、暂停/继续/取消/删除） |
| `pages/GamesPage.tsx` | `Views/GamesView.axaml` + `.cs` | 已完成（运行中游戏列表、关闭游戏） |
| `pages/CommunityPage.tsx` | `Views/CommunityView.axaml` + `.cs` | 已完成（管理员密钥、公告列表与全文、推荐维护） |
| `pages/BookmarksPage.tsx` | `Views/BookmarksView.axaml` + `.cs` | 已完成（收藏列表、版本选择安装、取消收藏） |

以上 11 个页面已全部移植完成，每页对应一次独立提交；`origin/` 中仅剩 `LocalImportDialog`、`FileDropView`、`AnnouncementWindow` 与微软登录窗口未纳入本轮。

## 5. 运行与构建

```bash
npm install          # 首次
npm run dev          # 浏览器预览（http://localhost:1420）
npm run build        # tsc + vite 构建，提交前必须通过
npm run tauri dev    # 桌面窗口（Windows 需 Rust 工具链）
```

窗口为无边框模式（`decorations: false`），标题栏使用 `data-tauri-drag-region` 拖动，按钮调用 `src/api/window.ts`。

## 6. 与后端的衔接

前端**只用 Tauri 命令**访问后端，完整清单见 [BACKEND_CONTRACT](BACKEND_CONTRACT.md)。

1. 页面动作调用 `src/api/commands/*` 中的函数，每个函数对应一次 `invoke`；
2. `src/state/launcher.tsx` 是命令驱动的状态容器：启动时用命令取数，写操作后由页面调用 `refresh*` 重新取数；
3. 不订阅事件；全局同步器每 3 秒调用 `list_tasks` / `list_game_sessions`，页面不重复轮询；
4. `src-tauri` 中已有按领域拆分的命令骨架（`commands/*.rs`）与内存演示数据（`demo.rs`）；
5. 重写内核时替换 `demo.rs` 与命令实现，保持命令名、参数与返回类型不变，前端无需改动。

新增一个功能的标准步骤：

```
src-tauri/src/commands/<domain>.rs 新增/替换 #[tauri::command] 并注册到 lib.rs
src-tauri/src/model.rs      新增/复用 DTO（camelCase）
src/api/commands/<domain>.ts 增加 invoke 封装
src/types/launcher.ts       同步类型
docs/BACKEND_CONTRACT.md    登记命令
```

## 7. 已知差异（后续补齐）

- 公告正文使用轻量 Markdown（标题/列表/引用/段落），不是完整 Markdown 渲染器。
- 文件与目录选择使用 `PathPrompt` 路径输入弹层，待接入 Tauri dialog 插件后替换为原生对话框。
- 拖拽导入覆盖层（`FileDropView`）、本地导入对话框（`LocalImportDialog`）尚未移植；下载页暂以路径输入提示替代。
- 微软登录窗口（WebView2）属于后端登录流程，不在 UI 移植范围内。
- 窗口尺寸记忆、任务栏图标等桌面特性待接入 Tauri 配置。
- `src-tauri` 当前是演示内核：数据保存在内存，重启即重置；真实实现见 BACKEND_CONTRACT 第 4 节。
