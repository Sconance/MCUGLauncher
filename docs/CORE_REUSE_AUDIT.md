# 0.24.0-codex Core 复用与职责复查

## 基线与范围

从 75e88a1 继续，保留该提交已统一的 ProfileInspector、ContentService 与 InstanceSnapshot。检查 App/Services、UseCases、Avalonia 展示层以及相应 Core 能力，按实际重复行为处理，不按文件行数拆类。

本轮不改插件化、动画、图标缩放、菜单布局、账号协议或安装目标。

## 已改进

| 问题 | 改法 | 保留契约 |
| --- | --- | --- |
| App 自写服务器 SLP 握手、VarInt、TCP 拆包、MOTD 解析，与 Core 重复 | ServerPingService 仅调用 Core ServerFavorites.Ping 并适配结果 | PingResult 字段、8 秒超时、失败结果、颜色符清理；延迟现在来自实际 ping/pong |
| ConfigStore 另写固定 .tmp 提交，社区文件直接覆盖 | 统一 AtomicFile.ReadJson/WriteJson | 配置字段及本地公告数组不变，原文件损坏仍保留备份 |
| 偏好编辑通过 JSON 往返克隆 | Preferences.Copy/NetworkSettings.Copy，显式隔离可变成员 | 未显示的偏好保留，编辑不修改活动选项 |
| 数据源模式通过 JSON 字符串比较，命令内手写复制 | DataSourceConfig.Copy/HasSameAddresses | 四地址精确比较，保存和使用仍分离 |

## 边界判断

- ServerPingService 虽然变短仍有明确职责：把异常转为界面结果，转换字段并清理颜色符；不是无意义转发器。
- AtomicFile 为多个存储调用方共享，继续留 Core，应用路径选择仍在 ConfigStore。
- 草稿复制属于值模型，放回现有类型，不新建 Manager/Service。
- HTTP、下载、Java、资源/实例入口扫描未发现 Avalonia 直接新建下载器；MUA 的独立客户端是既有认证适配，需要禁用重定向，不在此次更换。

## 限制与后续

这不是整个项目架构已经完美的证明。LauncherContext 仍向多个 B 组件提供较宽运行时依赖，LauncherApplication 的协议分发较集中；暂不为减少行数改成多层转发。CommunityCommands 同时处理内容和服务器收藏，但本轮只收敛存储与协议重复，避免改动授权/发布业务。已有部分自动生成的泛化 XML 注释仍需随对应业务修改逐步完善。

## 验证

WebBridgeChecks 新增真实环回 TCP SLP/pong、嵌套 MOTD、无效地址、关闭端口、异常数据包；偏好副本值等价与可变成员隔离；数据源草稿/模式；null 配置备份；公告追加与原子文件清理。测试不访问真实账号或公网服务器。

本轮结果：SourceFormatter、布局、版本、边界、WebBridgeChecks、CompatibilityChecks、NativeUiChecks 均通过。界面布局未修改；真实账号、游戏与公网服务器未在本轮验收。Core 原有两参数 Ping 接口保留，新增超时重载供 App 维持 8 秒行为。
