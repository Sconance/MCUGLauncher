# DTO JSON Schema

`schemas/*.schema.json` 按数据模型用途拆分为领域契约。新增或修改 DTO 时先更新对应 Schema，再运行生成命令；禁止在前端或 Rust 中重新手写同名传输字段。

Schema 采用 JSON Schema Draft 2020-12，统一约束 camelCase 字段、资源快照、结构化错误、任务状态、游戏会话和登录判别联合。

## 生成 DTO

安装依赖后执行：

```bash
npm run dto:generate
```

该命令从 Schema 生成 `src/types/generated.ts`。提交前执行：

```bash
npm run dto:check
```

`dto:check` 会重新整理领域 Schema、补齐字段说明、生成 TypeScript 类型并检查 Git diff，确保生成文件没有被手工修改或落后于 Schema。生成文件禁止直接编辑。

## Rust DTO

Rust 使用 `typify` 在编译期分别导入领域 Schema：

```text
src-tauri/src/schema_dto.rs
  → typify::import_types!(schema = "../schemas/<domain>.schema.json")
  → Rust 编译期生成 generated::<domain> 模块
```

因此执行 `cargo check --manifest-path src-tauri/Cargo.toml` 即会校验所有领域 Schema 是否能够生成合法 Rust 类型。`model.rs` 只保留内部状态、校验和行为，不再作为 DTO 契约来源。
