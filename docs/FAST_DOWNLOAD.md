# 高速下载引擎

下载实现集中在 `src/MCUGLauncher.Core/Infrastructure/Downloads/`，对上层只暴露 `DownloadEngine`、`DownloadTask` 与 `DownloadProgress`。

## 组装方式

- `DownloadTask` 描述一个文件：目标路径、候选源列表、期望哈希、算法、大小与优先级。
- `DownloadEngine.DownloadAllAsync` 负责文件级调度（`MaxFileConcurrency`）；单文件内部由 `ChunkedDownload` 负责分片级调度（`MaxChunkConcurrency`）。
- `SourceSelector` 做健康检查与测速排序；`FileChecker` 负责本地复用与最终校验；`DownloadMeter` 汇总速度。
- Minecraft 本体/库/资源通过 `FastGameInstaller : GameInstallerBase` 注入 `MinecraftLauncherParameters.GameInstaller`，其他下载点仍走 `ReliableDownloader` 的兼容单流。

## 降级顺序

1. 分片并发（源支持 `Range` 且文件不小于 `ChunkSizeThreshold`）。
2. 同一源的单流续传（服务器忽略 `Range`、返回 416、或分片读取持续失败）。
3. 换下一个候选源重试（带指数退避与抖动）。
4. 明确错误：永久文件错误直接上报，哈希连续失败提示更换下载源。

分片数据写在 `.part.chunked` 与 `.part.chunked.json`，与单流的 `.part` 分开，避免把不连续的分片内容当成顺序前缀。取消时按 `KeepPartialOnCancel` 决定保留断点还是清理。

## 下载源偏好

`NetworkSettings.DownloadSourcePreference` 决定默认首选源（新配置默认 `Mirror`，可在设置页「下载与网络」切换）。两处职责分开，避免同一次下载被双重改址：

- 下载任务：`FastGameInstaller` 与自定义文件下载按偏好构造 `[镜像, 官方]` 或 `[官方, 镜像]`；`SourceSelector` 先按偏好分组，再在首选组内做健康检查与测速，首选组失败或镜像缺少文件时由引擎切到另一源。
- 其他请求（版本清单、公告、API）：`NetworkPolicyHandler` 按偏好选择端点顺序；引擎发出的请求带 `NetworkPolicyHandler.DirectSource` 标记，处理器直接放行，不重写地址也不再换源。

旧配置里的 `DownloadSource` 字符串继续兼容：`仅官方` 视为官方优先，其余取值按枚举处理；设置页保存时会写入枚举并清空该覆盖字段。

## 配置

配置位于 `NetworkSettings`（`settings.json` 的 `network` 节），非法值由 `Validate`/`DownloadOptions.Normalize` 夹取或回退默认：

| 键 | 默认值 | 说明 |
|---|---|---|
| `EnableFastDownload` | `true` | 关闭后回退到原有 CmlLib 并行安装器与单流下载 |
| `MaxFileConcurrency` | `4` | 同时下载的文件数（1–16） |
| `MaxChunkConcurrency` | `3` | 单文件同时进行的分片数（1–8） |
| `ChunkSizeThreshold` | `1048576` | 达到该大小才分片（256 KiB–64 MiB） |
| `SmallFileThreshold` | `262144` | 小于该大小直接单流（16 KiB 起） |
| `SpeedTestBytes` | `262144` | 每个候选源的测速流量（16 KiB–4 MiB） |
| `SpeedCacheMinutes` | `3` | 测速结果缓存时长（1–60 分钟） |
| `RequestTimeout` | `20` | 单次请求超时秒数（0 表示不额外限制） |
| `MaxRetries` | `3` | 每个源的重试次数（0–10） |
| `HashAlgorithm` | `SHA1` | 新建任务的默认算法（SHA1/SHA256/SHA512） |
| `EnableDnsFallback` | `false` | 保留开关；真正的换 IP 重连需要接管 `ConnectCallback`，见 `IHostResolver` |
| `KeepPartialOnCancel` | `true` | 取消时保留断点 |
| `DownloadSourcePreference` | `Mirror` | 首选下载源（`Mirror`/`Official`），设置页可切换 |

## 边界说明

- `ResourceDownloads`、`ContentService`、`PackService` 与加载器安装器仍使用兼容单流，避免改变既有请求序列与异常文案；分片能力只作用于 CmlLib 的版本文件下载。
- CmlLib 文件的候选源在存在镜像映射时包含 BMCLAPI，并按偏好排序；映射不到的资源（如 `piston-data`）仍然只有官方地址。镜像侧缺文件返回 404/403 时由引擎切回官方，不会把整个下载判为失败。
- DNS 兜底只提供 `IHostResolver` 接口与 `EnableDnsFallback` 开关，接入点需要新建带 `ConnectCallback` 的 HttpClient，与全局单例 HttpClient 的约定冲突，因此默认不启用。
