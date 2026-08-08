# Codex 结果报告

- 任务：按 Demo 路线实现 G2.2 折枝与听风 Boss 运行时
- 里程碑：G2.2 / M10 中段与最终 Boss
- 分支：`codex/qinglan-demo-implementation`
- Git Commit：提交后填写
- 日期：2026-08-08

## 1. 实现范围

完成折枝/听风两个独立 Enemy Actor、两个三阶段 BossDefinition、10 个阶段技能、360.0/719.9 秒一次性
Encounter 规则、整数 Tick 时钟、内容驱动阶段技能启停、8 种风脉台修正、控制抗性、三种危险实体
清理策略、Owner Delivery 清理与幂等死亡事务。未提前实现 G2.3 正式奖励、G2.4/G2.5 RunResult/叙事、
G2.6/G2.8 表现 Telegraph 或 G2.8 Windows Build。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Assets/Game/Simulation/BossPhaseRuntime.cs` | 固定容量阶段、规则、快照、清理、控制与一次性事务 |
| `SkillRuntime*.cs`、`EnemyRuntime.cs`、`SimulationWorld.cs` | 阶段技能预加载/抑制、Delivery 回收和 Spawn 装配 |
| `SpawnRuntime.cs`、`EncounterScheduleAuthoring.cs` | 整数 Tick 时钟和 BossDefinition 一次性 Spawn |
| `QinglanRuntime.cs`、`QinglanSystems.cs`、`CombatSystems.cs` | BossPhase 固定 Pipeline 与状态/死亡接入 |
| `QinglanEncounterHeadlessHarness.cs`、`QinglanG16HeadlessCommand.cs` | 动态 Boss 锚点和两 Boss 双实例证据 |
| `QinglanG22ContentSetup.cs`、Placeholder Boss 资产 | Pack 0.7.0、两 Boss、10 Skill、双语和 Addressables |
| `QinglanG22BossTests.cs`、G16/G17/G21 回归 | 7 项 Boss 专项和未来 Pack 数量兼容 |
| `Docs/ADR/0020-*`、`PUBLIC_API_FREEZE.md` | 58 条追加 API 决策、迁移和冻结 |
| `Docs/DemoDevelopment/17_G2_2_BOSS_RUNTIME.md` | 完整模块设计、边界和实测值 |

## 3. 关键架构决定

- Simulation 不分支具体青岚 ID；阶段技能和目标规则全部由 Registry 在装配期解析。
- Spawn 时预加载唯一阶段技能，Tick 只切换 Suppressed 状态；失败路径完整回收已创建实例。
- Encounter 累计整数 Tick，避免 719.9 秒规则因 float 漂移漏生成。
- TelegraphOnly 立即禁伤；旧阶段 Delivery 先解绑再排队 Cleanup，Boss 死亡统一清空。
- ADR 0020 接受 Simulation 58 条 public 签名追加、0 删除；公开无参构造保留。

## 4. 实际执行的命令

```text
dotnet build free-world.slnx
Unity.exe ... -executeMethod Game.Editor.QinglanG22ContentSetup.RunFromCommandLine ...
Unity.exe ... -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.QinglanG16EncounterTests;Game.Tests.EditMode.QinglanG22BossTests ...
Unity.exe ... -runTests -testPlatform EditMode ...
Unity.exe ... -runTests -testPlatform PlayMode ...
Unity.exe ... -executeMethod Game.Editor.QinglanG16HeadlessCommand.Run ...
Unity.exe ... -executeMethod Game.Editor.ProjectValidationCommand.Run ...
Unity.exe ... -executeMethod Game.Editor.M10ApiFreezeCommand.Run ...
Compare-Object G2.1/Game.Simulation.signatures.txt G2.2/Game.Simulation.signatures.txt
M10_ENEMY_ID=qinglan.enemy.grass_spirit M10_TICK_COUNT=900 M10_WARMUP_TICKS=300 Unity.exe ... -executeMethod Game.Editor.M10PerformanceCommand.Run ...
CONTENT_PACK_OUTPUT=.../pack-first Unity.exe ... -executeMethod Game.Editor.ContentPackBuildCommand.Run ...
CONTENT_PACK_OUTPUT=.../pack-second Unity.exe ... -executeMethod Game.Editor.ContentPackBuildCommand.Run ...
Get-FileHash .../qinglan.pack.demo/0.7.0/catalog.json -Algorithm SHA256
```

首个 `dotnet build --no-restore` 因 Unity 清空 `Temp/obj` 的 NuGet assets 而 FAIL，随后实际执行带 Restore
的 `dotnet build` 并通过。首次全量 EditMode 250/252，修复具体 ID 与 Addressables 后最终通过。

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| 编译 | PASS | `dotnet build free-world.slnx`，0 error；27 条既有 DTO 警告 |
| Focused EditMode | PASS | `focused-boss-encounter-final3.xml`，15/15 |
| 全量 EditMode | PASS | `editmode-final3.xml`，253/253 |
| 全量 PlayMode | PASS | `playmode-final2.xml`，10/10 |
| 12 分钟 Headless | PASS | `headless-final.json`，双实例、2 Boss、0 InvalidHandle |
| 内容/治理验证 | PASS | `validation-final3.log` 含 `[Project Validation] PASS` |
| API Freeze | PASS | Simulation 1331 / `e41c43a1...f0249`；58 添加、0 删除 |
| Pack 双构建 | PASS | 两次各 7 Pack；Qinglan Catalog 字节一致 |
| 性能/Soak | PASS | Tick p99 5.2451 ms，0 B，GC 0/0/0，Checksum `b455f50ce958d212` |
| Windows Build | NOT RUN | G2.8 对完整垂直切片执行；G2.2 路线不要求单独 Build |

## 6. 构建产物

- 配置：Content Pack 0.7.0 / Schema 6 / 121 definitions
- 路径：`TestResults/QinglanDemo/G2.2/pack-first/`、`pack-second/`
- Content Hash：`a654cca5b99f355d9d5122fe106fa4bdba73aebcd745ddbbf136446b5214895a`
- Catalog SHA-256：`b2f0a3aca2544619159ca7a1b55b7535c7d79153701e33fcd57c14211a188270`
- Build Manifest：Windows Player `NOT RUN`；Content Pack report 已生成

## 7. 未执行项目

- Boss 正式 RewardDefinition、Pickup、灵物与唯一奇物：G2.3。
- 胜负 RunResult、Profile 原子合并与守风灵叙事：G2.4/G2.5。
- 视觉/音频 Telegraph、HUD、低闪光与色觉替代：G2.6/G2.8。
- Windows x64 Development Build：G2.8 完整垂直切片门禁。
- 目标硬件 GPU、1% Low 和 54,000 Tick 正式内容基准：G3.5。

## 8. 已知限制和风险

- 当前两 Boss 和技能均为程序化 Placeholder 数据，没有正式 Sprite、动画、VFX 或音频。
- 风脉台修正已输出纯模拟参数，但具体空间数量、假预警和节奏的表现消费仍需 G2.6/G2.8 接入。
- G2.2 Boss RewardId 有意为空；只有通用合法 RewardId 的幂等事务路径被测试，真实内容由 G2.3 建立。

## 9. 未完成项

- G2.2 纯模拟与内容范围内无未完成项；M10 跨模块表现验收按后续工作包继续关闭。

## 10. 下一步前置条件

- G2.3 必须复用 `RewardTransactionId(RunId, BossDefinitionId, 0)`，不得在 Pickup/UI 重算一次性状态。
- 新奖励只通过 RewardDefinition/RewardOperation 表达；现有模块不足时先提交 Change Request。

## 11. 结论

`COMPLETE`
