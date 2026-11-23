# Kernel API v1（`mcugl.kernel/v1`，已冻结）

> 状态：**v1.0.0 已冻结**，P0 实现已落地（`kernel_manifest` + 本地目录加载 + `frontend.json` 校验 + `window.__MCUGL__`）。
> 冻结含义：方法名、参数/返回形状、错误模型、加载契约在 v1 内只增不改；破坏性改动必须升 `apiVersion` 主版本并保留旧版本至少一个发布周期。
> 目标读者：维护者、想完全重写前端的第三方作者。
> 相关文档：[BACKEND_CONTRACT.md](BACKEND_CONTRACT.md)（方法语义）、[BACKEND_V2.md](BACKEND_V2.md)（快照/修订/错误/任务状态机）、[FRONTEND_PACKAGE.md](FRONTEND_PACKAGE.md)（`.mcuglf` 分发格式）、[DEVELOPMENT_ENVIRONMENT.md](DEVELOPMENT_ENVIRONMENT.md)（环境与排查）。

## 1. 目标与非目标

**目标**

1. 内核（Rust）不含任何 UI 假设。第三方可以用任意技术写前端：官方 React、Vue、原生 JS、任意打包器，甚至不需要打包器。
2. 前端只依赖公开、有版本号的接口：方法表、错误模型、能力清单、宿主注入的上下文；不 import 内核 crate，不读内核私有文件。
3. 前端可以在**不重新编译 Rust** 的前提下整体替换，同时保留启动、下载、账号、任务等全部内核能力。
4. 接口变更可协商：`kernel_manifest` 在加载时说明版本与能力，`frontend.json` 声明兼容范围，不兼容时明确报错而不是白屏。

**非目标（v1）**

- 第三方前端不直接获得文件系统、进程、网络原语；只能通过内核方法间接使用。
- 不提供插件级沙箱：第三方前端与官方前端同权限，只应运行作者信任的代码。
- 不支持远程 URL 作为正式加载方式（Tauri 对远程 origin 默认不授予 IPC 权限，需要显式放开白名单；留到后续版本重新评估）。
- 事件流不属于 v1 的完成条件：前端只靠轮询（现有 3 秒同步 + 聚焦刷新）也必须完全可用。

## 2. 分层

```text
┌──────────────────────────────────────────────┐
│ Frontend（官方 React / 第三方任意 Web 应用）  │  L3
├──────────────────────────────────────────────┤
│ Shell（Tauri 宿主：窗口、对话框、前端加载器） │  L2
├──────────────────────────────────────────────┤
│ Kernel API（方法 + manifest + 错误 + 上下文） │  L1
├──────────────────────────────────────────────┤
│ Kernel（配置/账号/实例/启动/内容/任务，无 UI） │  L0
└──────────────────────────────────────────────┘
```

- **L0 Kernel**：领域逻辑与持久化，可独立测试。当前由 `state::Backend` + `commands::*` 承载，业务函数后续继续下沉为普通 Rust API，Tauri 命令只做适配。
- **L1 Kernel API**：唯一对外契约。v1 的传输是 Tauri IPC；方法表本身与传输无关。
- **L2 Shell**：只做宿主职责，不理解业务。窗口在 Rust 里创建（`src-tauri/src/lib.rs`），这是前端可整体替换的前提。
- **L3 Frontend**：官方前端是 v1 的第一个消费者，不是特例；它和第三方前端调同一套方法。

## 3. 契约

### 3.1 方法

- 方法名 = 现有命令名，`snake_case`；参数对象字段 `camelCase`；返回值与 [schemas/](../schemas/) 中的 DTO 一致。
- 语义沿用 V2：可缓存查询返回 `{ epoch, revision, data }`；写入返回 `{ message, changed, entityIds }`；失败统一 `Err(ApiError)`。
- v1 不引入 `kernel.call` 信封：66 个命令直接就是方法表，官方前端与第三方前端走同一条调用路径（`@tauri-apps/api/core` 的 `invoke`，或直接 `window.__TAURI_INTERNALS__.invoke`）。
- 方法表是封闭清单，以 `kernel_manifest.methods` 为准；不在表里的调用会失败。

