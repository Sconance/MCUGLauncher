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
