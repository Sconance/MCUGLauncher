# UI 开发指南与文件边界

本指南约束 **MCUG Launcher 原生 Avalonia UI** 的开发，尤其是 AI 自动开发 UI 时的写入范围。

## 0. 强制规则

> **UI 任务只允许修改 `src/MCUGLauncher.Avalonia/` 下的界面、样式与视觉资源。**
>
> **禁止在 UI 任务中自动修改任何业务代码**，包括 `src/MCUGLauncher.App/`、`src/MCUGLauncher.Core/`、共享契约、测试和业务脚本。
>
> 如果视觉实现确实需要业务侧变更，必须 **停止编码，向用户说明原因、涉及接口和影响，并请求确认**。确认前不得修改业务代码，也不得用临时改动绕过。

UI 样式开发的性质是：

> **样式、布局、视觉层次、颜色、字体、间距、动画和展示结构的设计与实现。**

它不是业务规则修改，不是数据模型修改，不是命令/事件语义修改。

---

## 1. 当前边界

```text
Views / ShellWindow
        │  优先依赖领域接口（IAccountService 等）；ILauncherService 为兼容聚合
        ▼
App/Contracts（共享契约，业务侧所有）
        ▼
UseCases.LauncherService（业务实现）
        ▼
LauncherApplication / Commands / Services / Core
```

- **UI 层**：`src/MCUGLauncher.Avalonia/`
- **共享契约**：`src/MCUGLauncher.App/Contracts/`（属于业务侧，UI 只读）
- **业务层**：`src/MCUGLauncher.App/` 其余目录
- **核心层**：`src/MCUGLauncher.Core/`

页面通过领域接口（新代码）或 `ILauncherService`（兼容聚合）获取状态和调用操作。页面不得：

- 读取 `AppConfig`；
- 拼 JSON；
- 调用 `HandleAsync`；
- 直接创建 `LauncherService`（`ShellWindow` 作为组合根除外）；
- 直接调用 `LauncherApplication`、Commands、Services 或 Core 业务类。

---

## 2. 文件权限

### 2.1 允许修改：原生 UI

| 路径 | 允许内容 |
| --- | --- |
| `src/MCUGLauncher.Avalonia/Views/*.axaml` | 页面布局、控件层次、样式类、绑定展示 |
| `src/MCUGLauncher.Avalonia/Views/*.axaml.cs` | 仅展示相关代码：视觉状态、控件创建、格式化、事件转发、调用 `ILauncherService` 的既有 API |
| `src/MCUGLauncher.Avalonia/ShellWindow.axaml` | 顶栏、导航、PageHost、窗口控制、Toast 的视觉结构 |
| `src/MCUGLauncher.Avalonia/ShellWindow.axaml.cs` | 仅窗口/导航/视觉相关：创建 View、窗口尺寸、自定义 chrome、视觉状态 |
| `src/MCUGLauncher.Avalonia/Styles/Theme.axaml` | 颜色、字体、圆角、阴影、按钮/输入框/列表/Tab 等视觉样式 |
| `src/MCUGLauncher.Avalonia/Assets/` | 图标、背景、Logo 等视觉资源 |
| `src/MCUGLauncher.Avalonia/AccountText.cs` | 账号类型到展示文案的映射 |
| `src/MCUGLauncher.Avalonia/AvaloniaAvatarImage.cs` | 头像/皮肤的视觉渲染 |
| `src/MCUGLauncher.Avalonia/App.axaml` | 主题、全局样式资源；不改变启动流程 |
| `docs/images/` | 更新 UI 截图 |

### 2.2 禁止修改：业务代码

以下目录在 UI 任务中 **一律不得修改**：

| 路径 | 原因 |
| --- | --- |
| `src/MCUGLauncher.App/Contracts/` | 共享契约，业务侧定义；新增字段/方法会改变业务接口 |
| `src/MCUGLauncher.App/UseCases/` | 业务流程、命令分发、状态构建 |
| `src/MCUGLauncher.App/Services/` | 配置、队列、版本目录、账号缓存 |
| `src/MCUGLauncher.App/Models/` | 配置/领域模型 |
| `src/MCUGLauncher.App/Authentication/` | 微软/MUA 认证与凭据 |
| `src/MCUGLauncher.Core/` | 下载、安装、启动、实例、整合包核心 |
| `test/` | 行为与回归检查 |
| `scripts/check-*.py` | 边界与版本检查 |

