# AvatarDiskCache（A）

职责：持久化渲染后的头像 PNG，以及账号、来源到图片的索引。只依赖 Core 的 AtomicFile 与 BCL；不读取凭据，不访问网络，不引用 UI。

- ReadAsync(identity, token)：从账号索引恢复图片及检查时间。
- ReadSourceAsync(source, token)：复用相同来源的已渲染头像。
- SaveAsync(identity, source, image, revision, token)：图片先写临时文件并原子替换，然后写来源索引，最后提交账号索引。同一服务对象中旧代保存不能覆盖已提交的新代。

目录为数据目录下 cache/avatars/v1；accounts、sources 用 SHA-256 派生文件名，images 按 PNG 内容 SHA-256 去重。索引不保存原始 URL 或令牌。这里只保存头像预览，不保存整张皮肤，不作为游戏皮肤来源。

读取限制索引 4 KB、图片 2 MB，校验格式版本、哈希、PNG 头和尺寸；损坏按未命中处理，不删除用户文件。写入失败不影响当前成功显示的头像。当前不自动清理历史图片；渲染格式变化时应升级缓存格式目录。

参考 PCL 的账号索引、地址图片缓存，以及 HMCL TexturesLoader 的磁盘复用；按现有 LauncherService 边界实现，没有引入其 UI 或复制认证流程。来源：
- https://github.com/Meloong-Git/PCL/blob/main/Plain%20Craft%20Launcher%202/Modules/Minecraft/ModMinecraft.vb
- https://github.com/HMCL-dev/HMCL/blob/main/HMCL/src/main/java/org/jackhuang/hmcl/game/TexturesLoader.java

WebBridgeChecks 覆盖跨应用上下文恢复、来源复用、身份隔离、刷新失败保留、旧代写入、损坏索引与图片。真实账号和网络耗时未测量。
