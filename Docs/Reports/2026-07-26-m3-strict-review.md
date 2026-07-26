# Codex 结果报告

- 任务：M3 严格只读审查与最小修复
- 里程碑：M3
- 分支：`codex/m3-combat-status`
- Git Commit：未创建
- 日期：2026-07-26

## 1. 实现范围

先冻结源代码完成 Git、程序集、禁用模式、内容、资产、本地化和测试覆盖审查，再运行
未修复版本的全量测试与验证。审查发现两个同属 M3 临时护盾边界的问题，并用两个
EditMode 回归测试复现后做局部修复：

1. 临时护盾当前值已耗尽时，状态过期只改变最大值，原实现不发 `ShieldChanged`。
2. 两个各自有限的护盾容量相加可能得到正无穷，原实现仍接受状态并污染 Shield 状态。

没有实现 M4 技能运行时、投射物、正式内容、存档、本地化表或新的性能后端。

## 2. 新增和修改文件

最终工作区相对当前 Git 基线共有 13 个已跟踪修改、43 个新增、0 个删除。

### 2.1 已跟踪修改（13）

- `Assets/Game/Content/Authoring/ContentBaker.cs`
- `Assets/Game/Content/Runtime/BakedContentCatalogDto.cs`
- `Assets/Game/Content/Runtime/ContentPackTopology.cs`
- `Assets/Game/Content/Runtime/ContentValidator.cs`
- `Assets/Game/Content/Runtime/RuntimeContentDefinitions.cs`
- `Assets/Game/Simulation/EntityStores.cs`
- `Assets/Game/Simulation/SimulationPrimitives.cs`
- `Assets/Game/Simulation/SimulationSystems.cs`
- `Assets/Game/Simulation/SimulationWorld.cs`
- `Docs/ARCHITECTURE.md`
- `Docs/CONTENT_AUTHORING_WORKFLOW.md`
- `Docs/CONTENT_SCHEMA.md`
- `Docs/TEST_PLAN.md`

### 2.2 新增（43）

- `Assets/Game/Content/Authoring/StatusEffectAuthoring.cs`
- `Assets/Game/Content/Authoring/StatusEffectAuthoring.cs.meta`
- `Assets/Game/Content/Runtime/RuntimeStatusDefinition.cs`
- `Assets/Game/Content/Runtime/RuntimeStatusDefinition.cs.meta`
- `Assets/Game/Core/CombatTaxonomy.cs`
- `Assets/Game/Core/CombatTaxonomy.cs.meta`
- `Assets/Game/Core/StatId.cs`
- `Assets/Game/Core/StatId.cs.meta`
- `Assets/Game/Editor/M3TestStatusSetup.cs`
- `Assets/Game/Editor/M3TestStatusSetup.cs.meta`
- `Assets/Game/Simulation/CombatBuffers.cs`
- `Assets/Game/Simulation/CombatBuffers.cs.meta`
- `Assets/Game/Simulation/CombatPrimitives.cs`
- `Assets/Game/Simulation/CombatPrimitives.cs.meta`
- `Assets/Game/Simulation/CombatSystems.cs`
- `Assets/Game/Simulation/CombatSystems.cs.meta`
- `Assets/Game/Simulation/Stats.cs`
- `Assets/Game/Simulation/Stats.cs.meta`
- `Assets/GameAssets/Placeholder/TestStatusContent.meta`
- `Assets/GameAssets/Placeholder/TestStatusContent/TestBurningStatus.asset`
- `Assets/GameAssets/Placeholder/TestStatusContent/TestBurningStatus.asset.meta`
- `Assets/GameAssets/Placeholder/TestStatusContent/TestM3StatusContentPack.asset`
- `Assets/GameAssets/Placeholder/TestStatusContent/TestM3StatusContentPack.asset.meta`
- `Assets/GameAssets/Placeholder/TestStatusContent/TestM3StatusContentPack.baked.json`
- `Assets/GameAssets/Placeholder/TestStatusContent/TestM3StatusContentPack.baked.json.meta`
- `Assets/GameAssets/Placeholder/TestStatusContent/TestShieldedStatus.asset`
- `Assets/GameAssets/Placeholder/TestStatusContent/TestShieldedStatus.asset.meta`
- `Assets/GameAssets/Placeholder/TestStatusContent/TestSlowStatus.asset`
- `Assets/GameAssets/Placeholder/TestStatusContent/TestSlowStatus.asset.meta`
- `Assets/Tests/EditMode/CombatInfrastructureTests.cs`
- `Assets/Tests/EditMode/CombatInfrastructureTests.cs.meta`
- `Assets/Tests/EditMode/CombatStatusTests.cs`
- `Assets/Tests/EditMode/CombatStatusTests.cs.meta`
- `Assets/Tests/EditMode/ModifierCollectionHotPathTests.cs`
- `Assets/Tests/EditMode/ModifierCollectionHotPathTests.cs.meta`
- `Assets/Tests/EditMode/StatusContentTests.cs`
- `Assets/Tests/EditMode/StatusContentTests.cs.meta`
- `Assets/Tests/EditMode/StatusLifecycleEdgeTests.cs`
- `Assets/Tests/EditMode/StatusLifecycleEdgeTests.cs.meta`
- `Docs/ADR/0005-m3-combat-status.md`
- `Docs/CHANGE_REQUEST_M3_STATUS_SCHEMA_V2.md`
- `Docs/Reports/2026-07-26-m3-combat-status.md`
- `Docs/Reports/2026-07-26-m3-strict-review.md`

