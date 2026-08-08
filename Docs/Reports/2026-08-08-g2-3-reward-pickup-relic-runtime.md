# Codex 结果报告

- 任务：按 Demo 路线实现 G2.3 奖励、灵物与战斗奇物运行时
- 里程碑：G2.3 / M06 Reward、Pickup、Relic
- 分支：`codex/qinglan-demo-implementation`
- Git Commit：提交后填写
- 日期：2026-08-08

## 1. 实现范围

完成四类奖励来源、六种即时灵物、六种战斗奇物、三槽不重复库存、精英三选一、折枝显化宝匣、听风
固定首通、活动/已提交事务幂等、独立 Reward RNG、固定 fallback、局内永久结果增量和 RunSession
选择暂停/恢复。未提前实现 G2.4 RunResult、G2.5 Profile 持久化、G2.6 实际 UI/输入、G2.8 Windows
Build 或 G3 正式资产。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Assets/Game/Simulation/RewardExecutionRuntime.cs` | Reward 操作、Pickup sidecar、活动事务、Relic 库存/选择、结果增量 |
| `QinglanRuntime.cs`、`ProgressionRuntime.cs`、`SimulationWorld.cs` | Demo 容量、Run 装配、暂停和生命周期所有权 |
| `CombatSystems.cs`、`EnemyRuntime.cs`、`QinglanSystems.cs` | 普通/精英/Boss/Map 来源与 RewardResolution 消费 |
| `M6Systems.cs`、`SimulationSystems.cs` | Pickup 扫描和 Cleanup 创建/删除 |
| `Assets/Game/Application/RunSession.cs` | Relic Choice 的 UI-safe 投影、提交和 Clock 恢复 |
| `QinglanG23ContentSetup.cs`、Placeholder/Localization/Addressables | Pack 0.8.0、六 Pickup、六 Relic、宝匣、首通与双语 |
| `QinglanG23RewardTests.cs`、`QinglanG23RewardFlowPlayModeTests.cs` | 8 项 EditMode 和 1 项端到端 PlayMode |
| `Docs/ADR/0021-*`、`PUBLIC_API_FREEZE.md` | 65 条追加 API 决策、迁移、回滚和冻结 |
| `Docs/DemoDevelopment/18_G2_3_*` | 完整模块设计、内容明细、容量和实测值 |

## 3. 关键架构决定

- Simulation 不分支具体青岚 ID；RewardOperation、Definition 引用和 `relic.rule.*` Tag 驱动全部效果。
- 地面奖励在领取前保持活动事务索引，重放不能重复生成；结构变更只经 Cleanup。
- Relic 固定三槽、MaximumLevel=1、不覆盖；Reward RNG 与普通 Offer RNG 独立。
- 永久输出只形成 `RewardResultEntry`，G2.5 才原子写 Profile。
- Demo 组合根容量为 4096 整局事务/512 执行结构；旧公开 128 容量构造签名与行为保留。
- ADR 0021 接受 Simulation 65 条 public 签名追加、0 删除。

## 4. 实际执行的命令

```text
dotnet build free-world.slnx
Unity.exe ... -executeMethod Game.Editor.QinglanG23ContentSetup.RunFromCommandLine ...
Unity.exe ... -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.QinglanG23RewardTests ...
Unity.exe ... -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.QinglanG17PackRewardTests;Game.Tests.EditMode.QinglanG22BossTests;Game.Tests.EditMode.QinglanG23RewardTests ...
Unity.exe ... -runTests -testPlatform EditMode ...
Unity.exe ... -runTests -testPlatform PlayMode ...
Unity.exe ... -executeMethod Game.Editor.ProjectValidationCommand.Run ...
Unity.exe ... -executeMethod Game.Editor.M10ApiFreezeCommand.Run ...
Compare-Object G2.2/Game.Simulation.signatures.txt G2.3/Game.Simulation.signatures.txt
Unity.exe ... -executeMethod Game.Editor.QinglanG16HeadlessCommand.Run ...
M10_ENEMY_ID=qinglan.enemy.grass_spirit M10_TICK_COUNT=900 M10_WARMUP_TICKS=300 Unity.exe ... -executeMethod Game.Editor.M10PerformanceCommand.Run ...
CONTENT_PACK_OUTPUT=.../pack-first Unity.exe ... -executeMethod Game.Editor.ContentPackBuildCommand.Run ...
CONTENT_PACK_OUTPUT=.../pack-second Unity.exe ... -executeMethod Game.Editor.ContentPackBuildCommand.Run ...
Get-FileHash .../qinglan.pack.demo/0.8.0/catalog.json -Algorithm SHA256
```

两次带 `--no-restore` 的构建因 Unity 清空 `Temp/obj/project.assets.json` 返回 `NETSDK1004`；带 Restore
构建通过。新增测试首次因 `Vector2` 命名歧义未编译；另两次带显式 `-quit` 的 Unity 命令只完成域重载，
未生成 XML，均记为 `NOT RUN`。错误的 Validation 类名同样记为 `NOT RUN`，随后使用正确入口重跑。

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| 编译 | PASS | `dotnet build free-world.slnx`，0 error；27 条既有 DTO 警告 |
| Focused EditMode | PASS | `focused-final.xml`，20/20 |
| 全量 EditMode | PASS | `editmode-final.xml`，261/261 |
| 全量 PlayMode | PASS | `playmode-final.xml`，11/11 |
| 12 分钟 Headless | PASS | `headless-final.json`，双实例、2 Boss、0 InvalidHandle |
| 内容/治理验证 | PASS | `validation-final.log` 含 `[Project Validation] PASS` |
| API Freeze | PASS | Simulation 1396 / `4d5bfc3d...c6410c32`；65 添加、0 删除 |
| Pack 双构建 | PASS | 两次各 7 Pack；Qinglan Catalog 字节一致 |
| 性能/Soak | PASS | 复测 Tick p99 4.8810 ms，0 B，GC 0/0/0；首轮 GC FAIL 保留 |
| Windows Build | NOT RUN | G2.8 对完整垂直切片执行；G2.3 路线不要求单独 Build |

## 6. 构建产物

- 配置：Content Pack 0.8.0 / Schema 6 / 150 definitions
- 路径：`TestResults/QinglanDemo/G2.3/pack-first/`、`pack-second/`
- Content Hash：`5f233508384d0f9b4b5babc98571ccd45e0d35319c776f4de217ae99e3107c9d`
- Catalog SHA-256：`b274fc24afa07194682968eb2a290e0b3e12c631a621dd98f2799f0925702236`
- Build Manifest：Windows Player `NOT RUN`；Content Pack report 已生成

## 7. 未执行项目

- 胜负 RunResult 与局内增量汇总：G2.4。
- Profile v3 原子合并、保存重试和平台事件：G2.5。
- 实际奖励 UI、键鼠/手柄输入和可访问性：G2.6。
- Windows x64 Development Build 与完整垂直切片：G2.8。
- 正式视觉/音频/字体与目标硬件 GPU、1% Low：G3。

## 8. 已知限制和风险

- 当前奖励和奇物均为程序化 Placeholder 数据，没有正式 Sprite、动画、VFX 或音频。
- `RewardResultEntry` 是局内真值，尚未持久化；异常退出不会把本次永久增量写入 Profile。
- M10 性能首轮出现 Unity 后台 GC，完全同配置冷启动复测通过；两份证据均保留。
- 实际选择页面未实现，当前 PlayMode 只验证 Application 命令边界。

## 9. 未完成项

- G2.3 Simulation、Application 命令、内容和测试范围内无未完成项；跨模块持久化与表现按后续工作包继续。

## 10. 下一步前置条件

- G2.4 只能读取 `RewardResultEntry` 汇总 RunResult，不直接写 Profile。
- G2.5 必须以稳定 Outcome 事务原子合并，并复用 G2.3 的 Owned Unique 快照/结果 ID。
- G2.6 必须通过 `RunSession.CurrentRewardChoice` 提交，不复制候选或资格规则。

## 11. 结论

`COMPLETE`
