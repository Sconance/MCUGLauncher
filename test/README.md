# 回归检查

## 版本检查脚本

```sh
python -m unittest discover -s test/VersionChecks -p "test_*.py"
python scripts/check-version.py
```

使用 Python 标准库，验证公告源的 GitHub 分支 URL 不会被误报为程序版本，并保留对 User-Agent、界面版本文案以及同一行混合 URL/版本常量的拦截。Windows build 工作流在版本检查之前运行这组回归检查。

## 现有 C# 检查

使用仓库现有 PowerShell 入口选择相关组件，例如：

```powershell
./scripts/run-checks.ps1 -Name LauncherChecks,CompatibilityChecks
```

需要实际游戏文件、账号或 Windows 环境的检查不由 Python 版本回归代替。


## 服务器收藏实例绑定

```sh
dotnet run --project test/LauncherChecks -c Release -- --server-favorites-only
```

检查旧配置、强类型接口保存、配置持久化、状态快照、旧调用保留绑定、清除绑定、删除及无编号。
该场景也在完整 LauncherChecks 中执行，由现有 Windows CI 运行。
它不启动游戏，不能替代真实账号、Java 和服务器进服验收。


## 收藏服务器启动

```sh
dotnet run --project test/LauncherChecks -c Release -- --server-launch-only
```

覆盖真实接口路由、缺少绑定/错误地址/不存在实例、默认端口与 IPv6、临时参数隔离、普通启动、
进程失败后的重试，以及不同入口的并发目录预留。该场景也由完整 LauncherChecks 和既有 Windows CI 执行。
测试使用短命令进程替代 Minecraft，不执行真实账号登录或网络进服，不能声称真实游戏验收完成。
