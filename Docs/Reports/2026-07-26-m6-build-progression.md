# Codex 结果报告

- 任务：局内成长、构筑、联动与进化
- 里程碑：M6
- 分支：`codex/m6-build-progression`
- Git Commit：`fc66a1d47036bbcd29698a2b3b251154f55cfd66`
- 日期：2026-07-26

## 1. 实现范围

完成 Schema 5 Passive、Trait、UpgradeOffer、Synergy 和 Evolution 的作者数据、纯运行时定义、
DTO、稳定 Hash、类型化引用验证及 Run 前编译目录。完成 XP 曲线、敌人死亡经验拾取、连续升级、
Skill/Passive 库存、集中式 BuildState、五类条件、五类 Synergy 输出、Evolution 消费策略、
专用确定性 Offer 随机流、Reroll/Banish/Skip/Select 历史、升级暂停、应用层命令、Run End 和
不可变 RunResult。

创建 2 个测试 Synergy、1 个测试 Evolution、2 个 Passive、1 个 Trait 和 5 个 Offer，均位于
程序化 Placeholder 目录。自动玩家在固定 30 Hz 下运行 10 分钟（18,000 Tick）两次，覆盖
自动移动、拾取、选择升级、确定性统计与显式清理。

未实现升级 UI、正式构筑、正式数值平衡、局外商店、正式资产、30 分钟 Soak 或目标实体规模
压力测试；这些不属于 M6 当前交付边界。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Assets/Game/Content/Runtime/M6ContentDefinitions.cs`、`M6ContentDtos.cs`、`M6ContentValidation.cs` | Schema 5 纯数据、JSON DTO、确定性 Hash 输入和引用/数值验证 |
| `Assets/Game/Content/Runtime/BakedContentCatalogDto.cs`、`ContentPackTopology.cs`、`ContentValidator.cs`、`RuntimeContentDefinitions.cs`、`RuntimeSkillDefinition.cs` | 注册五种新内容类型、Schema 5 门禁和 Skill 最大等级 |
| `Assets/Game/Content/Authoring/M6ContentAuthoring.cs` 及五个具体 Authoring 文件、`ContentBaker.cs` | Passive/Trait/Synergy/Evolution/Offer 作者数据与 Bake 入口；具体 ScriptableObject 文件名与类型名匹配 |
| `Assets/Game/Simulation/ProgressionContentCatalog.cs` | Run 开始前解析 ContentId、StatId 和 Effect 引用 |
| `Assets/Game/Simulation/ProgressionInventories.cs`、`BuildState.cs` | 固定槽库存、重复升级/替换、标签、Trait、Synergy、Evolution 和 Modifier/Effect 真值 |
| `Assets/Game/Simulation/OfferGenerator.cs` | 专用派生随机流、加权无放回候选、Reroll/Banish/Skip/Select 诊断历史 |
| `Assets/Game/Simulation/ExperienceProgression.cs`、`ProgressionRuntime.cs`、`M6Systems.cs` | XP 曲线、拾取侧车、经验结算和 LevelUp Request |
| `Assets/Game/Simulation/SimulationWorld.cs`、`SimulationClock.cs`、`SimulationSystems.cs`、`CombatSystems.cs`、`SkillRuntime.cs` | M6 固定 Pipeline、Cleanup 创建拾取、升级暂停、死亡奖励和构筑附加效果 |
| `Assets/Game/Application/RunSession.cs`、`GameState.cs`、`GameStateMachine.cs` | 应用层升级命令、InRun/LevelUpChoice/RunResult 状态和运行结果 |
| `Assets/Game/Simulation/M6HeadlessHarness.cs` | 10 分钟自动移动、拾取、选择与重复种子校验 |
| `Assets/Game/Editor/M6TestBuildSetup.cs`、`Assets/GameAssets/Placeholder/TestBuildContent/**` | 生成并烘焙 11 个 Schema 5 Placeholder 条目 |
| `Assets/Tests/EditMode/M6*.cs`、`StatusContentTests.cs` | M6 验收、Schema 兼容和 10 分钟自动局测试 |
| `Docs/ADR/0008-m6-build-progression-runtime.md` | M6 Schema、随机流、模拟/应用和性能边界决定 |
| `Docs/ChangeRequests/CR-2026-001-m6-build-progression-schema.md` | Schema 5 Change Request、迁移与回滚说明 |
| `Docs/ARCHITECTURE.md`、`CONTENT_SCHEMA.md`、`CONTENT_AUTHORING_WORKFLOW.md`、`TEST_PLAN.md`、`PERFORMANCE_BUDGET.md` | 架构、内容制作、测试和事实边界同步 |
| 对应新增 `.meta` | Unity 资产身份文件 |

实现提交相对基线新增 73 个文件、修改 19 个文件、删除 0 个文件。

## 3. 关键架构决定

- 采用 ADR 0008：`BuildState` 是库存、标签、Synergy、Evolution 资格、Modifier 和附加 Effect
  的唯一真值，具体流派不进入 Skill 类。
- `BuildRuntimeCatalog` 在 Run 前把稳定 ID 解析为紧凑运行时索引；固定 Tick 不执行字符串查找。
- Offer 使用 Run Seed 派生的独立流 `0x4F4646455253`，战斗随机调用不会改变候选序列；每个
  Generate/Reroll/Select/Banish/Skip 记录种子、调用计数和候选摘要。
- 敌人死亡只排队经验拾取请求，实体结构变化由 Cleanup 应用；Pickup、Experience、
  LevelUpRequest 进入显式固定 Pipeline。
- `RunSession` 只翻译时间与命令；候选过滤、权重和资格规则全部留在 Simulation。
- Schema 由 4 升至 5，已提交 `CR-2026-001`；Schema 1–4 保持兼容，Schema 5 内容必须重 Bake。

## 4. 实际执行的命令

```text
git fetch --prune --tags origin
git switch main
git pull --ff-only origin main
git switch -c codex/m6-build-progression

Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -executeMethod Game.Editor.M6TestBuildSetup.RunFromCommandLine -logFile TestResults/M6Final/content-setup.log
Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.M6ProgressionTests.SchemaFiveRoundTripPreservesBuildDefinitionsAndHash -testResults TestResults/M6Final/schema-json.xml -logFile TestResults/M6Final/schema-json.log
Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -executeMethod Game.Editor.ProjectValidationCommand.Run -logFile TestResults/M6Final/validation.log
Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -runTests -testPlatform EditMode -testResults TestResults/M6Final/editmode.xml -logFile TestResults/M6Final/editmode.log
Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -runTests -testPlatform PlayMode -testResults TestResults/M6Final/playmode.xml -logFile TestResults/M6Final/playmode.log
Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.M6ProgressionTests.SynergyConditionsOutputsAndTagsRemainCentralizedInBuildState -testResults TestResults/M6Final/synergy-effect.xml -logFile TestResults/M6Final/synergy-effect.log
$env:BUILD_OUTPUT='Builds/WindowsDevelopment/AzureSword.exe'; Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -executeMethod Game.Editor.WindowsDevelopmentBuild.BuildFromCommandLine -logFile TestResults/M6Final/build.log

rg -n <禁用模式> Assets/Game/Simulation Assets/Game/Core Assets/Game/Application Assets/Game/Content
git diff --cached --name-status
git diff --cached --check -- '*.cs' '*.md' '*.json' '*.asmdef'
git commit -m "feat: implement M6 build progression"
git commit --amend --no-edit
```

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| 编译 | PASS | 最终 EditMode、PlayMode、验证和 Development Build 均完成脚本编译，无 C# 编译失败 |
| EditMode | PASS | `TestResults/M6Final/editmode.xml`：154/154，0 failed，0 skipped |
| PlayMode | PASS | `TestResults/M6Final/playmode.xml`：5/5，0 failed，0 skipped |
| Schema 5 Unity JSON 回归 | PASS | `TestResults/M6Final/schema-json.xml`：1/1；真实 `JsonUtility` 往返后 Hash 和定义类型保持一致 |
| Synergy 五类输出 | PASS | `TestResults/M6Final/synergy-effect.xml`：1/1；AddEffectOp 与基础 Effect 同次解析；其余输出在全量测试覆盖 |
| 10 分钟自动局 | PASS | EditMode 内相同 Seed 各推进 18,000 Tick，两次统计/Checksum 一致、无实体泄漏、无效 Handle 为 0 |
| 内容验证 | PASS | `TestResults/M6Final/validation.log`：`[Project Validation] PASS` |
| 构建 | PASS | `TestResults/M6Final/build.log`：`[M0 Build] PASS`；Manifest 为 `Succeeded` |
| 性能/Soak | NOT RUN | 10 分钟 Harness 是小型正确性门禁；30 分钟和 1,500/3,000/5,000 压力 JSON 固定在 M10 |

## 6. 构建产物

- 配置：Windows x64 Development，Unity `6000.3.20f1`
- 路径：`Builds/WindowsDevelopment/AzureSword.exe`
- 文件 Hash：SHA-256 `5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6`
- Build Manifest：`Builds/WindowsDevelopment/BuildManifest.json`，`result: Succeeded`

## 7. 未执行项目

- Release Build：`NOT RUN`。M6 当前适用门禁是 Windows Development Build。
- 独立 Player 10 分钟冒烟：`NOT RUN`。M6 自动局是纯模拟 EditMode Harness，尚无 M7 View/UI。
- 30 分钟 Soak 和目标实体规模性能 JSON：`NOT RUN`，按计划在 M10 执行。

## 8. 已知限制和风险

- Synergy 在条件首次满足时一次性激活并锁存；当前不撤销已应用输出。
- 升级 UI 尚未实现，M6 只提供 `RunSession` 命令接口；M7 不得复制候选规则。
- 10 分钟 Harness 证明正确性、确定性和显式清理，不证明最终目标实体规模性能。
- Unity 生成的 `.meta`/YAML 空字段保留尾随空格，按 M0-KI-007 管理；手写 C#、Markdown、JSON
  和 asmdef whitespace 检查为 PASS。

## 9. 未完成项

- 当前 M6 强制交付无未完成项。
- 后续里程碑事项已登记到 `Docs/KNOWN_ISSUES.md`，没有阻止合并的 `OPEN` 问题。

## 10. 下一步前置条件

- 通过 GitHub PR 合并本分支，确认 `main` 与合并提交一致并创建 `framework-m6` 标签。
- M7 必须从最终 `framework-m6` 创建独立分支，只消费 `UpgradeOfferSet`、`RunSession` 和
  `RenderSnapshot`，不得把候选规则或模拟真值搬入 View/UI。

## 11. 结论

`COMPLETE`
