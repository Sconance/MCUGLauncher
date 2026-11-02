# 历史归档

这里存放已经完成使命、仅用于追溯的文档。现行开发文档见 [文档索引](../legacy-wpf/DEVELOPMENT.md)、类文档目录（历史文件已退役） 和 [版本号控制](../../VERSIONING.md)。

## 归档标准

- 记录已废弃分支（`refactor`）或一次性合并过程的材料；
- 被新结构取代的对照表、快照与迁移清单；
- 不再作为开发入口，正文中的路径按当时的分支和版本保留原样。

## 内容

| 文档 | 归档原因 | 现行替代 |
| --- | --- | --- |
| [TEST_REFACTOR_COMPARISON.md](TEST_REFACTOR_COMPARISON.md) | 合并前的 `test` / `refactor` 结构快照（2026-10-28） | [MainWindow 职责拆分](../legacy-wpf/MAINWINDOW_REFACTOR.md)、类文档目录（历史文件已退役） |
| [COMPARE_DEVELOP_REFACTOR.md](COMPARE_DEVELOP_REFACTOR.md) | 0.20.1 / 0.20.2 从 `develop` 迁入 `refactor` 的功能对照 | [更新记录](../../../CHANGELOG.md)、类文档 |
| [REFACTOR_CONTINUATION.md](REFACTOR_CONTINUATION.md) | `refactor` 组件在 `test` 中的迁移与逐字复用说明 | 复用基线仍在 `scripts/refactor-reuse.json`，检查为 `scripts/check-refactor-reuse.py` |
| [REFACTOR_INTEGRATION.md](REFACTOR_INTEGRATION.md) | `refactor` 合入 `test` 的一次性记录（`145bf37`） | 合并到主分支前的说明（历史文件已退役）、[重构路线图](../../../TODO.md) |

`refactor` 分支已废弃，不再合并、不再发版；其源码保留在 git 历史 `db0ab6a`。`test` 是 0.22.0 的来源分支，合并事宜见 合并到主分支前的说明（历史文件已退役）。
