# MCUGLauncher

MCUGLauncher 是基于 Tauri、React 与 TypeScript 的 Minecraft 启动器前端。UI 仅通过 Tauri `invoke` 契约访问 Rust 后端，不直接读写本地业务数据，也不依赖事件才能完成状态同步。

当前仓库提供内存演示后端，用于验证 UI 与 V2 契约。尚未接入的安装、启动、在线认证及部分系统能力会返回结构化 `UNSUPPORTED`，不会伪造执行成功。

## 开发

安装依赖前，请查看[开发环境要求](docs/DEVELOPMENT_ENVIRONMENT.md)，确认 Node.js、Rust 和桌面平台依赖。

```bash
npm install
npm run tauri dev
```

仅构建前端：

```bash
npm run build
```

重新生成并检查 DTO：

```bash
npm run dto:generate
npm run dto:check
```

检查 Rust 后端：

```bash
cargo test --manifest-path src-tauri/Cargo.toml --lib
cargo check --manifest-path src-tauri/Cargo.toml
```

## 契约文档

- [当前后端契约](docs/BACKEND_CONTRACT.md)
- [V2 状态与迁移方案](docs/BACKEND_V2.md)
- [UI 迁移说明](docs/UI_PORTING.md)
- [DTO JSON Schema](schemas/launcher.schema.json)
