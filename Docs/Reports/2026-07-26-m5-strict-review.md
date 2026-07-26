# M5 严格里程碑审查报告

- 里程碑：M5 敌人、刷怪、遭遇与地图运行时
- 审查提示词：`Docs/Survivors_Codex_Documentation_Pack/Prompts/13_MILESTONE_REVIEW_GATE.md`
- 分支：`codex/m5-enemy-spawn-map`
- 基线：`framework-m4` / `ae4e02c2737c778a40d6f9ef24fdc8642d787a72`
- Unity：`6000.3.20f1`
- 日期：2026-07-26

## 里程碑结论

`PASS`

M5 强制交付、自动测试、内容验证和适用 Development Build 全部通过。30 分钟 Soak 与
1,500/3,000/5,000 目标实体压力测试属于 M10 门禁，本次如实标为 `NOT RUN`，不作为 M5
通过证据。

## 验收矩阵

| 验收项 | 结果 | 证据 |
|---|---|---|
| M4 基线、M2 空间网格和 M3 战斗前置 | PASS | 分支从 `framework-m4` 的 `ae4e02c...` 创建；基线 EditMode 125/125、PlayMode 5/5 |
| Enemy Definition 基础属性、碰撞、移动、SkillId、Tags、奖励、VisualProfileId | PASS | Schema 4 Runtime/Authoring/DTO、round-trip 与内容验证 |
| Chase、KeepDistance、Charge、Ranged 行为 | PASS | `ConfiguredBehaviorModulesEnterExpectedStates` |
| 集中式敌人更新、无逐敌人 MonoBehaviour | PASS | `EnemyRuntime` + `EnemyDecisionSystem`；Simulation UnityEngine/Update 扫描零命中 |
| Steering、局部分离和障碍规避无 NaN | PASS | `CoincidentEnemiesUseFiniteSeparationAndSteering` |
| 普通敌人不使用 NavMeshAgent/全局寻路 | PASS | M5 源码静态扫描零命中；Scene 没有行为组件 |
| Spawn Scheduler 与 Spawn Request Buffer | PASS | 延迟请求只由 Cleanup 应用；预算/并发自动测试 |
| Encounter 阶段、预算/间隔曲线、权重、群组、Elite、Boss、并发上限 | PASS | Schema、Validator、`SchedulerRespectsBudgetConcurrencyAndQueuesBossExactlyOnce` |
| 八种 Spawn Pattern | PASS | 8 组 `EverySpawnPatternProducesWalkableFiniteMapPosition` 用例 |
| `FiniteArenaMapRuntime` Walkable/ResolveMovement | PASS | 边界、半径和障碍自动测试 |
| `ChunkedInfiniteMapRuntime` 最小确定性版本 | PASS | 固定种子区块签名与活动窗口；ADR/Architecture 记录释放边界 |
| 地图 Scene 不拥有刷怪逻辑 | PASS | 两张 Scene 各仅一个 GameObject+Transform，占位场景没有 MonoBehaviour |
| Difficulty Snapshot 六类倍率 | PASS | Health/Damage/Speed/SpawnRate/EliteProbability/Reward 的不可变运行时输入与验证 |
| 两张测试地图、四种普通敌人、一个 Boss | PASS | `Assets/GameAssets/Placeholder/TestM5Content/`，全部为 development-only Placeholder |
| 同一 Encounter 用于两张地图 | PASS | 两个 Map 资产和自动测试均引用 `test.encounter.five_minute`/共享 Runtime 定义 |
| 固定种子区块和刷怪序列 | PASS | Chunk signature 与 SpawnChecksum 重复运行相等 |
| Boss 只在指定阶段生成一次 | PASS | Scheduler 与五分钟 Harness 均断言一次 |
| M4 Skill 可由玩家和敌人共同使用 | PASS | `SameM4SkillRuntimeCanBeOwnedByPlayerAndEnemy`，共享 Skill Catalog/Runtime |
| finite 五分钟 Headless | PASS | EditMode 中 9000 Tick；并发、位置、Boss、清理、无效句柄均通过 |
| chunked-infinite 五分钟 Headless | PASS | EditMode 中 9000 Tick；位置、Boss、并发与清理均通过 |
| Schema 1–3 兼容与 Schema 4 门禁 | PASS | 既有回归测试 + `SchemaFourRoundTrip...`、`SchemaFourRejectsLegacyEnemy...` |
| EditMode | PASS | `TestResults/M5Review/editmode.xml`：144/144，0 failed，0 skipped |
| PlayMode | PASS | `TestResults/M5Review/playmode.xml`：5/5，0 failed，0 skipped |
| 内容/工程验证 | PASS | `TestResults/M5Review/validation.log`：`[Project Validation] PASS` |
| Windows x64 Development Build | PASS | Manifest `Succeeded`；`TestResults/M5Review/build.log` 含 `[M0 Build] PASS` |
| Release Build | NOT RUN | M5 适用构建门禁为 Development Build；正式 Release 阶段执行 |
| 30 分钟 Soak 与目标规模性能 JSON | NOT RUN | 明确固定到 M10；M5 五分钟小型 Harness 不外推性能预算 |

