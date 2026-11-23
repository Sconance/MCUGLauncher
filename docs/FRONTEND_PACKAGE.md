# 前端包（`.mcuglf`）

> 状态：P1 已实现（导入 / 列表 / 切换 / 卸载 + 打包脚本 + 设置页）。命令级权限与签名尚未实现，见第 7 节。
> 相关文档：[KERNEL_API_V1.md](KERNEL_API_V1.md)（接口契约）、[DEVELOPMENT_ENVIRONMENT.md](DEVELOPMENT_ENVIRONMENT.md)（开发环境）。

## 1. 它是什么

`.mcuglf` 是第三方前端的分发格式：一个 **zip 容器**，里面是完整的 Web 前端目录（`frontend.json`、`index.html` 以及随包的 JS/CSS/字体/WASM）。启动器导入后解压到数据目录，按 [KERNEL_API_V1.md](KERNEL_API_V1.md) 的加载规则整体替换界面。

设计取向上和 SJMCL 的 `.sjmclx` 一致：容器就是 zip，作者用任何工具都能查看、审计、重打包；启动器也接受普通 `.zip`（改后缀即可）。

想从零开始写前端，用仓库里的脚手架 `mcugl-buildtool/` 生成一个可选模板的工程（尚未发布到 npm）：

```powershell
node .\mcugl-buildtool\bin\mcugl-buildtool.mjs                          # 真终端里：交互式向导
node .\mcugl-buildtool\bin\mcugl-buildtool.mjs my-frontend            # vanilla，零构建
node .\mcugl-buildtool\bin\mcugl-buildtool.mjs my-frontend -t simple  # 仿官方布局，极简 CSS，零构建
node .\mcugl-buildtool\bin\mcugl-buildtool.mjs my-frontend -t vite-react
node .\mcugl-buildtool\bin\mcugl-buildtool.mjs my-frontend -t vite-react --layout simple
```

向导先问技术栈（vanilla / vite-react），再问界面（dev / simple），组合成 `vanilla`、`simple`、`vite-react`、`vite-react-simple` 四个模板；`-t` 也可以直接写最终模板 id。脚本可用 `--layout dev|simple` 指定界面。
向导第一问选择界面语言（默认英语，可切中文），随后依次询问项目目录、技术栈、界面、前端标识、名称、描述、作者与版本，按键为 ↑/↓、Enter、Esc；脚本可用 `--lang zh` 切换语言。
它不依赖任何第三方 TUI 库，也不需要 `npm install`。

生成工程自带 `scripts/pack.mjs`、`frontend.json` 和 `npm run pack`，可直接用 `MCUGL_FRONTEND` 指向开发、
打包后从设置页导入。详见包内 README。


## 2. 包内结构

```text
my-frontend.mcuglf (zip)
├── frontend.json          # 必需，位于根目录
├── icon.png               # 可选，宿主列表里显示的前端图标
├── index.html             # entry 指向的入口
├── assets/
│   ├── app.js
│   └── style.css
└── …                      # 其余文件原样保留
```

`frontend.json`：

```json
{
  "identifier": "com.example.my-frontend",
  "name": "我的前端",
  "version": "1.0.0",
  "kernelApi": "^1.0.0",
  "entry": "index.html",
  "author": "Example",
  "description": "一句话说明这个前端。",
  "homepage": "https://example.com/my-frontend"
}
```

| 字段 | 必需 | 说明 |
| --- | --- | --- |
| `identifier` | 是（安装包时） | 反向域名标识，全局唯一；决定安装目录，更新/卸载都以它为单位 |
| `name` | 是 | 展示名 |
| `version` | 是 | semver；同一 identifier 可并存多个版本，只有最新版本会被加载 |
| `kernelApi` | 是 | 要求的内核 API 范围（semver），如 `^1.0.0`、`>=1.0 <2` |
| `entry` | 否 | 入口文件，默认 `index.html`；必须是包内相对路径 |
| `author` / `description` / `homepage` | 否 | 展示信息 |
| `icon` | 否 | 图标相对路径，默认 `icon.png`；用于启动器「设置 → 开发者选项」里的前端列表 |

