# Codex 结果报告

- 任务：表现层、输入与完整 UI 流程
- 里程碑：M7
- 分支：`codex/m7-presentation-ui-input`
- Git Commit：`abdd15969023d3c3f9ba968063aae99a800d5264`
- 日期：2026-07-26

## 1. 实现范围

完成 Render Snapshot 到 Unity 表现对象的绑定、插值、生成、回收和代际句柄校验；为 Actor、
Projectile、Area、Pickup 建立持久池，并把受击、死亡、状态、VFX、伤害数字和测试音频路由到
集中式请求池。敌人使用稳定 VisualProfileId，缺失配置和其他实体使用程序化 fallback。

完成 Gameplay、UI、Debug 三套 Input Action Map，覆盖键鼠、主流手柄、Action Map 切换、重映射、
死区和震动接口。完成 Bootstrap、MainMenu、CharacterSelect、MapSelect、Loading、RunHUD、Pause、
LevelUpDraft、RunResult、Settings、ContentError 页面及 Presenter/ViewModel 边界；完整流程接入真实
M6 Placeholder RunSession，而不是 UI 内模拟规则。

完成基础摄像机、有限地图边界、可关闭屏幕震动、闪光、伤害数字和自动瞄准设置。未实现正式 UI
皮肤、正式音画资源、正式语言表、局外系统、30 分钟 Soak 或目标实体规模性能测试。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Assets/Game/Application/GameState.cs`、`GameStateMachine.cs`、`PresentationContracts.cs`、`RunSession.cs` | 完整应用状态、UI-safe DTO、流程/重映射接口、只读快照与事件边界 |
| `Assets/Game/Presentation/EntityViews.cs`、`ViewPools.cs`、`PresentationCoordinator.cs` | 四类 View、稳定身份绑定、插值、池化协调、fallback 和过期句柄拒绝 |
| `Assets/Game/Presentation/PresentationRequests.cs`、`PresentationEffects.cs` | 受击/死亡/状态请求、池化 VFX、共享 Canvas 伤害数字和池化测试 AudioSource |
| `Assets/Game/Presentation/M7InputRouter.cs`、`PresentationCameraRig.cs` | 三套 Action Map、键鼠/手柄、重映射与可访问性输入、摄像机边界和震动 |
| `Assets/Game/UI/GameFlowPresenter.cs`、`RuntimeUiRoot.cs` | 11 页 ViewModel/Presenter 和程序化 uGUI Placeholder |
| `Assets/Game/Infrastructure/M7DemoRunFactory.cs`、`M7GameFlowController.cs`、`M7RuntimeHost.cs` | 真实 M4-M6 内容组合、完整流程命令、单一运行时循环和确定性测试入口 |
| `Assets/Game/Infrastructure/GameBootstrapper.cs`、相关 asmdef | Bootstrap 组合 M7；显式程序集依赖，不使用查找或 Service Locator |
| `Assets/Game/Editor/M7ProjectSetup.cs`、`Assets/GameAssets/Placeholder/M7InputActions.asset` | 可重复的 M7 工程配置和 Input System 资产 |
| `Assets/Scenes/Bootstrap.unity`、`ProjectSettings/EditorBuildSettings.asset` | 引用 M4-M7 内容、摄像机和项目 Input Action Asset |
| `Assets/Tests/EditMode/M7PresentationUiInputTests.cs`、`Assets/Tests/PlayMode/M7FullFlowPlayModeTests.cs`、既有治理/Bootstrap 测试 | M7 组件、架构、键鼠/手柄全流程、暂停和销毁清理证据 |
| `Docs/ADR/0009-m7-presentation-ui-input-composition.md` | 表现、UI、输入、应用和模拟之间的新依赖方向与生命周期决定 |
| `Docs/ARCHITECTURE.md`、`TEST_PLAN.md`、`PERFORMANCE_BUDGET.md`、`KNOWN_ISSUES.md` | 数据流、UI 状态、输入/可访问性、测试事实边界和后续事项同步 |

实现提交相对 `framework-m6` 新增 35、修改 18、删除 0，共 53 个文件。

## 3. 关键架构决定

- 采用 ADR 0009：Simulation 只输出 RenderSnapshot 和事件；View 不拥有 Health、Damage、XP、
  Death、Drop 或 Build 真值，也不能直接访问 Simulation Store。
- `M7RuntimeHost` 是唯一每帧组合点：先推进/暂停应用会话，再在下一事件批次前消费事件，最后同步
  Snapshot；实体 View 和短效对象没有各自的 Update。
- 所有高频 View 和表现请求由集中池管理；池只在预热或容量增长时创建对象，回收按
  `(EntityKind, EntityHandle.Generation)` 验证所有权。
- UI 只消费 `IGameFlowController` 的 ViewModel/命令；LevelUpDraft 不复制 M6 候选过滤、权重或资格。
- Input Asset 提供 Gameplay/UI/Debug Map；Gameplay 与 UI 互斥，Debug 为开发期独立 Map。
- 用户可见文本暂存为 Localization Key；正式表、字体回退和伪本地化由 M8 接入。

## 4. 实际执行的命令

```text
git fetch --prune --tags origin
git switch main
git pull --ff-only origin main
git switch -c codex/m7-presentation-ui-input

Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -runTests -testPlatform EditMode -testResults TestResults/M7Baseline/editmode.xml -logFile TestResults/M7Baseline/editmode.log
Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -runTests -testPlatform PlayMode -testResults TestResults/M7Baseline/playmode.xml -logFile TestResults/M7Baseline/playmode.log
Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -executeMethod Game.Editor.ProjectValidationCommand.Run -logFile TestResults/M7Baseline/validation.log

Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -executeMethod Game.Editor.M7ProjectSetup.RunFromCommandLine -logFile TestResults/M7/setup-final.log
Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -runTests -testPlatform EditMode -testResults TestResults/M7Final/editmode.xml -logFile TestResults/M7Final/editmode.log
Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -runTests -testPlatform PlayMode -testResults TestResults/M7Final/playmode.xml -logFile TestResults/M7Final/playmode.log
Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -executeMethod Game.Editor.ProjectValidationCommand.Run -logFile TestResults/M7Final/validation.log
$env:BUILD_OUTPUT='Builds/WindowsDevelopment/AzureSword.exe'; Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -executeMethod Game.Editor.WindowsDevelopmentBuild.BuildFromCommandLine -logFile TestResults/M7Final/build.log

