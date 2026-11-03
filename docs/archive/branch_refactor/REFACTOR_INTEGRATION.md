# refactor 分支整合记录

> 历史归档：本文记录 refactor 时期的一次性过程，`refactor` 分支已废弃。现行文档见 [开发导览](../legacy-wpf/DEVELOPMENT.md) 与 合并到主分支前的说明（历史文件已退役）。


## 结论

`test` 是当前重构主干。`refactor`（`db0ab6a`）已作为合并的第二父提交并入主干（合并提交 `145bf37`），结构以 `test` 为准：`MainWindow` 保持 35 行 shell、九个独立 XAML 页面与 `Composition/LauncherWorkspace` 组合根。

合并没有采用逐文件覆盖：`refactor` 的 86 个 `partial class MainWindow` 文件正是 `test` 已删除的实现方式，整文件合入会回退「拆分 `MainWindow`」这一目标。合并提交只确认祖先关系并记录整合决定；`refactor` 的规则、文档与版本控制内容随后按功能迁移，本文件即迁移记录。

## 分支关系

```text
69a41a6  v0.20.0（main）
├── 37758ee → 4a99ec9 → a056733  v0.20.2（官方功能线）
│                          └── 8 个 test 拆分提交 → 0eaf5ec（原 test tip）
└── 12 个 refactor 拆分/移植提交 → db0ab6a（refactor tip，0.21.0）

test 主干：0eaf5ec + 合并提交 145bf37（父提交 0eaf5ec、db0ab6a）
```

## 内容去向

| refactor 内容 | 处理 | 说明与去向 |
| --- | --- | --- |
| 86 个 `MainWindow/**` partial 文件 | 退役 | 功能已由 `Components/`、`Views/`、`Presentation/`、`Composition/` 承接，不再引入 `partial class MainWindow` |
| 51 篇 `docs/MainWindow/**` 逐类文档、`docs/CONTROLLERS.md` | 退役 | 描述已删除的 partial 结构；历史内容保留在 `refactor` `db0ab6a`，现行结构见 [MainWindow 职责拆分](../legacy-wpf/MAINWINDOW_REFACTOR.md) 与 [开发导览](../legacy-wpf/DEVELOPMENT.md) |
| `TODO.md` | 重写 | 保留「整合与拆分检查要点」，待办改为 `test` 结构下剩余的主窗口拆分 |
| `update_refactor.md` | 吸收 | 合并为本文件 |
| `compare_develop_refactor.md` | 迁移 | [develop 与 refactor 功能对照](COMPARE_DEVELOP_REFACTOR.md)，补充历史说明 |
| `0.21.0` 版本与诊断信息版本号修复 | 统一 | 改为单一来源 `Directory.Build.props` + `ProductInfo.Version`，见 [版本号控制](../../VERSIONING.md) |
| 0.20.1 / 0.20.2 功能（资源工作区、收藏、菜单扩展、加载器概览、品牌资源） | 主干已具备 | `test` 直接站在官方 `v0.20.2` 提交上，无需再次移植 |
| 11 个逐字复用组件 | 保留 | `scripts/refactor-reuse.json` 继续以 `db0ab6a` 为基线校验 |
| `ContentService` 排序校验改用 `switch` | 不采纳 | 与主干 `if (sort is not (...))` 语义等价，主干保留本地化异常信息 |
| Esc 关闭页内选择器、嵌套改名刷新实例名称 | 主干独有 | 已保留，由 `InlineUiChecks` 覆盖 |

## 验证记录

- Release 构建：0 警告、0 错误；程序集版本 `0.21.0`，`InformationalVersion` 与合并提交一致。
- 源码格式、布局、UI 边界、复用基线（仍以 `db0ab6a` 为准）与版本元数据检查全部通过。
- 通过检查：`InlineUiChecks`、`VersionUiChecks`、`WorkflowUiChecks`、`CollectionChecks`、`AuthChecks`、`MuaChecks`、`DownloadChecks`、`QueueChecks`、`CompatibilityChecks`、`MiniDownloadChecks`、`StabilityChecks`、`UninstallChecks`、`UpgradeChecks`、`ExperienceChecks`、`ContentSafetyChecks`、`DependencyChecks`、`LibraryChecks`、`ResourceDownloadChecks`、`CompactUiChecks`、`DownloadUiChecks`。
- `DiagnosticChecks` 在本机因缺少符号链接特权停在 `File.CreateSymbolicLink`，属环境限制；详细记录见 [验证记录](../legacy-wpf/TESTING.md)。
- 未执行 `IntegrationChecks`、真实微软 / MUA 授权与完整游戏启动矩阵。

## 后续

`MainWindow` 拆分尚未结束：实例概览、资源工作区、任务与启动协调仍通过 `LauncherWorkspace` 协作，`Components/` 内仍有 400 行以上的模块。后续按 [重构路线图](../../../TODO.md) 收窄依赖，不恢复 partial 组织，也不把窗口字段重新汇总到组合根。
