# 设置与提示修订

用户明确要求整理设置；保留 MCUG 标志与蓝白配色。

## 分类与行为

游戏：启动设置、Java、游戏目录、实例维护、存档保护。启动选项共用一个编辑器，实例可继承或覆盖；Java 集中管理，目录记录删除有确认且不删除文件，备份保留 1–50 份。

启动器：界面与背景、下载与网络、安装与导入、维护与恢复。背景支持 PNG/JPEG、透明度和缩放；源与限速作用于现有 HTTP 策略，代理重启生效；下载目录仅作为自定义资源下载默认值，不改变安装实例目录。

账号与社区：账号与社区、数据源、服务器收藏、我的收藏、管理员。保留原登录、密钥和默认/自定义源保存与启用分离行为，没有改写服务端权限。

任务与诊断：下载任务、运行中的游戏、运行日志、诊断与反馈。关于单独保留许可入口。

右上提示采用左右等宽占位，使文字相对整个浮层居中；只去除末尾句号，不删除内容中的标点和小数点。

## 简体中文

AutoChinese 默认 true，全局与实例均可设置。启动使用已冻结的实际游戏目录与基础版本，在构建进程前原子更新 options.txt。旧版 zh_CN、新版 zh_cn，其他行保留；同目录已有游戏运行时不改文件。关闭选项保留玩家语言。

## 协议与结构

saveSettings 新增可选 network、appearance、gameRoots、backupCount、resourceDownloadDirectory；state.preferences 返回本地界面/下载偏好，不包含授权令牌。saveLaunchOptions.settings.autoChinese 为可选字段，旧调用保持原值。

Views 只负责展示和提交，现有 UseCases 校验并保存，Core 负责语言和下载能力。没有新增下载器、认证服务或窗口业务控制器。依赖见 docs/components。

## 参考

参考 [PCL 启动设置](https://github.com/Hex-Dragon/PCL2/blob/main/Plain%20Craft%20Launcher%202/Pages/PageSetup/PageSetupLaunch.xaml) 的分组与自动选择提示、[HMCL 设置说明](https://docs.hmcl.net/launcher/set-item-details.html) 的游戏/外观/下载组织方式。仅借鉴交互，不复制代码，不表示已实现它们所有选项。

## 验证范围

NativeUiChecks：菜单、设置保存消息、通知去句号与几何居中、原有弹窗/路由行为；已检查网络、启动、外观及通知 Skia 截图。
WebBridgeChecks：真实命令的偏好保存、失败不修改玩家、活动网络引用、目录记录不删除文件、语言新旧版本/关闭路径；启动测试使用假进程，不代表真实游戏或账号验收。

本轮执行结果：SourceFormatter、布局、版本、原生边界、NativeUiChecks、WebBridgeChecks、CompatibilityChecks、DownloadChecks 均通过。NativeUiChecks 有一条既有过时按键 API 警告，产品构建无此警告。真实微软/MUA 登录、在线代理连通性和实际游戏启动未在本轮验收。
