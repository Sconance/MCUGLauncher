> 历史记录，不作为当前开发规范。当前入口见 [架构说明](../../ARCHITECTURE.md)。

# WebView2 UI 开发说明

## 分层

- MainWindow：WebView2 生命周期、可信来源校验、窗口和原生确认/文件选择。
- LauncherApplication：解析/校验消息、调用按职责划分的 UseCases；状态由 LauncherStateBuilder 生成。
- LauncherContext：配置、账号、网络、任务队列与游戏会话的组合与生命周期。
- Services/LauncherTaskQueue：OperationCoordinator 的持久队列调度；DownloadExecutor 调用 Core 安装与校验下载。页面不持有任务。
- Core：启动、依赖解析、事务安装、校验下载、Java 扫描、收藏与存档归档等既有能力。
- Ui：app.js 只保留查询路由和共享绑定。页面、导航、弹窗和图形分别在 *-page(s)/navigation/account-modals/graphics；新功能分别在 tasks/install/games/bookmarks/saves/instance-tools。bootstrap 在独立浏览器下才加载 preview-data。styles.css 是当前样式入口。

新增功能优先使用 Core，协议分发后调用相应 UseCase，不把业务搬回窗口。开发约定见根目录 AGENTS.md。

## 协议与数据

宿主仅接受 `https://app.local` 的 JSON 命令。消息先验证字段类型，错误返回 `{type:"error",message:"..."}`。

任务命令为 requestTasks/taskAction，操作包括 pause/resume/cancel/delete；tasks 响应包含持久记录 id、name、state、percent、message、committing。提交阶段不允许取消。

加载器通过 listLoaders/installLoader；整合包通过 pickFile(target=pack)/importPack；游戏通过 requestGames/stopGame；收藏通过 toggleBookmark；备份通过 listSaveBackups/backupSaves/restoreSave。原有命令继续兼容。

配置在 `%LOCALAPPDATA%/MCUGLauncher/config.json`；Core Preferences 的保留模型在 LegacyPreferences，网络/启动实际复用它。任务记录在 operations；收藏在 resource-bookmarks.json；备份按实例哈希放在 backups；游戏日志在 game-logs。

数据包不能直接安装到游戏根目录：先下载，再导入具体存档。整合包先下载后在安装与导入中选择文件，或直接选择已有本地包。

## 验证

```powershell
python scripts/check-layout.py
python scripts/check-version.py
python scripts/check-web-boundaries.py
dotnet run --project tools/SourceFormatter
./scripts/run-checks.ps1 -Name AuthChecks,MuaChecks,DownloadChecks,QueueChecks,CompatibilityChecks,WebBridgeChecks
npm ci --ignore-scripts --prefix test/WebUiChecks
npm run format:check --prefix test/WebUiChecks
npm test --prefix test/WebUiChecks
```

WebUiChecks 默认使用系统 Edge；也可通过 BROWSER_CHANNEL 指定支持的浏览器通道。截图输出到 work/verify/report-ui（不入库）。测试使用隔离目录、假 HTTP 和模拟桥接，不涉及真实用户账号。

完整的进展与剩余缺口见 [报告对应修复状态](REPORT_FIX_STATUS.md)。真实 WebView2、账号与游戏必须另行验收。

## 实例维护协议

cloneInstance/exportInstance 提交明确版本标识、可选内容和新名称/新 ZIP 路径。previewUninstall 返回路径及短期令牌，applyUninstall 在原生确认后入队。重启后的卸载任务必须再次确认。

checkContentUpdates 返回检查结果令牌；previewContentUpdates 携带所选项目生成文件变化和指纹；applyContentUpdates 只提交预览令牌。所有任务固定根目录与目标实例，不能在执行时改读当前选择。

维护请求新增的 Core 队列字段为可选项，兼容旧记录。实例导出采用 overwrite=false，不覆盖已有 ZIP；保持 Core 原有调用默认行为兼容。
