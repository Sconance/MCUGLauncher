# 版本号控制

版本号只有一个来源：仓库根目录 `Directory.Build.props` 的 `<McugVersion>`。程序集版本、文件版本与 `AssemblyInformationalVersion` 都由它派生；运行期统一通过 `MCUGLauncher.ProductInfo.Version` 读取，User-Agent、诊断信息和关于页面不再各自维护字面量。

## 规则

1. **只改一处**：发版时修改 `Directory.Build.props` 的 `McugVersion`，同时更新 `CHANGELOG.md` 与 `README.md` 的「当前版本为 **x.y.z**」。
2. **不在源码或界面里写版本**：新增 `MCUGLauncher/x.y.z`、`MCUG Launcher x.y.z` 或页面上的版本文字时，使用 `ProductInfo.Version`；`Brand.cs` 不保存 `Version`。
3. **语义化版本**：格式固定为 `x.y.z`。是否递增由发版决定，重构本身不自动改号。
4. **标签与提交一致**：发版在通过检查的同一提交打 `v<版本>` 标签，Release 附件必须从该标签构建。
5. **历史标签不动**：既有 `v0.20.0`、`v0.20.1`、`v0.20.2` 等标签保持原样，不改写历史提交。
6. **分支职责**：`test` 是 0.22.0 的来源分支，准备合并 `main`；`refactor` 分支已废弃并归档，不再合并或递增版本号。

## 校验

```powershell
python scripts/check-version.py          # 常规检查，CI 会执行
python scripts/check-version.py --tag    # 发版前额外确认 HEAD 已打 v<版本>
```

检查内容：

- `Directory.Build.props` 的 `McugVersion` 是 `x.y.z`；
- `CHANGELOG.md` 最新一条 `## [x.y.z]` 与 `McugVersion` 一致，且存在该版本小节；
- `README.md` 的当前版本行与 `McugVersion` 一致；
- 各 `*.csproj` 不残留 `<Version>`；
- `Brand.cs` 不保存版本常量，`ProductInfo.cs` 存在；
- `src/`、`test/`、`tools/` 的 C# 与 XAML 不硬编码版本字面量；
- 检查程序的程序集版本断言不写死（应与程序集元数据比较）；
- `scripts/build.ps1` 的发版文件名从 `McugVersion` 读取，不再解析已删除的 csproj `<Version>`；
- 使用 `--tag` 时，HEAD 上存在 `v<版本>` 标签。

## 发版顺序

1. 修改 `Directory.Build.props` 的 `McugVersion`；
2. 在 `CHANGELOG.md` 记录本版变更，更新 `README.md` 的当前版本；
3. 运行 `python scripts/check-version.py`、`./scripts/build.ps1` 与相关检查；
4. 提交后打 `v<版本>` 标签，用 `--tag` 再校验一次；
5. 从该标签构建并上传Release 附件；不移动旧标签。