### 2.3 本次最小修复触及文件

| 文件 | 修复摘要 |
|---|---|
| `Assets/Game/Simulation/CombatBuffers.cs` | ShieldChanged 增加最大值前后状态与容量差值 |
| `Assets/Game/Simulation/CombatSystems.cs` | 最大值单独变化也发事件；非有限聚合在写入前拒绝 |
| `Assets/Tests/EditMode/CombatInfrastructureTests.cs` | 增加耗尽后过期、有限容量聚合溢出两个回归用例 |
| `Docs/TEST_PLAN.md` | 记录新增护盾边界覆盖 |
| `Docs/ADR/0005-m3-combat-status.md` | 固化 ShieldChanged 与有限容量不变量 |
| `Docs/Reports/2026-07-26-m3-combat-status.md` | 标记初始报告已由严格审查结果接续 |
| `Docs/Reports/2026-07-26-m3-strict-review.md` | 本审查报告 |

## 3. 关键架构决定

- 不改变程序集依赖、Content Schema、存档格式、Tick 率或系统顺序。
- `ShieldChanged` 代表完整 Shield 状态变化，因此同时携带 Current 与 Maximum 的前后值；
  保留原 `Delta` 语义，新增 `MaximumDelta`。
- 护盾状态继续只接受有限非负容量。聚合结果在任何状态写入和事件产生前校验，失败时沿用
  已有原子回滚路径。
- 对应长期约束已补入 `Docs/ADR/0005-m3-combat-status.md`。

## 4. 实际执行的命令

```text
Get-Content AGENTS.md、Docs/MASTER_PLAN.md、Docs/ARCHITECTURE.md、
  Docs/CONTENT_SCHEMA.md、Docs/CODEX_WORKFLOW.md、Docs/EXECUTION_ORDER.md、
  Docs/TEST_PLAN.md、Docs/PERFORMANCE_BUDGET.md、ADR、Change Request、结果模板
git -c safe.directory=E:/ai/free-world status --short --branch
git -c safe.directory=E:/ai/free-world diff --name-status
git -c safe.directory=E:/ai/free-world diff --cached --name-status
git -c safe.directory=E:/ai/free-world ls-files --others --exclude-standard
git -c safe.directory=E:/ai/free-world diff --stat
rg --files -g '*.asmdef'
PowerShell DFS asmdef 循环检查
rg 禁用 API、Unity 层引用、UI/View Store 写入、热路径可疑分配与 Health 写入

$env:UNITY_PATH='C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe'
.\Scripts\test.ps1 -Platform All -ResultsDirectory 'TestResults\M3StrictReviewPreFix'
.\Scripts\validate.ps1 -LogPath 'TestResults\M3StrictReviewPreFix\validation.log'
.\Scripts\test.ps1 -Platform EditMode -ResultsDirectory 'TestResults\M3StrictReviewRepro'
.\Scripts\test.ps1 -Platform EditMode -ResultsDirectory 'TestResults\M3StrictReviewFix'
.\Scripts\test.ps1 -Platform All -ResultsDirectory 'TestResults\M3StrictReviewFinal'
.\Scripts\validate.ps1 -LogPath 'TestResults\M3StrictReviewFinal\validation.log'
.\Scripts\build-windows.ps1 `
  -OutputPath 'Builds\WindowsDevelopmentM3StrictReview\AzureSword.exe' `
  -LogPath 'TestResults\M3StrictReviewFinal\build-windows.log'

Get-FileHash -Algorithm SHA256 `
  'Builds\WindowsDevelopmentM3StrictReview\AzureSword.exe'