### 3.2 `kernel_manifest`

唯一一个「元方法」，前端启动后第一步调用。返回示例（camelCase）：

```json
{
  "protocol": "mcugl.kernel/v1",
  "apiVersion": "1.0.0",
  "appVersion": "0.22.3",
  "backend": "demo",
  "transports": ["tauri-ipc"],
  "methods": ["get_overview", "get_settings", "..."],
  "events": [],
  "capabilities": {
    "get_overview": true,
    "save_settings": true,
    "launch_game": false
  },
  "limits": { "planTtlSeconds": 600 },
  "frontend": {
    "mode": "directory",
    "name": "MCUGL 示例前端",
    "version": "0.1.0",
    "entry": "index.html"
  }
}
```

`capabilities` 实际包含全部 66 个方法（示例只列三个）。

- `backend`：`demo` 表示当前是演示数据；真实内核接入后改为对应标识。
- `capabilities`：**逐个方法**的布尔值，`false` 表示该方法当前返回 `UNSUPPORTED`。演示后端有 28 个方法为 `false`（启动、登录、安装、更新、扫描 Java、选择目录、导出实例等）。前端据此隐藏或禁用入口，而不是等用户点了才报错。
- `frontend`：当前挂载的第三方前端；加载官方打包前端时为 `null`。
- `events`：v1 恒为空数组（见 3.4）。
- `appVersion` 与界面显示的产品版本一致，不是 Cargo 清单的脚手架版本。

### 3.3 错误

`ApiError { code, message, retryable, fieldErrors? }` 不变，错误码集合见 [BACKEND_CONTRACT.md](BACKEND_CONTRACT.md)；新增错误码必须先更新该文档。前端应把 `code` 用于分支、`message` 用于展示，不要解析文案。

### 3.4 事件（v1 不提供）

v1 不承诺事件通道：`kernel_manifest.events` 为空数组，前端只靠轮询即可完成全部功能。后续版本会以 `{ topic, seq, at, payload }` 信封（`seq` 单调递增、丢失后可从快照恢复）增加订阅能力，订阅方法届时才进入方法表；在那之前，第三方前端不应该依赖任何事件名。

现有 `launcher://tasks` 等内部事件不属于 v1 契约，删除它们不会被视为破坏性变更。

### 3.5 版本兼容

- `apiVersion` 是 semver；`frontend.json` 的 `kernelApi` 是 semver 范围，加载时由内核校验（`semver::VersionReq`）。
- 规则：**patch** 只修 bug、改文案；**minor** 只增不改（新增方法、新增可选参数、新增返回字段）；**major** 才删/改方法或字段语义。
- 版本不匹配时启动失败并弹窗说明（要求范围 + 当前版本），不会静默换成官方前端——作者要能看见自己的前端为什么没起来。

## 4. 前端加载契约

### 4.1 解析顺序

1. `MCUGL_FRONTEND`：指向本地目录或 `.mcuglf` / `.zip` 包（显式覆盖，开发期用）；
2. 设置页选中的已安装前端（配置字段 `frontend`，来自 `.mcuglf` 导入，见 [FRONTEND_PACKAGE.md](FRONTEND_PACKAGE.md)）；
3. 都没有：使用打包进二进制的官方前端（`frontendDist`）。

同一 identifier 装了多个版本时，加载 semver 最大的版本。切换已安装前端后点「重启前端」（或重启启动器）生效。

`frontend.path`（任意路径的长期配置）仍属后续版本，v1 的位置由上面第 2 条取代。

### 4.2 本地目录与安装包

本地目录（`MCUGL_FRONTEND` 指向目录）与 `.mcuglf` / `.zip` 包走同一套校验和协议：

- `.mcuglf` / `.zip` 会被解压到 `<数据目录>/frontends/<identifier>/<version>/`，运行期不解析 zip；开发期 `MCUGL_FRONTEND` 指向包文件时解压到 `frontends/.dev/`，`mode` 为 `package`。包格式、大小上限与安装位置见 [FRONTEND_PACKAGE.md](FRONTEND_PACKAGE.md)。
- 目录根部必须有 `frontend.json`：

