# 构建原生实验版

本分支为 0.24.0-codex，AI 迁移框架尝试。目标 Windows x64。

```powershell
dotnet build src/MCUGLauncher.Avalonia/MCUGLauncher.Avalonia.csproj -c Release
./scripts/build.ps1 -Publish -SingleFile
```

完整包自带 .NET；Lite 依赖 .NET 8 x64 运行时。微软授权页仍需 WebView2 Runtime，其他页面不使用它。本分支不自动上传 Release。

```powershell
./scripts/run-checks.ps1 -Name MuaChecks,DownloadChecks,QueueChecks,CompatibilityChecks,WebBridgeChecks,NativeUiChecks
```

NativeUiChecks 输出原生 Skia 截图到 work/native-ui。测试夹具不接触用户账号和游戏。

人工测试时可传入独立数据目录（仅作为开发参数）：

```powershell
MCUGLauncher.exe --data-directory C:\Temp\MCUG-Avalonia-Test
```

默认仍读取原 MCUGLauncher 数据目录。不要用实验版覆盖正式程序。
