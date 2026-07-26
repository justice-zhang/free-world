# Codex 结果报告

- 任务：属性、伤害、护盾与状态系统
- 里程碑：M3
- 分支：`codex/m3-combat-status`
- Git Commit：未创建
- 日期：2026-07-26

> 后续审查：本报告记录初始 M3 实现结果。严格 Review Gate 随后复现并修复了两个临时
> 护盾边界问题；最终门禁为 EditMode 97/97、PlayMode 5/5。以
> `Docs/Reports/2026-07-26-m3-strict-review.md` 作为当前审查结论。

## 1. 实现范围

完成 M3 指定范围：14 个稳定属性与 Runtime Index、统一 Modifier 管线、集中伤害结算、
Health/Shield/Armor/Resistance、四种状态叠层、死亡请求与单次死亡事件、四类 Tick 内结构体
事件、ProcDepth 截断诊断，以及 Burning、Slow、Shielded 三个程序化 Placeholder 定义。

没有实现 M4 的完整技能选择、EffectOp 或投射物行为，没有新增正式美术、第三方运行时包、
Jobs 或 Burst 后端。

## 2. 新增和修改文件

| 文件/目录 | 变更摘要 |
|---|---|
| `Assets/Game/Core/StatId.cs`、`CombatTaxonomy.cs` | 稳定属性 ID/Index、Modifier Operation、共享 DamageType/Tags |
| `Assets/Game/Simulation/Stats.cs` | StatCatalog、缓存 ActorStatBlock、六阶段 Modifier、紧凑 StackingGroup |
| `Assets/Game/Simulation/CombatPrimitives.cs` | DamagePacket/Context、Health/Shield/Resistance、状态与战斗侧车 |
| `Assets/Game/Simulation/CombatBuffers.cs` | DamageApplied、StatusApplied、EntityDied、ShieldChanged 缓冲 |
| `Assets/Game/Simulation/CombatSystems.cs` | DamageResolution、StatusTick、Death、EventFlush |
| `Assets/Game/Simulation/EntityStores.cs`、`SimulationWorld.cs` 等 | Actor 战斗记录复用、M3 Pipeline、世界级事件 Flush |
| `Assets/Game/Content/Runtime/RuntimeStatusDefinition.cs` | Schema 2 纯状态定义及不可覆盖 Behavior |
| `Assets/Game/Content/Authoring/StatusEffectAuthoring.cs` | 状态作者数据、验证和 Baker 输入 |
| `Assets/Game/Content/Runtime/*` | Status DTO、Schema 兼容、Hash 与内容验证 |
| `Assets/Game/Editor/M3TestStatusSetup.cs` | 三个测试状态的确定性生成与 Bake 入口 |
| `Assets/GameAssets/Placeholder/TestStatusContent/` | Burning、Slow、Shielded、Pack 与 baked JSON |
| `Assets/Tests/EditMode/*Combat*`、`*Status*`、`ModifierCollectionHotPathTests.cs` | M3 行为、边界、热路径和内容测试 |
| `Docs/ADR/0005-m3-combat-status.md` | M3 长期架构决定 |
| `Docs/ARCHITECTURE.md`、`CONTENT_SCHEMA.md` 等 | 架构、Schema、作者流程和测试计划同步 |

所有新增 C# 和 Unity 资产均有对应 `.meta`。未引入第三方代码或资产。

## 3. 关键架构决定

- Modifier 固定按 Base → AddFlat → AddPercent → Multiply → Clamp → Override → Stat 域边界。
- StackingGroup 在写入阶段转为集合内 `int` key，属性读取不比较字符串、不创建集合。
- 技能、状态和测试命令只排入 `DamagePacket`；伤害导致的 Health 写入集中于
  `DamageResolutionSystem`。
- 状态行为属于经 Baker 验证的 `RuntimeStatusDefinition.Behavior`；申请请求不能覆盖。
- ShieldCapacity 是临时实例贡献，刷新不重复扩容，过期/驱散回收。
- Actor Handle slot 复用战斗记录及内部数组，避免在重复出生/死亡时持续分配。
- Schema 1 保持兼容；包含状态的 Pack 使用 Schema 2 并重新 Bake。
- 对应长期决定：`Docs/ADR/0005-m3-combat-status.md`。

## 4. 实际执行的命令