```json
{
  "name": "my-launcher-ui",
  "version": "1.0.0",
  "kernelApi": ">=1.0.0 <2.0.0",
  "entry": "index.html"
}
```

- 校验项：目录可访问且是目录；`frontend.json` 可解析；`name`、`version` 非空；`kernelApi` 是合法 semver 范围且匹配内核 `apiVersion`；`entry` 缺省为 `index.html`，必须是目录内相对路径（不含 `..`、盘符、`\`）且文件存在。
- 校验失败 → 启动流程弹窗显示具体原因并退出（`app.exit(1)`）。不退回官方前端，避免作者误以为自己的前端已加载。
- 加载方式：内核注册自定义协议 `mcugl`，窗口地址指向入口文件，由 `src-tauri/src/frontend.rs` 直接读目录。Windows 与 Android 上实际地址是 `http://mcugl.localhost/<entry>`，其他平台是 `mcugl://localhost/<entry>`（与 Tauri 对自定义协议 origin 的处理一致）。
- 协议处理器行为：
  - 只读；`Cache-Control: no-store`（作者改文件刷新即生效）；
  - 按扩展名给 MIME（html/js/css/json/svg/png/jpg/gif/webp/ico/woff/woff2/ttf/wasm/map/webmanifest/txt，其余 `application/octet-stream`）；
  - 请求路径百分号解码后必须仍在目录内，`..`、符号链接逃逸、非法编码一律 404；
  - 找不到且路径**没有扩展名**时回退入口文件（SPA 路由）；有扩展名则 404，避免把丢失的 `.js` 当 HTML 返回；
  - 根路径与空路径都交给入口文件。
- 依赖、资源、字体、WASM 全走同一目录，不需要网络；`fetch`/`import()` 使用相对路径即可。
- 页面 origin 属于「应用自己注册的自定义协议」，Tauri 判定为本地 origin，因此 **IPC 默认可用**，不需要 `dangerousRemoteDomainIpcAccess`，也不需要 capability 里的 `remote` 白名单（该结论已对照 tauri-2.12.5 `is_local_url` 与 `protocol::origin` 源码确认）。

### 4.3 前端拿到的上下文

窗口加载前由 Shell 注入只读对象（加载目录或 `.mcuglf` 包时有值，用官方前端时为 `null`）：

```js
window.__MCUGL__ = {
  apiVersion: "1.0.0",
  protocol: "mcugl.kernel/v1",
  frontend: { mode: "directory", name: "my-launcher-ui", version: "1.0.0", entry: "index.html" }
};
```

不含令牌、路径、账号等敏感信息；需要更多信息请调 `kernel_manifest` 或 `get_system_info`。前端不应把 `__MCUGL__` 当作授权凭据，它只是自描述。

### 4.4 最小第三方前端

`examples/third-party-frontend/` 是一个无构建、无框架、无 npm 的完整示例：`frontend.json` + `index.html`，用 `window.__TAURI_INTERNALS__.invoke` 调 `kernel_manifest` 与 `get_overview` 并渲染结果。它同时是 P0 的验收对象。

```powershell
# Windows（在 MCUGLauncher 目录）
$env:MCUGL_FRONTEND = "examples\third-party-frontend"
npm run tauri dev
```

看到示例页显示 `apiVersion`、66 个方法、已实现数量与 `get_overview` 快照，即 P0 通过。

### 4.5 功能对等（frontend parity）

第三方前端是「整体替换」，因此要自己承担官方界面的功能入口：

