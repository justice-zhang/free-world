# Codex 结果报告

- 任务：干净工程与工程治理
- 里程碑：M0
- 分支：`codex/m0-clean-project`
- Git Commit：本报告与 M0 代码位于同一提交，提交 SHA 以 Git 历史为准
- 日期：2026-07-25

## 1. 实现范围

本次建立可编译、测试、验证和构建的 Unity 空框架：

- 创建 13 个无循环依赖的程序集。
- 创建唯一 Composition Root、`NullPlatformFacade` 和空 `MainMenu` 状态。
- 创建最小 Bootstrap Scene。
- 创建圆、方形、线条三种程序化 Placeholder，并配置 Addressables 标签。
- 创建第三方记录、AI provenance 和 Release/Placeholder 规则验证器。
- 创建 EditMode、PlayMode 测试以及测试、验证和 Windows 构建脚本。
- 生成并启动验证 Windows x64 Development Build。

本次没有实现正式玩法、正式菜单、Steam、存档、构筑或 M1 内容模型，也没有导入
外部图片、音频或参考项目资产。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `.gitignore` | 忽略 Unity 6 生成的 `*.slnx` IDE 文件 |
| `Packages/manifest.json`、`Packages/packages-lock.json` | 安装并锁定 M0 要求的 Unity 官方包 |
| `Assets/Game/**` | 11 个产品和编辑器 asmdef、应用状态、平台抽象、Bootstrap 与编辑器工具 |
| `Assets/Tests/**` | 2 个测试 asmdef、EditMode 和 PlayMode 测试 |
| `Assets/Scenes/Bootstrap.unity` | 最小 Bootstrap Scene |
| `Assets/GameAssets/Placeholder/**` | 程序化圆、方形和线条纹理 |
| `Assets/GameAssets/AI/.gitkeep` | 建立正式 AI 资源隔离目录 |
| `Assets/ThirdParty/.gitkeep` | 建立第三方资源隔离目录 |
| `Assets/AddressableAssetsData/**` | Addressables 设置、Placeholder 条目和标签 |
| `ProjectSettings/EditorBuildSettings.asset` | 构建场景只包含 Bootstrap |
| `ProjectSettings/ScriptableBuildPipeline.json` | 记录 Addressables/SBP 项目级构建设置 |
| `Scripts/test.ps1` | EditMode/PlayMode 命令行测试入口 |
| `Scripts/validate.ps1` | 命令行治理验证入口 |
| `Scripts/build-windows.ps1` | Windows x64 Development Build 入口 |
| `Docs/ARCHITECTURE.md` | 更新 M0 实际 asmdef 依赖图 |
| `README.md` | 增加测试、验证和构建说明 |

`Docs/ADR/0001-unity-version.md` 已核对；其中的精确版本 `6000.3.20f1` 与
`ProjectSettings/ProjectVersion.txt` 一致，因此无需产生无意义修改。

## 3. 关键架构决定

- `Game.Core`、`Game.Content.Runtime`、`Game.Simulation`、
  `Game.Platform.Abstractions`、`Game.Application` 和 `Game.Platform.Null`
  设置 `noEngineReferences: true`。
- `Game.Infrastructure` 是唯一 Unity Composition Root，通过构造函数组合
  `GameApplication`、`GameStateMachine` 和 `NullPlatformFacade`。
- Placeholder 仅由代码生成，统一附加 `placeholder` 和 `development-only`
  Addressables 标签，不附加 `release`。
- 治理验证器同时用于编辑器菜单、命令行和 Build Preprocessor。
- 默认构建产物名采用 M0 前已冻结的项目名 `AzureSword`。
- 没有改变已接受 ADR 所定义的长期架构方向，因此没有新增 ADR。

## 4. 实际执行的命令