`identifier` 规则（与 SJMCL 的扩展标识一致，禁止路径穿越与 Windows 设备名）：

- 3–128 个字符，只允许 ASCII 字母、数字、`.`、`-`、`_`，且必须以字母或数字开头；
- 不能以点结尾、不能有连续点或空段；
- 不能是 Windows 保留名（`con`、`prn`、`aux`、`nul`、`com1`–`com9`、`lpt1`–`lpt9`）。

### 2.1 图标

前端包可以带一张图标，显示在启动器设置页的已安装列表里：

- 放在包根目录，默认文件名 `icon.png`；也可以用 `frontend.json` 的 `icon` 字段指向别的相对路径（例如 `assets/icon.svg`）；
- 支持 png / jpg / jpeg / webp / gif / svg，文件 ≤ 512 KiB；
- 路径按绝对路径、`..`、盘符或反斜杠解析时视为无效；
- 没有图标（或格式不认识）时，启动器显示内置的方块占位图标，不影响安装与运行；
- 如果 `frontend.json` 显式写了 `icon` 但文件不存在或超过 512 KiB，安装会直接失败并指出该字段——避免作者以为图标生效了。

## 3. 安装位置

数据目录（见 [DEVELOPMENT_ENVIRONMENT.md](DEVELOPMENT_ENVIRONMENT.md) 的「数据目录」）下：

```text
<data>/frontends/<identifier>/<version>/     # 解压后的可运行前端
<data>/frontends/packages/<identifier>-<version>.mcuglf   # 原始包存档
<data>/frontends/.dev/<identifier>-<version>/             # MCUGL_FRONTEND 指向包文件时的开发解压
<data>/frontends/.staging-<pid>-<nanotime>/              # 安装过程中的临时目录
```

- 安装 = 解压到 `.staging-*` → 逐项校验 → 原子 `rename` 到最终目录。失败不会留下半个前端。
- 运行时不解析 zip：`mcugl` 协议始终从解压后的目录读文件，因此作者可以用普通静态资源工具调试。

## 4. 校验规则

导入时逐项检查，任一失败即拒绝安装并返回 `VALIDATION_ERROR`（消息里带具体文件名）：