```text
git -c safe.directory=E:/ai/free-world status --short --branch
git -c safe.directory=E:/ai/free-world switch -c codex/m3-combat-status
.\Scripts\test.ps1 -Platform All -ResultsDirectory 'TestResults\M3BaselineHost'

# Roslyn 辅助编译：Game.Core、Game.Content.Runtime、Game.Simulation、聚焦测试程序集
& 'C:\Program Files\dotnet\dotnet.exe' $arguments

$env:UNITY_PATH='C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe'
.\Scripts\test.ps1 -Platform EditMode -ResultsDirectory 'TestResults\M3Iteration2Host'

& $env:UNITY_PATH -batchmode -nographics -projectPath 'E:\ai\free-world' `
  -executeMethod Game.Editor.M3TestStatusSetup.RunFromCommandLine `
  -logFile 'E:\ai\free-world\TestResults\M3Setup\setup-after-mask-fix.log'

.\Scripts\validate.ps1 -LogPath 'TestResults\M3Validation2\validation.log'
.\Scripts\test.ps1 -Platform All -ResultsDirectory 'TestResults\M3Final'
.\Scripts\build-windows.ps1 `
  -OutputPath 'Builds\WindowsDevelopmentM3\AzureSword.exe' `
  -LogPath 'TestResults\M3Build\build-windows.log'

Get-FileHash -Algorithm SHA256 'Builds\WindowsDevelopmentM3\AzureSword.exe'
git -c safe.directory=E:/ai/free-world diff --check
```

早期沙箱内 Unity 尝试因许可证超时和 SourceAssetDB 锁而未进入测试；主机侧重跑成功。
第一次主机编译发现 NUnit 不支持 `Assert.Multiple`，修复后重跑通过。第一次内容验证发现
Unity 不支持序列化 `ulong` enum 字段，作者层改用 32 位掩码并重新 Bake 后通过。

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| M3 前 EditMode 基线 | PASS | 50/50，`TestResults/M3BaselineHost/editmode.xml` |
| M3 前 PlayMode 基线 | PASS | 5/5，`TestResults/M3BaselineHost/playmode.xml` |
| Unity 编译 | PASS | 最终 EditMode 和 Windows Build 均完成脚本编译 |
| 最终 EditMode | PASS | 95/95，0 failed，`TestResults/M3Final/editmode.xml` |
| 最终 PlayMode | PASS | 5/5，0 failed，`TestResults/M3Final/playmode.xml` |
| 内容/项目验证 | PASS | `[Project Validation] PASS`，`TestResults/M3Validation2/validation.log` |
| Windows Development Build | PASS | StandaloneWindows64，Manifest result `Succeeded` |
| 性能/30 分钟 Soak | NOT RUN | 依 `PERFORMANCE_BUDGET.md` 在 M10 性能门禁执行 |

## 6. 构建产物

- 配置：Unity `6000.3.20f1`，StandaloneWindows64，Development
- 路径：`Builds/WindowsDevelopmentM3/AzureSword.exe`
- SHA-256：`5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6`
- Build Manifest：`Builds/WindowsDevelopmentM3/BuildManifest.json`
- Placeholder Catalog Hash：`42073ed8bf84f09fdbf91a7d140ceca06b4420e1660926b2dcf1d9c06b657b5b`

## 7. 未执行项目

- 30 分钟 Soak、1,500 敌人/3,000 投射物/5,000 拾取物压力 JSON：`NOT RUN`。
  这些规模涉及后续生成、技能与投射物里程碑，固定性能回归门禁按计划在 M10 启用。
- Release Build：`NOT RUN`；当前里程碑要求并实际生成的是 Windows Development Build。

## 8. 已知限制和风险

- 一个状态定义当前组合至多一个 Stat Modifier、一个周期伤害和一个临时护盾贡献；更完整的
  Effect 列表属于后续技能/效果 Schema 里程碑。
- 状态 Tag 免疫与驱散匹配采用正确性优先的线性扫描；只有测量证明为热点后才更换后端。
- 完整高并发性能和长期内存趋势尚未测量，不能据本报告宣称达到最终性能预算。

## 9. 未完成项

- 当前 M3 强制交付、测试、内容验证和适用构建无未完成项。
- Git 暂存、Commit、Push 未执行，因为用户未要求发布变更。

## 10. 下一步前置条件

- 先执行 M3 Review Gate 并接受本报告、ADR 和 Schema 2 兼容性影响。
- Review Gate 通过后才可开始 M4；不得在本分支提前实现完整技能、投射物或选择系统。

## 11. 结论

`COMPLETE`

M3 指定的强制检查已通过；性能/Soak 明确为后续既定门禁，未被表述为通过。
