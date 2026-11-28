# 前端类型 SDK（`@mcugl/frontpack-sdk`）

> 状态：v0.1.0，已独立发布到 npm（`@mcugl/frontpack-sdk`），源码维护在 `Ecmon-mc/mcugl-buildtool` 的 `sdk` 分支。
> 目标读者：写第三方前端的作者、维护内核方法的开发者。
> 相关文档：[KERNEL_API_V1.md](KERNEL_API_V1.md)（接口契约）、[FRONTEND_PACKAGE.md](FRONTEND_PACKAGE.md)（打包与分发）、[DEVELOPMENT_ENVIRONMENT.md](DEVELOPMENT_ENVIRONMENT.md)（开发环境）。

## 1. 它解决什么

`mcugl.kernel/v1` 已经是稳定的运行期契约：66 个具名命令、snake_case 命令名、camelCase 参数、`ApiError` / `SavedInfo` / 资源快照。SDK 把这份契约变成 TypeScript 类型和一层很薄的传输封装：

- 命令名、参数、返回值都有类型；写错参数在编译期报错，而不是运行期 `UNSUPPORTED`。
- 错误统一成 `KernelError`（`code` / `message` / `retryable` / `fieldErrors`）。
- 不引入运行时依赖，不打包 UI 框架，不改变现有 wire 协议。

SDK **不**替代内核：它只是前端侧的调用入口，与官方前端的 `src/api/invoke.ts` 访问的是同一组 Tauri 命令。

## 2. 使用

```ts
import { createKernel, KernelError } from "@mcugl/frontpack-sdk";

const kernel = createKernel();

if (!kernel.available) {
  // 不在 MCUGL Launcher 内（普通浏览器、SSR、测试环境）
  showError("请通过 MCUGL Launcher 打开这个前端");
}

const overview = await kernel.getOverview();
const manifest = await kernel.manifest();
const runtime = kernel.runtime(); // window.__MCUGL__；官方前端为 null

try {
  await kernel.launchGame({ instanceId, accountId });
} catch (error) {
  if (error instanceof KernelError) {
    // code: VALIDATION_ERROR / NOT_FOUND / CONFLICT / BUSY / AUTH_REQUIRED /
    //       NETWORK_ERROR / IO_ERROR / PLAN_EXPIRED / INTERNAL_ERROR / UNSUPPORTED
    console.error(error.command, error.code, error.message);
    if (error.fieldErrors) showFieldErrors(error.fieldErrors);
  }
}
```

通过 npm 安装使用：

```bash
npm install @mcugl/frontpack-sdk
```

在本仓库中可校验已安装 SDK 与 Rust 内核方法表是否一致：

```bash
npm run sdk:check
```

## 3. 类型与常量

| 导出 | 说明 |
| --- | --- |
| `createKernel(options?)` | 创建客户端；`options.transport` 可替换传输实现。 |
| `kernel.<method>(...args)` | 66 个内核方法各有一个 camelCase 方法，如 `kernel.getOverview()`、`kernel.launchGame({...})`。 |
| `kernel.call(name, ...args)` | 按命令名调用：无参命令不传第二个参数，全可选参数的命令可省略；与上面完全等价。 |
| `kernel.manifest()` | 等价 `kernel.call("kernel_manifest")`，返回 `KernelManifest`。 |
| `kernel.runtime()` | `window.__MCUGL__`（`apiVersion` / `protocol` / `frontend`），官方前端为 `null`。 |
| `KERNEL_METHODS` | 66 个内核方法名（与 `kernel_manifest.methods` 同序）。 |
| `METHOD_NAMES` | 命令名 → `kernel` 方法名（如 `get_overview` → `getOverview`），显式表以免压缩器改名。 |
| `HOST_COMMANDS` | 宿主管理命令：`kernel_manifest`、`list_frontends`、`install_frontend`、`use_frontend`、`remove_frontend`、`reload_frontend`、`read_frontend_icon`。 |
| `CommandArgsMap` / `CommandResultMap` | 每个命令的参数与返回类型。 |
| `Snapshot<T>` | `{ epoch, revision, data }` 资源快照。 |
| `KernelError` | 统一错误类型。 |

