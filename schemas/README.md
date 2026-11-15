# DTO JSON Schema

`launcher.schema.json` 是 V2 DTO 的集中契约文件。Rust 的 `model.rs`、前端的 `src/types/launcher.ts` 和 `docs/BACKEND_CONTRACT.md` 必须与该 Schema 同步；新增或修改 DTO 时先更新 Schema，再更新两端类型和命令文档。

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

`dto:check` 会重新生成文件并检查 Git diff，确保生成文件没有被手工修改或落后于 Schema。生成文件禁止直接编辑。