## 范围外改动

无。相对 M4 的变更均属于 Schema 4、敌人/刷怪/地图运行时、程序化 Placeholder、自动测试、
ADR/Change Request 或 M5 文档。没有修改 Packages、ProjectSettings、Core、UI、Presentation、
正式资源、第三方资源或存档格式。

## 架构违规

无。

- asmdef 循环检查：PASS；`Game.Core` 无引用，`Game.Simulation` 只引用 Core/Content.Runtime，
  两者 `noEngineReferences: true`。
- `Game.Core`/`Game.Simulation` 的 UnityEngine/GameObject/MonoBehaviour/Scene 等引用：零命中。
- M5 Simulation 的 Resources.Load、全局 Find、NavMeshAgent、Service Locator、运行时反射、
  LINQ、逐敌人 Update/FixedUpdate、高频 Instantiate/Destroy、字符串格式化：零命中。
- UI/View 直接写 Simulation Store：本里程碑没有 UI/View 改动。
- 新增资产只位于 `GameAssets/Placeholder`，没有 ThirdParty/AI/release 内容；全部 `.meta` 存在且
  GUID 无重复。

## 场景检查

- `M5FiniteArena.unity`：一个 `M5FiniteArenaPlaceholder` 根对象，仅 Transform。
- `M5ChunkedInfinite.unity`：一个 `M5ChunkedInfinitePlaceholder` 根对象，仅 Transform。
- 两张测试 Scene 没有 MonoBehaviour、Prefab、缺失脚本或冲突标记。
- `ProjectSettings/EditorBuildSettings.asset` 未修改，唯一启用 Scene 仍为
  `Assets/Scenes/Bootstrap.unity`。

## 实际命令与结果

```text
.\Scripts\test.ps1 -Platform All -ProjectPath F:\Code\AzureSword -ResultsDirectory TestResults\M5Review
=> EditMode 144/144 PASS；PlayMode 5/5 PASS

.\Scripts\validate.ps1 -ProjectPath F:\Code\AzureSword -LogPath TestResults\M5Review\validation.log
=> PASS

.\Scripts\build-windows.ps1 -ProjectPath F:\Code\AzureSword -OutputPath Builds\WindowsDevelopment\AzureSword.exe -LogPath TestResults\M5Review\build.log
=> PASS；StandaloneWindows64 Development；Manifest Succeeded

Get-FileHash Builds\WindowsDevelopment\AzureSword.exe -Algorithm SHA256
=> 5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6

git diff --name-status
git ls-files --others --exclude-standard
git diff --check
rg <禁用模式> Assets/Game/Simulation 及 M5 源码
PowerShell asmdef 依赖循环、meta 缺失/GUID 重复、Scene YAML、Build Settings 检查
=> PASS；手写 C#/Markdown/JSON 无 whitespace error；`git diff --check` 只报告 Unity 自动生成
   `.meta`/`.asset` 的空值尾随空格，符合已接受的 M0-KI-007
```

## 修复文件

- 审查未发现需要修改运行时代码的 FAIL。
- 补充 `Docs/TEST_PLAN.md` 的 M5 已落地覆盖及真实性边界。
- 补充 `Docs/CONTENT_AUTHORING_WORKFLOW.md` 的 Schema 4 作者流程与 Fixture 入口。
- Unity 构建产生的 `AddressableAssetSettings.asset` 非语义行尾改写已恢复；不纳入 M5 diff。

## 未解决问题

没有阻止合并或 M6 的 `OPEN` 问题。已接受/计划限制登记在 `Docs/KNOWN_ISSUES.md`：

- M5-KI-001：最小无限区块运行时不包含正式内容流送。
- M5-KI-002：障碍规避只支持轴对齐矩形和滑轴回退。
- M5-KI-003：Visual Profile 的 View 解析留到 M7。
- M5-KI-004：30 分钟 Soak 和目标实体压力测试留到 M10，当前为 `NOT RUN`。
