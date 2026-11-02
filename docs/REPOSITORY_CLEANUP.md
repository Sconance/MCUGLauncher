# 仓库资源与文档清理

## 删除

- `.prettierrc.json`：当前已无跟踪的 JS/CSS/HTML 页面，CI 和构建不使用此配置。
- docs/images 下的 app-home、appearance、download-tasks、home、java、manager、native-sources、pack-import 共 8 张旧截图：没有实际文件路径引用，也不属于应用资源；测试中的相似截图名写到 work/native-ui，和这些文件不是同一来源。
- AVALONIA_MIGRATION、NATIVE_UI_MIGRATION、NATIVE_UI_PARITY 三份完全相同、无引用的迁移提示文档；保留完整且有引用的 MIGRATION_0.24.0-codex.md。

## 整理

旧网页分层、旧分支报告、旧 TODO 和混合时期的风格记录集中到 docs/archive/webview，并更新引用。CONTRIBUTING、TODO、CODE_STYLE_REVIEW、USAGE 改为当前原生分支说明。依赖清单按实际 restore 包元数据更新。

Notices.txt 仅清理开头旧 0.8.0 标识，版权正文不删除。应用图标、透明 MCUG 标志、README 引用的 native-home.png 保留。外部 MCUGL-storage 是真实数据源仓库名，不改写成无效链接。

没有删除业务源码、测试夹具、历史 Git 提交、用户游戏与配置，也不清空忽略的 work/artifacts 测试目录。资源无静态引用不总等于无用，反射、打包通配符与版权资源单独核对。

验证：当前 README、贡献指南、TODO 与 docs 下所有 Markdown 本地链接可解析；SourceFormatter、布局、版本、边界检查通过；产品 Release 构建为 0 警告、0 错误。本轮没有修改业务代码，不把构建成功当作真实账号或游戏验收。