rg -n <禁用模式> Assets/Game/Application Assets/Game/Core Assets/Game/Infrastructure Assets/Game/Presentation Assets/Game/Simulation Assets/Game/UI
git diff --name-status framework-m6..abdd159
git diff --check framework-m6..abdd159
git commit -m "feat: implement M7 presentation UI and input"
```

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| M6 基线 | PASS | `TestResults/M7Baseline`：EditMode 154/154、PlayMode 5/5、Project Validation PASS |
| 编译 | PASS | 最终 EditMode、PlayMode、验证和 Development Build 均完成脚本编译，无 C# 编译失败 |
| EditMode | PASS | `TestResults/M7Final/editmode.xml`：163/163，0 failed，0 skipped |
| PlayMode | PASS | `TestResults/M7Final/playmode.xml`：8/8，0 failed，0 skipped |
| 完整键鼠/手柄流程 | PASS | `KeyboardAndGamepadCompleteMenuUpgradePauseAndResultFlow` 覆盖菜单、真实 Run、升级、暂停、结算和返回主菜单 |
| 暂停与销毁清理 | PASS | 暂停时 Tick 停止且 UI 响应；`DestroyingBootstrapReleasesViewsPoolsAndInputOwner` 通过 |
| 内容/工程验证 | PASS | `TestResults/M7Final/validation.log`：`[Project Validation] PASS` |
| 构建 | PASS | `TestResults/M7Final/build.log`：`[M0 Build] PASS`；Manifest 为 `Succeeded` |
| 性能/Soak | NOT RUN | M7 只验证池生命周期和功能；30 分钟与目标实体规模性能 JSON 固定在 M10 |

## 6. 构建产物

- 配置：Windows x64 Development，Unity `6000.3.20f1`
- 路径：`Builds/WindowsDevelopment/AzureSword.exe`
- 文件 Hash：SHA-256 `5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6`
- Build Manifest：`Builds/WindowsDevelopment/BuildManifest.json`，`result: Succeeded`，生成时间
  `2026-07-26T14:01:39.5301737Z`

## 7. 未执行项目

- Release Build：`NOT RUN`。M7 当前适用门禁为 Windows Development Build。
- 正式本地化表、伪本地化和字体覆盖：`NOT RUN`，按计划由 M8 实现和验证。
- 30 分钟 Soak、1,500 敌人、3,000 投射物、5,000 拾取物压力测试：`NOT RUN`，按计划在 M10 执行。

## 8. 已知限制和风险

- Placeholder UI 当前显示 Localization Key；正式语言表、字体 fallback 和裁切证据尚未建立。
- 玩家、Projectile、Area、Pickup 没有实例级 VisualProfileId，当前按 EntityKind 使用程序化 fallback。
- 池化功能已测试，但未测量目标规模下的池命中率、GC、帧时间和内存趋势。
- Debug Action Map 是 development-only；Release 门禁必须禁用。

## 9. 未完成项

- 当前 M7 强制交付无未完成项。
- `Docs/KNOWN_ISSUES.md` 已登记 M7-KI-001 至 M7-KI-004，没有阻止 M8 开始的 `OPEN` 问题。

## 10. 下一步前置条件

- 通过 GitHub PR 合并本分支，确认 `main` 与合并提交一致并创建 `framework-m7` 标签。
- M8 必须从最终 `framework-m7` 创建独立分支，接入 Localization Table、伪本地化、字体覆盖与
  内容工具；不得把本地化文本或内容规则硬编码到现有 Presenter/View。

## 11. 结论

`COMPLETE`
