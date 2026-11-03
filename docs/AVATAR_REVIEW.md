# 头像与异步图片审查修订

本轮获用户授权同步调整业务、共享契约、UI 和检查；仅维护 0.24.0-codex。

## 参考与取舍

参考 PCL ModMinecraft 的账号索引与皮肤磁盘复用、HMCL TexturesLoader 的 UUID 身份、后台加载和失败诊断。只采用设计思路，未复制源代码或资源；同地址内容更新的五分钟重新读取策略是本项目的兼容选择，不声称两者采用完全相同的 TTL。

- https://github.com/Meloong-Git/PCL/blob/main/Plain%20Craft%20Launcher%202/Modules/Minecraft/ModMinecraft.vb
- https://github.com/HMCL-dev/HMCL/blob/main/HMCL/src/main/java/org/jackhuang/hmcl/game/TexturesLoader.java

## 行为与边界

AvatarDiskCache.IsFresh 同时约束账号索引和来源索引。来源命中时保留原验证时间，不能通过多账号共享不断延长；过期重新读取，失败保留旧头像。没有常驻计时器，下次头像查询时检查过期；手动刷新立即绕过缓存。

LauncherContext.AvatarIdentity 统一由模式、提供方、UUID（缺失时玩家名）、离线路径组成，沿 LauncherState / AccountStateInfo / AvatarInfo 传递。它不是授权证明、不含令牌，不写入用户配置格式。界面只接受当前身份的结果；无标识的旧测试响应仍按模式和玩家名兼容，实际后端总是发送标识。旧磁盘键规则保留。

RemoteImage 每次挂载拥有独立取消等待和请求代次，离开时取消等待、清除并释放当前位图。这里取消的是本视图等待，底层共享业务请求仍由服务管理，不声称关闭了网络传输。意外解码错误通过 Trace 记录类型。

头像来源的预期网络、文件和格式错误写 launcher.log 类别，不写 URL、令牌或原始错误正文；意外异常不伪装为默认图，由原命令边界记录，自动刷新兜底也记录类别。

## 验证

WebBridgeChecks 覆盖缓存过期、共享来源时间保留、损坏/null 索引、失败诊断脱敏、意外异常传播、身份和迟到结果。NativeUiChecks 覆盖同名不同 UUID 的刷新与隔离、重新挂载时旧图片晚返回、位图清除，以及现有页面截图。真实账号、弱网和实际游戏未实测。