> `App/Contracts` 即使只新增一个“展示用字段”，也属于业务契约变更，必须按第 5 节请求确认，不能由 UI 任务直接改。

### 2.3 需要确认的例外

| 路径 | 规则 |
| --- | --- |
| `src/MCUGLauncher.Avalonia/MicrosoftLoginWindow.cs` | 登录窗口；只允许窗口/样式改动，不得改变 OAuth 流程，改动前说明 |
| `src/MCUGLauncher.Avalonia/MicrosoftLoginWebUi.cs` | OAuth WebView 适配；默认不改，确需改动先确认 |
| `src/MCUGLauncher.Avalonia/MCUGLauncher.Avalonia.csproj` | 仅允许 UI 资源/包引用；任何依赖或构建变更先说明 |
| `src/MCUGLauncher.Avalonia/Program.cs` | 只允许启动/主题相关，不改变业务初始化顺序 |
| 根目录 `AGENTS.md`、`README.md`、`docs/ARCHITECTURE.md` | 文档更新可以，但不得把业务规则写进 UI 指南来绕过确认 |

---

## 3. UI 任务与业务任务的分界

### 3.1 属于 UI 开发的改动

- 调整颜色、字体、字号、行高、间距、圆角、边框、阴影；
- 调整卡片、表格、列表、表单、导航、Tab、空状态、加载状态；
- 调整页面布局、列宽、响应式换行、滚动区域；
- 调整按钮层级、图标、图片、背景、过渡动画；
- 使用已有 `ILauncherService` 状态/事件进行纯展示；
- 为视觉展示移动控件、重命名 `x:Name`（同步更新同 View 的 code-behind）；
- 新增 UI 展示用的文字、标签、提示（不改变业务规则）；
- 更新截图和 UI 文档。

### 3.2 不属于 UI 开发的改动

- 修改 RAM/窗口/实例名称等校验范围；
- 修改占位实例、筛选、排序、业务状态判断；
- 修改下载、安装、启动、卸载、任务队列行为；
- 修改命令、事件、状态 DTO 的字段或语义；
- 修改配置字段、持久化字段、账号/皮肤/社区逻辑；
- 修改 `ILauncherService` 接口；
- 修改 Core/Services/UseCases；
- 为了让页面“看起来能用”而写一套页面内业务逻辑。

### 3.3 样式任务的额外约束

如果用户明确要求“只改样式/布局”，则不得：

- 改变按钮点击后的业务行为；
- 改变事件订阅关系；
- 改变数据请求时机或参数；
- 改变页面导航逻辑；
- 改变状态判断；
- 在 code-behind 中新增业务分支。

布局调整需要移动控件时，可以保留原事件和原服务调用；如果移动导致行为变化，应先说明。

---

## 4. View 的编码约定

### 4.1 数据来源

View 只能通过构造函数/`Attach` 接收 `ILauncherService`：

```csharp
internal partial class ExampleView : UserControl
{
    ILauncherService? service;

    public ExampleView(ILauncherService service) : this()
    {
        Attach(service);
    }

    public void Attach(ILauncherService value)
    {
        service = value;
        service.StateChanged += ApplyState;
    }
}
```

不得从 View 直接 `new LauncherService`，不得引用 `LauncherApplication`。

### 4.2 展示逻辑

允许在 View 中：

- 把 `LauncherState` 映射成文字/颜色/可见性；
- 把 DTO 列表渲染成控件；
- 根据状态显示空态、禁用态、加载态；
- 使用 `LaunchLimits` 做即时输入提示。

不允许在 View 中：

- 决定“是否允许安装/启动/卸载”的业务规则；
- 实现下载、安装、依赖解析；
- 直接访问文件系统改变业务数据；
- 直接发 HTTP；
- 直接改 `AppConfig`；
- 解析 JSON。

### 4.3 事件订阅

新增事件订阅时：

- 只订阅本页面真正需要的领域事件；
- 避免一个页面订阅整个 `ILauncherService` 的所有事件；
- 新增页面时应考虑 `Detach`/取消订阅；当前 View 缓存的单窗口模型不能作为长期依赖；
- 不在事件回调中执行长时间业务操作，交给 `ILauncherService`。

### 4.4 主题与资源

