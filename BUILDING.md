# Building / 编译

Requires PowerShell 7, a local Dyson Sphere Program installation, BepInEx 5 and DSPModSave. GalacticScale is needed only to build the optional adapter. The script uses Roslyn bundled with PowerShell; no separate .NET SDK is required. Game and dependency assemblies are referenced locally and are not included in this repository.

需要 PowerShell 7、本机《戴森球计划》、BepInEx 5 和 DSPModSave。仅编译可选适配器时需要 GalacticScale。编译器使用 PowerShell 自带的 Roslyn，无需单独安装 .NET SDK。游戏及依赖程序集由本地安装提供，仓库不包含这些文件。

```powershell
pwsh -NoProfile -File ./build.ps1 `
  -GameDir 'D:/Games/Dyson Sphere Program' `
  -BepInExDir 'D:/ModProfiles/Default/BepInEx' `
  -DSPModSaveDir 'D:/ModProfiles/Default/BepInEx/plugins/CommonAPI-DSPModSave'
```

Replace these paths with your own installation locations. `GameDir` contains `DSPGAME_Data`; `BepInExDir` contains `core` and `plugins`. `DSPModSaveDir` defaults to `plugins/CommonAPI-DSPModSave` under BepInEx. A mod manager profile may be separate from the game directory.

请替换为自己的安装路径：`GameDir` 内应有 `DSPGAME_Data`，`BepInExDir` 内应有 `core`、`plugins`。`DSPModSaveDir` 默认指向 BepInEx 下的 `plugins/CommonAPI-DSPModSave`。模组管理器目录可能位于游戏目录之外。

Output / 输出：`artifacts/DiverseBiomes.dll`。核心编译与检查不需要 GS2。

To also build the optional adapter, add `-GalacticScaleDir 'D:/ModProfiles/Default/BepInEx/plugins/Galactic_Scale-GalacticScale'`. This folder must contain `GalacticScale.dll`. Adapter output is `artifacts/DiverseBiomes.GalacticScale.dll`; its references are separate from the core's.

要同时编译可选适配器，增加上面的 `GalacticScaleDir` 参数；对应目录应包含 `GalacticScale.dll`。适配器输出为 `artifacts/DiverseBiomes.GalacticScale.dll`，单独引用 GS2，不使核心依赖 GS2。

The build only writes to `artifacts`. To install manually, close the game and copy the core and, if built, the adapter into `BepInEx/plugins/DiverseBiomes/`. No preloader patcher is needed.

编译仅写入 `artifacts`。手动安装时先关闭游戏，再把核心与已编译的适配器复制到 `BepInEx/plugins/DiverseBiomes/`，无需安装预加载补丁。

Run `verify.ps1` with the same arguments for targeted checks. It checks sampling, saved-plant matching, per-save settings, vanilla theme profiles and current game hooks under Windows .NET Framework. The core is checked before any GS2 assembly is loaded. With `GalacticScaleDir`, it also checks the separate adapter. It does not launch the game or validate rendered vegetation and construction behavior.

必要检查可用相同参数运行 `verify.ps1`，验证采样、存档植物匹配、独立设置持久化、原版主题和当前游戏挂接。核心检查先在未加载 GS2 的环境中运行；提供 `GalacticScaleDir` 时再检查独立适配器。它不启动游戏，画面与实际建造效果仍需游戏内观察。

Lifecycle hook installation can encounter Unity ECall restrictions outside the game. This is reported as `LIMIT`, with signature checks retained; it is not counted as a successful in-game load/reload test.

存档生命周期挂接在游戏外可能受 Unity ECall 限制。检查会明确输出 `LIMIT`，保留接口核对，不将其算作游戏内读档或重载成功。