```text
C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe
  -batchmode -nographics -projectPath E:\ai\free-world
  -runTests -testPlatform EditMode
  -testResults E:\ai\free-world\TestResults\baseline-editmode.xml
  -logFile E:\ai\free-world\TestResults\baseline-editmode-unity.log

C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe
  -batchmode -nographics -projectPath E:\ai\free-world
  -runTests -testPlatform PlayMode
  -testResults E:\ai\free-world\TestResults\baseline-playmode.xml
  -logFile E:\ai\free-world\TestResults\baseline-playmode-unity.log

Unity Editor: Tools > Free World > M0 > Configure Project

$env:UNITY_PATH='C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe'
.\Scripts\test.ps1
.\Scripts\validate.ps1
.\Scripts\build-windows.ps1

C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe
  -batchmode -nographics -quit -projectPath E:\ai\free-world
  -logFile E:\ai\free-world\TestResults\compile.log

E:\ai\free-world\Builds\WindowsDevelopment\AzureSword.exe
  -batchmode -nographics
  -logFile E:\ai\free-world\TestResults\player-smoke.log

git -c safe.directory=E:/ai/free-world diff --check
```

另外实际检查了 `Scripts/test.ps1` 的配置错误退出码：缺少 `UNITY_PATH` 返回 `2`，
无效编辑器路径返回 `3`，完整测试成功返回 `0`。

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| 初始 EditMode 基线 | NOT RUN | `TestResults/baseline-editmode.xml`：测试数 0 |
| 初始 PlayMode 基线 | NOT RUN | `TestResults/baseline-playmode.xml`：测试数 0 |
| 编译 | PASS | `TestResults/compile.log`：Unity 退出码 0，编译错误匹配数 0 |
| EditMode | PASS | `TestResults/editmode.xml`：6/6，通过 6，失败 0 |
| PlayMode | PASS | `TestResults/playmode.xml`：4/4，通过 4，失败 0 |
| 内容验证 | PASS | `TestResults/validation.log`：`[M0 Validation] PASS` |
| Windows Development Build | PASS | `TestResults/build-windows.log`：`Build Finished, Result: Success.` |
| 构建启动冒烟 | PASS | `TestResults/player-smoke.log`：进入 `MainMenu`，未记录未处理异常 |
| PowerShell 脚本退出码 | PASS | 成功 `0`、缺少变量 `2`、无效路径 `3` |
| 性能/Soak | NOT RUN | M0 没有正式模拟负载；按后续性能里程碑执行 |

## 6. 构建产物

- 配置：Windows x64 Development Build
- 路径：`Builds/WindowsDevelopment/AzureSword.exe`
- 文件 Hash（SHA-256）：
  `5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6`
- Build Manifest：`Builds/WindowsDevelopment/BuildManifest.json`
- Manifest 结果：Unity `6000.3.20f1`、`StandaloneWindows64`、Development、
  `Succeeded`

构建产物与原始测试日志由 `.gitignore` 排除，保留在执行 M0 的本机工作区。

## 7. 未执行项目

- 性能和 Soak Test：M0 没有正式模拟负载，不适用。
- Steam 或 Release 打包：M0 明确禁止提前实现。
- Addressables 远程 Catalog、DLC 和内容更新构建：属于后续里程碑。

## 8. 已知限制和风险

- `MainMenu` 当前只有应用状态和黑色空场景，没有正式 UI，符合 M0 范围。
- 冒烟测试在无图形批处理模式运行；确认进程稳定运行 8 秒并进入 `MainMenu` 后
  主动终止，播放器进程的 `-1` 是人工关闭结果，不是崩溃。
- `NullPlatformFacade` 只提供可调用的 no-op 平台边界，不包含 Steam 实现。
- 初次检查 PowerShell 配置错误退出码时发现参数默认值阶段无法可靠使用
  `$PSScriptRoot`；已将默认项目路径解析移到参数绑定之后并重新验证。
- 完整 `git diff --check` 会对 Unity 自动生成的 `.meta/.asset` 空值字段报告
  trailing whitespace；C#、asmdef、Markdown、PowerShell 和 JSON 文件子集检查通过。
  未手工批量改写 Unity 序列化文件。

## 9. 未完成项

当前 M0 强制交付项无未完成内容。

## 10. 下一步前置条件

- 人工审查并通过 M0 门禁。
- 保持 Unity `6000.3.20f1` 和包锁文件不变。
- 在 M0 审查通过前不得开始 M1。

## 11. 结论

`COMPLETE`
