# 正版 / MUA 切换流畅度优化

本轮用户在账号切换卡顿分析后授权同时优化业务、UI 和相关检查。只维护 0.24.0-codex，不更改其他分支。

- HomePage 账号控件只创建一次，模式/名字/标签做局部更新。头像事件只更新 Image.Source；已解码头像最多缓存 6 张，页面释放时统一释放，不重复解码 A→B→A。
- 与账号无关的启动区、服务器/公告/推荐/语录及实例列表不再跟随账号摘要重建；背景设置未变时不重复检查背景文件。
- UiContext 默认 Steve 编码只生成一次，相同头像结果不重复通知；皮肤弹窗仅监听有效头像变化。
- AvatarCommands 自动读取使用有时限缓存、合并同身份请求；手动刷新仍强制读取。图形适配器首次解码仍在所属图形线程执行，不宣称所有冷路径完全没有开销。
- 模式选择先更新内存状态，再用独立配置快照异步原子保存；同步与异步写入有顺序保护。启动时预读已保存账号的本地身份；首次切换也不联网恢复会话。启动游戏前再按需验证令牌，网络失败时停止该次启动。

验证：WebBridgeChecks 检查缓存会话复用、网络/授权错误、迟到恢复、快照保存排序、头像缓存/合并/手动刷新与迟到结果；NativeUiChecks 验证快速切换时控件/社区卡片身份不变、位图复用和原有界面行为；LauncherChecks、MuaChecks 继续检查本地用例和模拟认证。以上没有使用真实微软/MUA账号测量耗时，不给出未经测量的毫秒数或速度提升比例。

快速切换标签允许后续选择继续提交，由后端 AccountRevision 决定最终身份；其他保存/安装按钮仍防重复点击。已增加挂起前次命令时 A→B→A 三次意图均到达的控件检查。

## 本地选择与启动授权分离

参考 [HMCL Accounts.setSelectedAccount](https://github.com/HMCL-dev/HMCL/blob/main/HMCL/src/main/java/org/jackhuang/hmcl/setting/Accounts.java) 与 [LauncherHelper 的登录阶段](https://github.com/HMCL-dev/HMCL/blob/main/HMCL/src/main/java/org/jackhuang/hmcl/game/LauncherHelper.java)，以及 [PCL PageLoginMs 的本地列表/选择处理](https://github.com/Meloong-Git/PCL/blob/main/Plain%20Craft%20Launcher%202/Pages/PageLaunch/PageLoginMs.xaml.vb)。这些实现将选中项与实际认证分开；本分支按现有用例边界实现同一职责划分。

AccountIdentity 只含玩家名和 UUID，不含令牌，也不代表授权通过。启动时在后台读取两个模式的本地身份摘要，供切换复用；没有摘要才进入登录入口。本地读取迟到时检查选择版本，不能把旧模式切回来。账号移除同时清空身份索引与验证会话。

GameCommands 不再要求选中账号时已有 OnlineSession；在线启动先调用 EnsureFreshAsync，拿到有效会话后才处理外置认证注入和游戏进程创建。选择摘要不会填入验证时效缓存，不能用本地名字假装通过购买/令牌验证。MUA 和正版使用同一流程。

补充验证：启动时/冷切换不调用网络认证；离线网络条件下仍可选中本地身份；按需授权才恢复会话；缺失账号仍提示登录；本地未授权身份不能到达进程创建；迟到身份读取不覆盖新选择。实际账号与弱网体验仍需人工验收。
