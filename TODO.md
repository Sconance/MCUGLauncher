# 当前待办（0.24.0-codex）

本分支是 Avalonia 原生界面的 AI 迁移框架尝试。当前结构见 [ARCHITECTURE](docs/ARCHITECTURE.md)，历史网页迁移清单已归档，不再作为当前开发任务。

## 需要真实环境验收

- [ ] Microsoft 与 MUA 登录、会话恢复和网络失败路径。
- [ ] Windows 资源管理器实际拖放、系统文件选择器和不同缩放比例。
- [ ] 代表性原版/加载器实例的安装、启动、多开与退出。
- [ ] 弱网、中断、重启恢复、文件占用和存档恢复的组合场景。

## 继续维护

- [ ] 按实际修改逐步缩小 LauncherContext 依赖面，完善方法和成员说明。
- [ ] 依实际失败案例补齐旧加载器、整合包格式兼容性。
- [ ] README 的原生截图随实际界面更新，避免把模拟截图当真实业务验收。

插件化、交互动画和图标缩放按用户要求暂不做。本分支不自动发布 Release；跨平台尚未验收。

检查命令见 [BUILD](docs/BUILD.md)，现有缺口与验证范围见 [Core 审查](docs/CORE_REUSE_AUDIT.md) 和 [交互修复](docs/INTERACTION_FIXES.md)。
