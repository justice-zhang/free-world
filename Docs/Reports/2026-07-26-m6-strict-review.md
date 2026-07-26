# M6 严格里程碑审查报告

- 里程碑：M6 局内成长、构筑、联动与进化
- 审查提示词：`Docs/Survivors_Codex_Documentation_Pack/Prompts/13_MILESTONE_REVIEW_GATE.md`
- 分支：`codex/m6-build-progression`
- 基线：`0384b29c718b1bdd325a75267509cded5c4907f1`（包含 `framework-m5`）
- 实现提交：`fc66a1d47036bbcd29698a2b3b251154f55cfd66`
- Unity：`6000.3.20f1`
- 日期：2026-07-26

## 里程碑结论

`PASS`

M6 强制交付、自动测试、内容验证和适用 Development Build 全部通过。30 分钟 Soak、
1,500 敌人、3,000 投射物、5,000 拾取物和性能分位 JSON 属于 M10，本次如实标为
`NOT RUN`，不作为 M6 通过证据。

## 验收矩阵

| 验收项 | 结果 | 证据 |
|---|---|---|
| M5 基线已验收且在当前历史中 | PASS | `framework-m5` peeled 为 `14198e3...`，是 M6 基线 `0384b29...` 的祖先；基线 EditMode 144/144、PlayMode 5/5、验证 PASS |
| XP、等级曲线和多次连续升级 | PASS | `ExperienceCurveCarriesOverflowAcrossMultipleLevels` 覆盖 4 次连续升级与余量 |
| 敌人死亡奖励、经验拾取和 LevelUp Request | PASS | `EnemyDeathCreatesCollectibleExperiencePickupInFixedPipeline`；死亡排队、Cleanup 创建、Pickup/Experience/Request 顺序 |
| Skill/Passive 槽位、重复等级、最大等级和替换策略 | PASS | `InventoriesEnforceSlotsDuplicateLevelsMaximumAndReplacement` 及 Offer 满槽/满级测试 |
| Offer 候选、权重、前置、互斥、满槽和满级过滤 | PASS | `OfferStreamIsWeightedReproducibleRerollableAndBanishable`、`OfferFilteringHonorsMaximumSlotsAndMutualExclusion` |
| 专用 Run 派生随机流和诊断历史 | PASS | 固定流 ID；父战斗流额外调用不影响结果；记录 RootSeed、Calls 和 Generate/Reroll/Select/Banish/Skip |
| 固定 Seed 候选可复现 | PASS | 两个生成器的首轮及连续 Reroll 序列完全一致 |
| Reroll 可预测且产生不同序列 | PASS | 连续 Reroll 两端一致，并至少出现一次不同候选序列 |
| Banish 后不再出现目标 | PASS | Banish 后候选扫描断言目标不存在 |
| BuildState 集中拥有 Skills、Passives、Traits、Tags、Active Synergies、Evolution Eligibility | PASS | `BuildState` 单一真值与 `SynergyConditionsOutputsAndTagsRemainCentralizedInBuildState` |
| 五类 Condition Evaluator | PASS | Fixture 同时覆盖 OwnsContent、HasTagCount、SkillLevelAtLeast、StatAtLeast、MapHasTag |
| 五类 Synergy Outputs | PASS | AddModifier、UnlockOffer、AddEffectOp、TransformSkill、GrantTrait 均有行为断言；补充测试确认基础与附加 Effect 同次解析 |
| Evolution Definition 与 Consume Policy | PASS | `EvolutionTransformsSkillAndConsumesConfiguredPassive` 覆盖资格、转换、消费和资格重算 |
| 构筑标签随内容变化更新 | PASS | 被动消费后 tag count 从 1 变 0；Trait/转换后标签断言 |
| Run State、升级暂停、应用层命令、Run End/Result | PASS | `RunSessionPausesForCommandSelectionAndProducesResult` 覆盖暂停、Select/Reroll/Banish/Skip、恢复和结果统计 |
| 暂停升级时 SimulationClock 停止且测试时钟可控 | PASS | Runner 在 Request 后中断 catch-up 并 Pause；命令后 Resume；暂停状态 Advance 返回 0 |
| 两个测试 Synergy 和一个测试 Evolution | PASS | `Assets/GameAssets/Placeholder/TestBuildContent/`；Pack 共 11 条，Bake Hash `2498f729...88bd` |
| 新 Synergy/Evolution 只需配置 | PASS | Schema 5 ScriptableObject→Runtime→DTO→Compiler 路径，无具体流派类 |
| 不存在 FireBuild/CritBuild 等硬编码类 | PASS | 全仓静态扫描零命中 |
| 候选规则不在 UI/Application | PASS | `RunSession` 只提交命令；过滤和权重只在 `BuildState`/`OfferGenerator` |
| 10 分钟自动玩家 Harness | PASS | 相同 Seed 两次各 18,000 Tick；自动移动/拾取/选择；统计与 Checksum 一致，清理后无泄漏 |
| Schema 1–4 兼容和 Schema 5 JSON/Hash | PASS | 既有完整回归 + 真实 Unity `JsonUtility` Schema 5 round-trip 1/1 |
| 内容/本地化/资产规则 | PASS | 稳定 ContentId、本地化 Key、程序化 Placeholder；无 ThirdParty、AI 正式资产或来源不明文件 |
| EditMode | PASS | `TestResults/M6Final/editmode.xml`：154/154，0 failed，0 skipped |
| PlayMode | PASS | `TestResults/M6Final/playmode.xml`：5/5，0 failed，0 skipped |
| 内容/工程验证 | PASS | `TestResults/M6Final/validation.log`：`[Project Validation] PASS` |
| Windows x64 Development Build | PASS | Manifest `Succeeded`；EXE SHA-256 `5D7EEB...C9C6` |
| 30 分钟 Soak 与目标规模性能 JSON | NOT RUN | M10 计划项；10 分钟小型 Harness 不外推为性能通过 |