- **覆盖范围**：一个可分发的前端必须覆盖 `kernel_manifest.methods` 里的全部方法，而不是只做几个演示按钮；缺失的方法等于让用户失去对应功能。
- **基线实现**：`mcugl-buildtool/templates/vite-react`（`npx mcugl-buildtool my-frontend -t vite-react`）是参考实现——它用 `src/actions.ts` 把 66 个内核方法 + 4 个宿主命令列成可点击的表单，并在页面上显示 `覆盖数/总数` 与缺失清单。
- **打包期强制**：`mcugl-buildtool` 的 `scripts/pack.mjs` 会读取随包的 `kernel-methods.json`，扫描前端目录里的文本文件，缺方法时拒绝出包并列出缺项；只做原型预览时才允许 `--allow-incomplete`（或 `MCUGL_PACK_ALLOW_INCOMPLETE=1`），产物会带警告。`simple`、`vanilla` 是有意不完整的布局起点；`vite-react-simple` 通过「命令」页覆盖全部 66 个方法，可直接出包。
- **能力降级**：演示后端对 28 个写操作返回 `UNSUPPORTED`；前端应原样展示错误码并说明「当前后端不支持」，不要吞掉或假装成功。

## 5. 官方前端的模组化（配套，不阻塞 v1）

官方前端拆成：

- `src/core/`：启动、路由、内核客户端、主题、i18n；
- `src/modules/<feature>/`：每个业务模块导出 `register(ctx)`，声明路由、导航项、槽位贡献；
- `src/shell/`：布局与槽位渲染。

槽位（slot）是「扩展」而非「重写」：`home.hero`、`instance.actions`、`settings.sections`、`nav.footer` 等。第三方可以只写一个槽位模块，也可以按 v1 整体替换前端——两条路互不冲突。槽位机制在 P3 落地，v1 只保证整体替换可用。

## 6. 非 Tauri 宿主（P2，可选）

方法表与传输解耦后，可增加 `--kernel-server` 模式：监听 `127.0.0.1` 随机端口，JSON-RPC over HTTP + WebSocket 事件推送，启动时把 `{port, token}` 写到临时文件。用途：浏览器调试、自动化测试、编辑器插件、未来非 WebView 前端。安全约束：仅绑定 loopback、一次性 token、进程退出即失效。

## 7. 分阶段

| 阶段 | 内容 | 状态 |
| --- | --- | --- |
| P0 | 冻结本文档；`kernel_manifest`；本地目录加载 + `frontend.json` 校验 + `mcugl` 协议；`window.__MCUGL__`；示例前端 | **已完成**（含方法表漂移测试） |
| P1a | `.mcuglf` 前端包：安装/列表/切换/卸载、打包脚本、设置页 | **已完成**（34 个单元测试通过） |
| P1b | 事件信封与订阅；`@mcugl/kernel-client`（TS，类型来自 schemas）；示例前端接入 SDK | 待办 |
| P2 | `--kernel-server` + HTTP/WS；能力清单按真实后端填充；命令级权限与签名 | 待办 |
| P3 | 官方前端 core/modules/slots 拆分 | 待办 |

P0/P1a 不改任何现有命令的语义，也不改官方前端的调用方式；P1b 才动前端代码。

## 8. 维护约束

1. 新增/删除 Tauri 命令时，必须同步 `src-tauri/src/commands/kernel.rs` 的 `METHODS`；漏改会让 `methods_match_registered_commands` 测试失败（它解析 `lib.rs` 的 `generate_handler!` 注册表）。
2. 新增命令若在演示后端返回 `UNSUPPORTED`，必须加入 `DEMO_UNSUPPORTED`，否则 `capabilities` 会误报为可用。
3. 宿主自己的管理命令（如 `install_frontend`、`list_frontends`、`read_frontend_icon`、`use_frontend`、`reload_frontend`、`remove_frontend`）不属于内核方法表，必须加入 `kernel.rs` 测试里的 `NON_KERNEL_COMMANDS`，否则漂移测试会把它们算进 66 个方法。
4. 破坏性改动：升 `KERNEL_API_VERSION` 主版本、更新本文档与 `frontend.json` 示例、保留旧版本至少一个发布周期。
5. 本文档冻结后，方法语义的细节仍以 [BACKEND_CONTRACT.md](BACKEND_CONTRACT.md) 为准，两者冲突时先修文档再改代码。