- 颜色、圆角、阴影、字体优先使用 `Styles/Theme.axaml` 资源；
- 避免在多个 View 中硬编码重复颜色；
- 图片缩放使用项目约定的插值设置；
- 不在 View 中引入新的业务资源语义。

---

## 5. 需要业务变更时的流程

### 5.1 先停止

当出现以下情况时，UI 任务必须停止，而不是顺手修改业务代码：

- 需要的字段不在 `LauncherState`/事件 DTO 中；
- 需要的操作不在 `ILauncherService` 中；
- 现有事件无法表达页面状态；
- 需要改变校验范围、筛选规则、业务状态判断；
- 编译错误指向 `App`/`Core`；
- 现有业务行为与 UI 设计冲突。

### 5.2 向用户说明

用下面的模板说明，等待确认：

```text
需要业务侧变更

视觉需求：
- ...

为什么纯 UI 无法完成：
- ...

涉及文件/接口：
- src/MCUGLauncher.App/Contracts/...
- src/MCUGLauncher.App/UseCases/...

建议变更：
- ...

对现有行为的影响：
- ...

可选的纯 UI 替代方案：
- ...

请确认是否允许修改业务代码。
```

### 5.3 未确认前禁止

- 不修改 `App`/`Core`；
- 不新增/修改共享 DTO；
- 不修改 `ILauncherService`；
- 不修改测试来“适配” UI；
- 不用反射、动态类型、`object` 或页面内临时状态绕过契约；
- 不先改业务再“事后说明”。

---

## 6. 开发流程

### 6.1 开始前

1. 阅读 `AGENTS.md` 和本指南；
2. 判断任务属于「样式」「UI 功能」还是「业务变更」；
3. 只读检查相关 `ILauncherService` 契约；
4. 列出计划修改的 UI 文件；
5. 如果计划涉及业务目录，立即停止并请求确认。

### 6.2 修改中

- 只编辑第 2.1 节列出的文件；
- 一次只处理一个视觉/页面目标；
- 不顺手重构业务代码；
- 不顺手改测试；
- 不把业务逻辑复制到 View；
- 保持现有命令、事件和状态语义不变。

### 6.3 完成后

必须运行：

```powershell
dotnet build src/MCUGLauncher.Avalonia/MCUGLauncher.Avalonia.csproj -c Debug
python scripts/check-native-boundaries.py
python scripts/check-layout.py
```

修改页面时实际启动应用并检查：

- 页面能打开；
- 主要控件可见、可滚动；
- 空状态/错误状态不崩溃；
- 960×640 和至少一个较大尺寸下无横向溢出；
- 键盘焦点基本可用。

提交时说明：

- 修改的 UI 文件；
- 未修改业务代码；
- 如果存在业务变更请求，说明尚未修改并等待确认。

---

## 7. 示例

### 允许

```text
把下载结果改成三列卡片
调整设置页侧栏宽度和选中色
给任务空状态加卡片
把按钮主次层级改为参考样式
调整公告卡片间距和字体
```

### 不允许

```text
把 RAM 上限从 65536 改成 131072
把“请安装版本”占位实例改成另一个字符串过滤
修改 LauncherState 增加一个 UI 字段
修改 ILauncherService 增加一个方法
为了空状态在 View 里直接读配置文件
为了排序在 View 里写业务比较逻辑
```

### 需要先确认

```text
UI 需要显示“实例是否被其他进程占用”，但 State 中没有该字段
UI 需要显示任务队列的预计剩余时间，但 TaskInfo 中没有该字段
UI 需要按新的业务规则隐藏某个按钮
```

---

## 8. 提交前检查清单

- [ ] 只修改了 `src/MCUGLauncher.Avalonia/`（以及 `docs/images` 截图）；
- [ ] 没有修改 `App`、`Core`、`Contracts`、`test`、`scripts`；
- [ ] 没有在 View 中新增业务规则；
- [ ] 没有改变服务调用、事件语义、状态字段；
- [ ] 没有读取 `AppConfig` 或直接调用 `LauncherApplication`；
- [ ] 构建通过；
- [ ] 原生边界检查通过；
- [ ] 页面实际启动并检查过；
- [ ] 如果存在业务变更需求，已向用户说明并等待确认。

---

## 9. 一句话总结

> **UI 开发只做“看起来怎样、放在哪里、如何交互展示”；业务做“数据是什么、是否允许、如何执行”。**
>
> **两者之间只经过 `ILauncherService`；UI 任务不得自动越过这条线。**