`src/generated.ts` 由 `npm run dto:generate` 从 `schemas/*.schema.json` 生成，与启动器前端的 `src/types/generated.ts` 同源，**不要手改**。

## 4. `kernel` 上的方法（推荐）

66 个内核方法都挂在 `kernel` 上，方法名是命令名的 camelCase 形式。它们是 `kernel.call` 的绑定版本，**没有额外逻辑**：同样的类型、同样的参数、同样的错误。

```ts
import { createKernel } from "@mcugl/frontpack-sdk";

const kernel = createKernel();

const overview = await kernel.getOverview();            // get_overview
const tasks = await kernel.listTasks();                 // list_tasks
const pick = await kernel.pickFile({ title: "选择" });   // pick_file（参数可选）
await kernel.launchGame({ instanceId, accountId });      // launch_game（参数必填）
await kernel.queueContentVersion({ token, versionId });  // queue_content_version
```

如果更习惯「命令名 + 参数」或需要动态命令，`kernel.call` 仍然完全可用，两者可以混用：

```ts
const overview = await kernel.call("get_overview");
const tasks = await kernel.call("list_tasks");
```

| 形式 | 适用场景 |
| --- | --- |
| `kernel.getOverview()` | 推荐；类型提示最直接，无需记命令名。 |
| `kernel.call("get_overview")` | 动态命令、通用调试面板、按命令名配置的批量工具。 |
| `getOverview(kernel)` | 具名包装（66 个函数，首参 `kernel`），导入即用；等价于 `kernel.getOverview()`。 |
| `METHOD_FUNCTIONS[command](kernel, ...)` | 按命令名查表，键与 `KERNEL_METHODS` 同序。 |
| `METHOD_NAMES[command]` | 命令名 → `kernel` 方法名；不要用 `Function.name` 推导。 |

函数/方法命名是命令名的 camelCase 形式（`get_overview` → `getOverview`，`queue_content_version` → `queueContentVersion`）。宿主管理命令（`list_frontends` 等）不在 66 个方法内，继续用 `kernel.call(...)`。

## 5. 传输与演示模式

`createKernel()` 默认读取 `globalThis.__TAURI_INTERNALS__`。可以传入自定义 transport：

```ts
const kernel = createKernel({
  transport: {
    invoke: async (command, args) => mockKernel[command](args),
  },
});
```

注意：启动器的 Vite 演示模式（`npm run dev:demo`）是在 `src/api/invoke.ts` 内部拦截，**不会**注入 `__TAURI_INTERNALS__`。因此：

- 在官方前端里，演示模式正常；
- 在第三方前端里，若想在浏览器里脱离桌面壳预览，需要自己传一个 mock transport；
- SDK 不内置演示后端（保持零依赖、零副作用）。

## 6. 兼容与维护

- SDK 的方法表必须与 `src-tauri/src/commands/kernel.rs` 的 `METHODS` 一致；`npm run sdk:check` 会逐项比对并失败。
- 内核新增命令时：更新 Rust、`kernel.rs` 的 `METHODS`、`commands.ts` 的 `CommandArgsMap` / `CommandResultMap` / `KERNEL_METHODS`，再跑 `npm run sdk:check`。
- 破坏性改动按 [KERNEL_API_V1.md](KERNEL_API_V1.md) 的规则升 `apiVersion` 主版本；SDK 的主版本跟随内核 `apiVersion`。
- `package.json` 目前是 `private`；发布策略（独立 npm 包或随启动器分发）确定后再改。

## 7. 与 buildtool 的关系

buildtool 的四个模板（`vanilla`、`simple`、`vite-react`、`vite-react-simple`）目前各自带一份手写的 `kernel.ts` 桥接。后续计划把模板改为依赖本 SDK：

1. `vite-react` / `vite-react-simple`：在 `package.json` 里引用 SDK（`file:` 或发布的包），删除手写桥接；
2. `vanilla` / `simple`：把 `dist` 里的类型与客户端以 ESM 形式 vendor 进模板，保持零构建；
3. 演示模式：模板可传入一个小的 mock transport，让 `npm run dev` 在浏览器里也能看到界面。

这一步尚未开始；按当前约定，buildtool 的改动**暂不推送**。
