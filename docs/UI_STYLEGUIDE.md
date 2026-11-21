# MCUG Launcher 前端样式规范

这份文档描述界面里已经稳定下来的表面、间距和组件写法。新增页面时先复用这里的类，
不要在页面里另写一套颜色、圆角或阴影。

## 1. 基本原则

- 颜色、圆角、阴影只从 `src/styles/tokens.css` 取，页面里不写新的十六进制色。
- 内容卡片用 `.card`；导航胶囊和浮窗用 `.glass-surface` 的组合类；弹窗用 `.modal-card`。
- 页面只负责组织和间距，滚动、分区切换、淡入淡出交给 `SplitPage` / `Shell`。
- 设置页的每个分区是独立组件，状态放在分区内部，共享数据走 `SettingsDataContext`。

## 2. 设计令牌

完整列表见 `src/styles/tokens.css`，常用的有：

| 令牌 | 用途 |
| --- | --- |
| `--app-ink` / `--app-ink-2` / `--app-ink-3` | 标题、正文、弱说明 |
| `--app-accent` / `--app-accent-dark` / `--app-accent-deep` | 主色、按下态、深色文字 |
| `--app-glass` | 内容卡片的浅玻璃 |
| `--app-glass-strong` | 导航胶囊、浮窗的强玻璃 |
| `--app-glass-blur` / `--app-glass-grain` | 模糊与噪点，两者总是一起用 |
| `--radius-card` / `--radius-panel` / `--radius-pill` | 卡片 12px / 面板 18px / 胶囊 999px |
| `--shadow-card` / `--shadow-nav` / `--shadow-dialog` | 卡片 / 悬浮表面 / 弹窗 |

文字层级：页面标题 `.h2`，卡片标题也是 `.h2`；操作结果 `.status`；空态和关于信息才用
`.muted`。设置项的说明不写成正文段落，必要说明收进 `SettingHint`。

## 3. 四类表面

| 类名 | 用在哪 | 不要用在哪 |
| --- | --- | --- |
| `.card` | 设置卡片、列表容器、内容块 | 导航栏、浮窗 |
| `.glass-surface.glass-pill` | 顶部导航胶囊、右上角状态按钮组 | 大块内容 |
| `.glass-surface.glass-panel` | 右上角状态浮窗、悬浮面板 | 普通卡片 |
| `.modal-card` | 居中弹窗（Java 选择、路径选择） | 页面内联内容 |

`.glass-surface` 只提供材质（强玻璃、噪点、模糊、`--shadow-nav`），圆角由
`.glass-pill` / `.glass-panel` 决定。按钮组本身需要定位时，在业务 CSS 里写
`position: relative` / `z-index`，不要重复写背景和阴影。

内容卡片不要套 `.glass-surface`：卡片的玻璃更浅，套上强玻璃后层级会变重。

## 4. 布局骨架

- 普通页面：`.page` > `.page-head` + `.page-scroll`（或 `.page-status`）。
- 左右分栏页面（设置、发现、下载、账户）：

```text
.extras.split-page
├─ .extras-nav          // 左侧 .section-nav，分组圆角块
└─ .split-body
   ├─ .split-content    // 右栏唯一滚动层，设置页再加 .settings-scroll
   └─ .page-status      // 可选，常驻在滚动区下方
```

- 设置页的分区内容放在 `.settings-scroll` 里；一个分区有多张卡片时用
  `.settings-cards`（纵向、12px 间距），单张卡片用 `.card.stack.settings-card`
  （卡片内部 10px 节奏）。
- 左栏宽度由 `.extras` 的 184px 栅格决定，不要在页面里写死。

## 5. 控件

- 按钮：`.btn` + `.primary` / `.soft` / `.ghost` / `.link`。默认高度 34px。
- 开关：`button.switch`（`role="switch"`、`aria-checked`）内放
  `<span className="switch-thumb" />`，开启时加 `.on`。
- 分段选择：`.segmented` > 两个 `button.segmented-option`，选中项加
  `.is-selected`，容器用 `role="radiogroup"`，选项用 `role="radio"`。
- 帮助标签：`<SettingHint tip="…" />`（`src/pages/settings/SettingHint.tsx`），
  圆形问号，悬停/聚焦时在右侧显示说明；有 DEV 等标签时放在标签之后。
  说明必须必要且简短，能直接删掉的描述不要加。
- 跳转条目：`NavItem` 加 `jump: true`，左栏条目会显示靠右的 `SvgIcon`
  （`src/components/SvgIcon.tsx` + `src/assets/icons/ui/jump.svg`）；点击不切当前分区，
  由调用方跳到目标页面（例如下载页的「收藏」跳到发现）。
- 图标：组件里的单色图标统一用 `SvgIcon` 渲染 `src/assets/icons/ui/*.svg`，
  不要在任何 `.tsx` 里内联 `<svg>` / `<path>`。`SvgIcon` 通过 CSS mask 使用
  `currentColor` 着色，默认 16×16；彩色图标不适用，继续用资源文件 + `<img>`。
- 表单：`.field` > `.field-label` + 输入控件 + `.field-hint`。
- 一行放输入框和按钮：`.settings-actions`（自动换行、8px 间距，输入框吃掉剩余宽度）。
- 一行混放复选框和多个字段：`.launch-numbers`（1200px 以下自动降为两列）。
- 操作结果：`.status`，不要用红色/绿色硬编码；需要错误态时沿用现有状态类。

## 6. 设置分区组件写法

设置页的 `SettingsPage.tsx` 只保留左侧导航和 `SplitPage`。每个分区放在
`src/pages/settings/<Name>Settings.tsx`，约定：

1. 接收 `{ active: boolean }`，组件常驻挂载，`active` 为 false 时 `return null`。
   这样切换分区不会丢掉未保存的输入。
2. 分区自己的输入、请求和 `useState` 放在组件内部；共享的 `overview/settings/accounts/
   resourceVersions/refreshSettingsResources` 从 `useSettingsData()` 取。设置写入成功后按响应的 `changed` 刷新资源。
3. 多张卡片用 `<div className="settings-cards">` 包起来，卡片用
   `SettingsCard`，行用 `SettingRow`。`SettingsCard` 不再接受说明段落：
   卡片级必要信息放在标题里的 `SettingHint`，其余直接删掉。
4. 操作结果用 `useSettingsStatus()` 的 `say(key, message)` / `statusAt(key)`，
   key 按「分区:卡片」命名（如 `maintenance:saves`），避免不同卡片的提示互相覆盖。
5. 新增分区时同步改 `SettingsPage.tsx` 的 `NAV_GROUPS` 和分区组件，
   并确认左栏图标来自 `src/assets/icons/nav/`。

当前分区：外观、启动、社区、社区管理、维护、开发者选项、关于与许可。

## 7. 提交前检查

- `npm run build` 通过，TypeScript 没有新增错误。
- 页面里没有新增硬编码颜色；新表面优先复用 `.card` / `.glass-surface`。
- 设置页七个分区都能切换，切换后未保存的输入仍在。
- 新浮层使用 `.modal-card` 或 `.glass-surface.glass-panel`，不要自己写阴影。