git -c safe.directory=E:/ai/free-world diff --check
```

修复前复现的 Unity 子进程完成并写出失败 XML 后，外层 PowerShell 等待进程未自行返回，
因此终止了已无 Unity 子进程的包装调用；失败结果来自实际生成的 XML 和 Unity 日志，
不是推断结果。

## 5. 验收矩阵

| 验收项 | 结果 | 证据 |
|---|---|---|
| 14 个稳定 StatId 与 Runtime StatIndex | PASS | `BuiltInStatCatalogMapsFourteenStableIds` |
| Modifier 六阶段顺序、Priority、Duration | PASS | Modifier 顺序、Override 优先级、过期回退测试 |
| 同 StackingGroup 规则 | PASS | 高优先级、同优先级后加入、过期恢复测试 |
| DamagePacket / DamageContext 字段 | PASS | 代码审查及 DamageApplied Context 断言 |
| 伤害集中于 DamageResolutionSystem | PASS | Health 写入静态扫描；无公开 Health 写 API |
| 暴击、Armor/Resistance、Shield、Health | PASS | 伤害结算与固定种子测试 |
| 伤害上下界 | PASS | 负值、正无穷与自定义 MaximumDamage 测试 |
| 四种状态叠层策略与 MaxStacks | PASS | 四策略综合测试 |
| Tick、过期、驱散、免疫 | PASS | 状态生命周期与边界测试 |
| Burning、Slow、Shielded Placeholder | PASS | Schema 2 baked catalog、内容验证 |
| ProcDepth 上限与截断计数 | PASS | 直接请求及周期触发截断测试 |
| 死亡事件只触发一次 | PASS | 双致死包测试 |
| 无效目标安全失败 | PASS | 无效 Handle 伤害测试 |
| 固定种子可复现 | PASS | 20 次暴击序列比较 |
| 四类结构体事件与 Tick 清空 | PASS | catch-up、零 Tick、自定义 Pipeline、护盾事件测试 |
| 技能/测试命令不能直接写 Health | PASS | API/赋值静态审查；只允许初始化、上限协调和伤害系统 |
| 状态定义不持有 Unity Object | PASS | 纯运行时定义反射审计与 noEngineReferences |
| 旧测试全部通过 | PASS | 最终 EditMode 97/97、PlayMode 5/5 |
| asmdef 无循环且方向正确 | PASS | 0 个 asmdef 改动；DFS 无循环；治理测试通过 |
| 禁用 API / Locator / View 直写 | PASS | 最终 rg 扫描无匹配 |
| 内容、存档、资产和本地化规则 | PASS | Schema CR/ADR、无 Save 改动、仅 Placeholder、全部显示字段使用 Key、验证 PASS |
| 30 分钟 Soak / 1,500-3,000-5,000 压力 | NOT RUN | 按 PERFORMANCE_BUDGET 在 M10 启用 |

### 修复前失败复现

| 用例 | 结果 | 最小复现与根因 |
|---|---|---|
| `ConsumedTemporaryShieldStillEmitsCapacityChangeWhenItExpires` | FAIL | 先授予 10 护盾、完全消耗、同 Tick 过期；期望两个事件但只有伤害事件。事件结构和发射条件只观察 Current，遗漏 Maximum 10→0。 |
| `TemporaryShieldApplicationRejectsAggregateCapacityOverflow` | FAIL | Actor 最大护盾 `float.MaxValue`，再申请有限 `float.MaxValue` 贡献；状态被接受。`old + delta` 先溢出为正无穷，再被 `Math.Max` 保留。 |

失败证据：`TestResults/M3StrictReviewRepro/editmode.xml`，97 total、95 passed、2 failed。

### 最终门禁

| 检查 | 结果 | 证据 |
|---|---|---|
| Unity 编译 | PASS | 最终 EditMode、PlayMode、Build 均完成脚本编译；最终日志无 CS error/warning |
| EditMode | PASS | 97/97，`TestResults/M3StrictReviewFinal/editmode.xml` |
| PlayMode | PASS | 5/5，`TestResults/M3StrictReviewFinal/playmode.xml` |
| 内容/项目验证 | PASS | `[Project Validation] PASS`，最终 validation.log |
| Windows Development Build | PASS | Manifest `Succeeded`、StandaloneWindows64、Development |
| 性能/Soak | NOT RUN | M10 门禁，当前不能宣称通过 |

## 6. 构建产物

- 配置：Unity `6000.3.20f1`，StandaloneWindows64，Development
- 路径：`Builds/WindowsDevelopmentM3StrictReview/AzureSword.exe`
- SHA-256：`5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6`
- Build Manifest：`Builds/WindowsDevelopmentM3StrictReview/BuildManifest.json`
- Placeholder Catalog Hash：`42073ed8bf84f09fdbf91a7d140ceca06b4420e1660926b2dcf1d9c06b657b5b`

## 7. 范围外改动与未执行项目

- 范围外改动：无。Schema 2、Actor 战斗侧车、事件 Flush、复用数组及三项 Fixture 都直接
  服务 M3 交付或验收；没有 M4 代码、正式资源、第三方包、Save 或平台改动。
- Release Build：NOT RUN；当前适用门禁是 Windows Development Build。
- 30 分钟 Soak 与目标实体规模压力 JSON：NOT RUN；固定在 M10 启用。

## 8. 架构违规、已知限制和风险

- 未解决架构违规：无。
- 静态扫描命中的 `HeadlessSimulationHarness.string.Format` 只在运行结束生成摘要；
  Simulation 内 Dictionary 均在构造/高水位初始化，Modifier 属性读取不创建集合。
- `GameBootstrapper.Destroy` 只处理一次性重复 Bootstrap，不是高频对象生命周期。
- 状态标签匹配仍是线性比较；完整并发性能和长期内存趋势未在 M3 测量。

## 9. 未完成项

- 当前 M3 强制交付、测试、验证和适用构建无未完成项。
- Git 暂存、Commit、Push 未执行，因为用户未要求。

## 10. 下一步前置条件

- 人工接受本审查报告、ADR 0005 和 Schema 2 兼容性影响。
- Review Gate 接受后才可开始 M4；不得在本分支提前实现技能或投射物运行时。

## 11. 结论

里程碑结论：`PASS`

任务结论：`COMPLETE`