- zip 结构：只接受 store（0）与 deflate（8）；不支持 ZIP64、加密、符号链接、重复条目；
- 条目名：长度 ≤ 256，禁止前导 `/`、`\`、盘符、控制字符、空段、`.`、`..`、以空格或点结尾；
- 大小：单文件 ≤ 128 MiB，全部解压 ≤ 256 MiB，条目数 ≤ 2048；
- 内容：根目录必须有 `frontend.json`；`identifier`/`name`/`version`/`kernelApi` 必须通过第 2 节与 semver 校验；`entry` 必须存在且在包内；
- 图标：`icon` 显式指定时必须存在、在包内且 ≤ 512 KiB；默认 `icon.png` 缺失不报错，界面回退内置图标；
- 冲突：同一 `<identifier>/<version>` 已存在时拒绝覆盖，返回 `CONFLICT`；要替换请先卸载；
- 内核兼容：`kernelApi` 必须匹配当前 `mcugl.kernel` 版本，否则拒绝，错误里给出要求范围与当前版本。

## 5. 打包

仓库自带零依赖脚本（Node 20+）：

```powershell
npm run frontend:pack                        # 打包 examples/third-party-frontend
node scripts/pack-frontend.mjs <目录> --out dist-frontends/my.mcuglf
```

脚本会：

1. 读取 `<目录>/frontend.json`，校验必填字段与 `identifier` 规则；
2. 递归收集文件（跳过 `.git`、`node_modules`、`dist-frontends`），按名称排序保证产物可复现；
3. 对每个文件算 CRC32，deflate 后更小才压缩，否则 store；
4. 写出 `<identifier>-<version>.mcuglf` 到 `dist-frontends/`，并打印文件数、大小与 **sha256**。

发布时把 `.mcuglf` 与 sha256 一起放到 GitHub Releases / Modrinth；用户导入前可以自行核对哈希。

## 6. 使用

设置页 →「开发者选项」→「前端（第三方 UI）」：

- **导入前端包…**：选择 `.mcuglf` 或 `.zip`，确认「会获得与官方界面相同的权限」后导入；
- **使用**：切换为该前端（写入配置 `frontend` 字段）；
- **切回官方前端**：清空选择；
- **卸载**：删除该版本的目录与包存档；如果卸载的是当前选中的最后一个版本，自动切回官方前端。

切换与卸载后点设置页的**重启前端**立即生效（只重建主窗口，不退出进程）；导入不需要重启。已安装列表会显示包内自带的图标（见 2.1；没有就用内置占位图标）。

如果第三方前端白屏、卡死，或者根本没有提供退出入口，在 Windows 上按 **Ctrl+Alt+C** 可以强制切回官方前端：启动器会清空进程内的 `MCUGL_FRONTEND`、清空配置里选中的前端，然后原地重建主窗口——**进程不退出**，已加载的数据也还在。快捷键由 Rust 侧用 `RegisterHotKey` 注册，不经过页面 JavaScript，所以页面卡死也能触发；若组合键已被其它程序占用，启动日志会打印「无法注册快捷键」。

开发期仍可用 `MCUGL_FRONTEND` 覆盖已安装的选择，且现在既接受目录也接受 `.mcuglf` 文件：

```powershell
$env:MCUGL_FRONTEND = "dist-frontends\com.example.mcugl-showcase-0.1.0.mcuglf"
npm run tauri dev
```

包的优先级：`MCUGL_FRONTEND`（目录或包）> 配置里选中的已安装前端 > 官方前端。启动日志会打印实际加载的模式（`directory` / `package`）与路径。

### 6.1 功能对等（frontend parity）

`.mcuglf` 只约束「怎么装」，不约束「做了多少」。按 v1 的前端规范，一个可分发的前端必须覆盖 `kernel_manifest.methods` 的全部 66 个方法（并额外处理 4 个宿主命令）：

- 参考实现：`mcugl-buildtool` 的 `vite-react` 模板，`src/actions.ts` 即全量清单，页面顶部显示覆盖率与缺失方法；
- 打包期强制：`scripts/pack.mjs` 读取随包的 `kernel-methods.json`，扫描前端目录里的文本文件，缺方法时拒绝出包并列出缺项；原型预览可加 `--allow-incomplete`（或 `MCUGL_PACK_ALLOW_INCOMPLETE=1`），产物会带警告；
- `simple`、`vanilla` 是有意不完整的布局起点，发布前需自行补全（原型阶段用 `--allow-incomplete`）；`vite-react-simple` 的「命令」页可直接调用全部 66 个方法，因此默认可通过覆盖检查；
- 演示后端对 28 个方法返回 `UNSUPPORTED`，前端要展示错误而不是隐藏入口。

## 7. 安全模型（当前阶段）

- **v1 没有命令级权限**：第三方前端与官方前端一样能调用全部 66 个方法（读实例、账号、设置，写设置、启动、安装等）。导入确认框会明确提示这一点。
- 不允许远程 URL 作为前端来源；只从本地文件导入。
- 包内容不执行安装脚本：没有 postinstall、没有 npm 生命周期，只有静态文件 + 浏览器代码。
- 后续计划：命令级 `permissions`（`PERMISSION_DENIED`）、Ed25519 签名与发布者标识、CSP 收紧、`frontend.path` 配置化。签名格式届时在 `frontend.json` 旁增加 `mcugl.sig`，不破坏现有包。

## 8. 排错

| 现象 | 原因 |
| --- | --- |
| 导入报「暂不支持 ZIP64」 | 包 > 4 GiB 或条目数/大小用了 ZIP64 扩展；重新打包或拆分 |
| `CONFLICT` | 同 identifier + version 已安装；先卸载 |
| 「读取 frontend.json 失败」 | 包根不是 `frontend.json`，或打包时多套了一层目录 |
| 切换后重启仍是官方界面 | 启动日志里看 `[mcugl] 前端（…）` 一行；若加载失败会弹窗，并按提示改回官方 |
| 页面白屏但日志正常 | 打开开发者工具看 `mcugl.localhost` 下的资源 404；入口与相对路径要和包内一致 |
