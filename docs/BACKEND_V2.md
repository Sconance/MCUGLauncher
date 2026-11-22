# V2 后端契约与状态方案

本文记录 V2 契约的设计原则、迁移结果与验收边界。当前可调用接口以 [BACKEND_CONTRACT.md](BACKEND_CONTRACT.md) 和 [launcher.schema.json](../schemas/launcher.schema.json) 为准。传输使用 Tauri invoke，字段使用 camelCase，命令使用 snake_case。

## 实施状态

契约迁移已完成：全局三秒同步任务和游戏状态；聚焦刷新；各初始化请求独立提交；同资源旧刷新响应丢弃；前端识别结构化错误。

已补充：未实现的系统、网络、登录和长操作返回 UNSUPPORTED；任务动作由后端校验，提交阶段禁止中断，取消进入 cancelling，终态才允许删除。实例、服务器和模组使用稳定 ID，选择实例不再混用版本名称。启动器状态查询返回 epoch/revision 快照，设置写入校验 expectedRevision，前端拒绝同代次的旧修订。任务页明确标识演示数据。

能力边界：全局启动设置和自定义数据源配置已写入本地 JSON，实例清单读取当前游戏目录。账号和任务仍使用内存演示数据，真实安装、持久队列和多步骤事务尚未接入。游戏启动与停止、在线认证、更新和整合包处理需要后续接入真实适配器；未接入的命令明确返回 `UNSUPPORTED`，不伪造成功。卸载预览会生成十分钟有效的 `planId`，绑定实例 ID 和实例资源修订号；执行前区分 `PLAN_EXPIRED`、`CONFLICT` 和有效但未实现的 `UNSUPPORTED`，演示后端不会删除文件。所有当前查询资源均返回独立 `epoch/revision`；前端逐项加载资源，一个资源失败时保留其他资源与已有数据。

## 状态归属

后端持久状态包括设置、实例配置、账号记录、收藏。后端运行状态包括任务、游戏会话、登录会话、探测结果。前端仅拥有页面、搜索条件、表单草稿和缓存状态。

资源拆分为 overview、settings、accounts、instances、instanceMods(instanceId)、instanceLaunchSettings(instanceId)、tasks、gameSessions、servers、bookmarks、community。前端状态容器直接保存这些独立资源，不再重新聚合旧的聚合状态。overview 仅包含 appVersion、selectedInstanceId、activeAccountId。未选择使用 null，不使用空字符串。

实例使用持久稳定 ID；名称和目录不是主键。当前选择只保存 `selectedInstanceId`，版本清单属于安装目录。模组、存档、安装、启动、备份等请求必须显式携带 `instanceId`；服务器和账号也使用稳定 ID。重命名不能改变 ID。

## 查询与写入

可缓存查询返回 { epoch, revision, data }。epoch 标识后端运行代次，revision 是同资源同代次递增的安全整数。不同资源的 revision 不可比较。epoch 改变时清空旧缓存并重新加载。

短写入返回 { message, changed, entityIds }。changed 是结构化资源键数组，例如 { kind: "instanceMods", instanceId }；entityIds 列出新建、修改或删除的实体 ID。命令成功表示实际提交成功；提示文本不得参与业务判断。配置写入携带 expectedRevision，过期返回 CONFLICT。

字段补丁：省略表示保持，允许清除的字段以 null 表示清除，后端必须区分这两种情况。配置持久化成功后才能返回成功。

失败统一通过 Err(ApiError) 返回：{ code, message, retryable, fieldErrors? }。code 包括 VALIDATION_ERROR、NOT_FOUND、CONFLICT、BUSY、AUTH_REQUIRED、NETWORK_ERROR、IO_ERROR、PLAN_EXPIRED、INTERNAL_ERROR、UNSUPPORTED。移除重复的 ok/error 包装。探测成功发现离线是正常数据结果。

## 长任务

安装、导入、导出、备份、恢复、卸载、更新返回 { taskId }，表示接受而非完成。重复提交通过 requestId 去重；相同 requestId 和相同参数返回原任务，不同参数返回 CONFLICT。去重记录至少保留至任务终态后的保留期内，并明确配置保留期。

Task 包含 id、kind、instanceId、status、phase、progress、allowedActions、error、result、createdAt、updatedAt。该结构已经落实到前后端；progress 为 null 或 { completed, total: number | null, unit: "bytes" | "items" }，未知总量不显示伪造百分比。时间统一为 UTC RFC3339。

状态流：queued → running → succeeded/failed；支持暂停时 running → paused → running；接受取消后进入 cancelling，清理完成后进入 cancelled。提交阶段禁止暂停或取消。后端计算 allowedActions 并再次校验操作，不信任 UI 按钮状态。只有终态允许删除记录。

终态结果必须包含受影响资源键和生成实体 ID。重启时恢复可恢复任务，其他任务转为 failed 并给出原因；不得遗留虚假的 running。

游戏会话独立使用 preparing、starting、running、stopping、exited、failed，保存 sessionId、instanceId、accountId、可空 pid、exitCode 和 error。launch_game(instanceId, accountId, server?) 返回 { sessionId, taskId }；准备任务成功不代表游戏已运行。stop_game(sessionId) 仅代表已请求停止，进程退出后才更新会话终态。

## 登录与确认计划

登录返回不透明 authSessionId，查询会话得到 awaitingAuthorization、awaitingProfile、succeeded、failed、cancelled、expired 之一；每个状态只包含该状态所需字段。账号访问令牌仅在后端保存。会话必须支持取消和过期。

预览返回 planId、targetId、targetRevision、expiresAt 和具体变更。执行必须绑定原计划、检查修订和有效期，并加目标写锁。过期返回 PLAN_EXPIRED，对象变化返回 CONFLICT。重复执行返回原任务；执行失败后的计划是否可重试必须由后端明确返回。

## 前端同步

每个资源独立保存 data、status(idle/loading/ready/error)、refreshing、error。刷新失败保留旧数据，页面可显示过期提示；单个资源失败不阻断其他资源。请求序号隔离搜索条件和实例切换，旧响应不得覆盖新响应。

全局同步器负责运行状态；任务终态引起相关资源失效。写入后按 changed 刷新。可选事件只传失效通知，事件丢失由轮询和聚焦刷新恢复。请求卸载时停止更新，避免无效通知。

## 迁移结果与验收

1. 全局同步与独立初始化已落地。
2. 后端已统一结构化错误和稳定 ID，实例、账号、服务器与模组操作不再依赖名称定位。
3. 查询已拆分为独立资源快照，前端按资源缓存、保留旧数据并处理修订冲突。
4. 任务、游戏、登录和危险操作计划均有明确状态/契约；演示后端无法执行的能力返回 `UNSUPPORTED`。
5. 旧聚合查询和旧结果包装已移除，主契约已更新为 V2。

每步运行前端构建、Rust 检查及相关状态转换测试。最终验收覆盖：社区失败不阻断首页；后台任务完成刷新实例；重命名后 ID 不变；并发编辑冲突；取消提交阶段任务被拒绝；过期计划无法执行；重复提交不创建重复任务；重启恢复无永久 running；旧响应不能覆盖新实例数据。