## Git Diff 与范围

- 相对 M6 基线，实现提交新增 73、修改 19、删除 0，共 92 个文件。
- 变更均属于 Schema 5、作者/Bake、Simulation/Application、Placeholder、测试或同步文档。
- `.unity`、`.prefab`、`.asmdef`、Packages、ProjectSettings、ThirdParty、AI 正式资产均无变更。
- Unity Build 产生的 Addressables 纯行尾噪声经 Git 重索引后未进入提交。
- 未发现范围外功能、M7 UI、局外商店或正式数值/资产。

## 架构与禁用模式审查

| 检查 | 结果 | 说明 |
|---|---|---|
| asmdef 方向和循环 | PASS | 未修改 asmdef；Unity 四项门禁完成程序集编译，无新增依赖或循环 |
| Simulation/Core 引用 UnityEngine | PASS | `using UnityEngine`/`UnityEngine.` 静态扫描零命中 |
| GameObject.Find / FindObjectOfType / Resources.Load | PASS | M6 范围和全局静态扫描零命中 |
| 高频 LINQ、反射、字符串格式化 | PASS | 固定 Tick 路径无 System.Linq、Reflection 或格式化；目录构建阶段集合不在 Tick 热路径 |
| 全局 Service Locator | PASS | 静态扫描零命中；依赖由构造或 `InitializeProgression` 显式注入 |
| 高频 Instantiate/Destroy、逐敌人 Update | PASS | Simulation 静态扫描零命中；结构变化仍由 Cleanup Buffer 应用 |
| UI/View 直写 Simulation Store | PASS | M6 未实现 View/UI；Application 只调用命令接口 |
| 稳定 ID、存档和 Unity Object 边界 | PASS | Runtime/Simulation 保存 ContentId/RuntimeContentIndex，不保存 Unity Object；无存档格式变更 |
| 手写文件 whitespace | PASS | C#、Markdown、JSON 和 asmdef 的 cached diff check 为 0；Unity 序列化空字段尾空格按 M0-KI-007 接受 |
| Unity 资产身份 | PASS | 无重复 GUID、无 `m_Script: {fileID: 0}`；五个具体 Authoring 类型拆分至同名文件 |

## 审查中发现并完成的最小修复

| 文件 | 修复 |
|---|---|
| `Assets/Game/Content/Runtime/M6ContentDtos.cs` | `JsonUtility` 会为不适用的嵌套 DTO 产生空对象；解析器改为按 Output 类型只解析必要字段，避免空 Effect/Modifier 被误判为有效数据 |
| `Assets/Tests/EditMode/M6ProgressionTests.cs` | JSON round-trip 改为真实 Unity `JsonUtility`；补充 AddEffectOp 行为断言 |
| `Assets/Game/Content/Authoring/{Passive,Trait,Synergy,Evolution,UpgradeOffer}Authoring.cs` | 具体 ScriptableObject 类型拆分到同名文件，修复生成资产潜在 `m_Script: 0` |
| `Assets/Game/Simulation/BuildState.cs`、`SkillRuntime.cs` | Synergy 附加效果热路径由 ContentId 改为预解析 RuntimeContentIndex 比较 |

修复后重新运行相关单测和全量 EditMode；最终结果 154/154。

## 实际命令与结果

| 命令类别 | 结果 |
|---|---|
| M6 Placeholder Setup/Bake | PASS，11 条，Hash `2498f729967afbfb3bc60faeb75f6544e4199941ca54595917a3c103178a88bd` |
| Schema JSON 定向测试 | PASS，1/1 |
| Synergy AddEffectOp 定向测试 | PASS，1/1 |
| 全量 EditMode | PASS，154/154 |
| 全量 PlayMode | PASS，5/5 |
| Project Validation | PASS |
| Windows Development Build | PASS，Manifest `Succeeded` |
| staged diff/name-status/禁用模式/GUID 扫描 | PASS；Unity 生成 YAML 尾空格按既有治理规则接受 |

完整命令列于 `Docs/Reports/2026-07-26-m6-build-progression.md`。

## 未解决问题

- 没有阻止 M6 合并的 `OPEN` 问题。
- 30 分钟 Soak、目标实体规模与性能分位 JSON 为 `NOT RUN`，计划在 M10。
- Synergy 一次性锁存、M7 UI 边界和正确性 Harness 的性能事实边界已登记在
  `Docs/KNOWN_ISSUES.md`。
