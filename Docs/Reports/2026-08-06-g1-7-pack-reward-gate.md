# Codex 结果报告

- 任务：按 Demo 文档顺序完成受控显化奖励与完整 Placeholder Pack 门禁
- 里程碑：Qinglan Demo G1.7
- 分支：`codex/qinglan-demo-implementation`
- Git Commit：提交后回填；本报告与实现同一提交
- 日期：2026-08-06

## 1. 实现范围

完成 CR-2026-007 / ADR 0018 的通用受控 Evolution Reward Choice：独立 Reward RNG、锁定候选资格、
1—3 项加权不放回选择、空池 fallback、事务重放拒绝、BuildState 再验证、Application 暂停/恢复与
普通 Level-up 回归。完成 94 项完整 Placeholder Pack 的 Addressables、双语、Bake、两次 Pack CLI、
API Freeze、性能短测和 Windows x64 Development Build 门禁。

未创建实际宝匣、Boss/精英消费者、Reward/Pickup/Relic 内容、fallback 操作或选择 UI；它们分别属于
G2.2/G2.3/G2.6。未导入任何正式或第三方资产。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Assets/Game/Simulation/RewardChoiceRuntime.cs` | 受控选择状态、快照、独立 RNG、fallback、选择与历史 |
| `Assets/Game/Simulation/BuildState.cs` 等 | 锁定 Evolution 资格、RewardRuntime 事务复用、Progression 暂停接线 |
| `Assets/Game/Application/*` | RewardChoice 状态、UI-safe 投影、选择命令与时钟恢复 |
| `Assets/Game/Editor/QinglanG17PackSetup.cs` | 完整 Pack 重 Bake、Addressables 地址/标签与 CLI 入口 |
| `Assets/AddressableAssetsData/*` | 94 项定义与 Baked Catalog 的稳定交付元数据 |
| `Assets/Tests/EditMode/QinglanG17PackRewardTests.cs` | Pack、双语、随机隔离、fallback、幂等与 RunSession 测试 |
| `Docs/ADR/0018-*`、`Docs/ChangeRequests/CR-2026-007/008-*` | 决策与分期实现状态 |
| `Docs/DemoDevelopment/15_G1_7_PACK_REWARD_GATE.md` | G1.7 完整开发与验收设计 |
| `Docs/PUBLIC_API_FREEZE.md`、测试/性能/风险/执行文档 | Freeze 基线与实际证据同步 |

## 3. 关键架构决定

- 受控奖励选择不复用普通升级随机池或状态，候选真值只在 Simulation。
- 幂等所有权复用 `RewardRuntime`；不建立第二套已提交事务真值。
- 空池提交 fallback ID 时不暂停且不消耗 Reward RNG；实际 fallback 操作留给 G2.3。
- Reward Choice 在 Tick 边界暂停，优先于普通 Level-up；只有成功提交才恢复。
- 所有 G1 内容继续保持 Placeholder/Development-only，Development 可包含，Release 必须阻断。

## 4. 实际执行的命令

```text
dotnet build free-world.slnx --nologo
Unity.exe -batchmode -nographics -projectPath E:\ai\free-world -executeMethod Game.Editor.QinglanG17PackSetup.RunFromCommandLine ...
Unity.exe ... -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.QinglanG17PackRewardTests ...
Unity.exe ... -runTests -testPlatform EditMode ...
Unity.exe ... -runTests -testPlatform PlayMode ...
Unity.exe ... -executeMethod Game.Editor.ProjectValidationCommand.Run ...
Unity.exe ... -executeMethod Game.Editor.M10ApiFreezeCommand.Run ...
Unity.exe ... -executeMethod Game.Editor.M10PerformanceCommand.Run ...
Unity.exe ... -executeMethod Game.Editor.ContentPackBuildCommand.Run ... (CONTENT_PACK_OUTPUT=pack-first)
Unity.exe ... -executeMethod Game.Editor.ContentPackBuildCommand.Run ... (CONTENT_PACK_OUTPUT=pack-second)
powershell -ExecutionPolicy Bypass -File scripts/build-windows.ps1 -OutputPath Builds/WindowsDevelopmentG17/AzureSword.exe ...
AzureSword.exe -batchmode -nographics -logFile TestResults/QinglanDemo/G1.7/player-smoke.log
```

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| 编译 | PASS | `dotnet build free-world.slnx --nologo`，0 error；Unity 脚本编译成功 |
| Focused EditMode | PASS | `TestResults/QinglanDemo/G1.7/editmode-focused-final.xml`，4/4 |
| 全量 EditMode | PASS | `TestResults/QinglanDemo/G1.7/editmode.xml`，239/239 |
| PlayMode | PASS | `TestResults/QinglanDemo/G1.7/playmode.xml`，9/9 |
| 内容验证 | PASS | `validation.log` 含 `[Project Validation] PASS`；Pack 0.5.0/Schema 6/94 |
| API Freeze | PASS | Simulation 1192/`57e2944c...87875`；Application 355/`f57fe00c...f8a6`；其余不变 |
| Pack 双构建 | PASS | 两次各 7 Pack；Qinglan Catalog 字节一致，SHA-256 `9d397996...00cb` |
| 性能/Soak | PASS | 900 Tick＋300 预热；p99 4.2112 ms，0 B，GC 0/0/0，Checksum `b455f50ce958d212` |
| Windows Build | PASS | Manifest `Succeeded`、StandaloneWindows64、Development、全部证据 pass |
| Player 冒烟 | PASS | 8 秒进入 MainMenu；日志无 Error/Exception/FAIL，随后按精确 PID 主动终止 |

## 6. 构建产物

- 配置：Unity `6000.3.20f1` / Windows x64 Development
- 路径：`Builds/WindowsDevelopmentG17/AzureSword.exe`
- 文件 Hash：`5d7eeb5359c2e35e4eb1f6a5844b25c3d7556795bd2f15ec234a2011406bc9c6`
- Build Manifest：`Builds/WindowsDevelopmentG17/BuildManifest.json`
- Qinglan Content Hash：`798dbb302dda57b9f0158e83010ee89392ffdc291cc629715ba357b691ebd5ad`

## 7. 未执行项目

- 实际 Boss/精英 Reward Choice 消费者：`NOT RUN`，Boss 和奖励内容属于 G2.2/G2.3。
- 显化宝匣与 fallback Reward 操作执行：`NOT RUN`，属于 CR-2026-008 / G2.3。
- Reward Choice UI、键鼠/手柄完整流程：`NOT RUN`，属于 G2.6。
- Release Build、正式资产/字体/本地化、目标 GPU：`NOT RUN`，属于 G3。
- 30 分钟正式内容 Soak：`NOT RUN`，G1.7 只执行路线要求的目标规模短测；G3.5 重新执行正式基准。

## 8. 已知限制和风险

- G1.7 fallback 只记录稳定结果 ID 并提交事务，尚未执行治疗、货币或奇物等具体操作。
- 低频候选生成允许快照数组分配；它不在固定 Tick 热路径，UI 高频重建仍需 G2.6 约束。
- Development Player 启动的是现有 Placeholder Bootstrap；完整 Qinglan 可玩流程要到 G2.8 才验证。
- Build Manifest 的 `workingTreeClean=false` 是同提交前生成证据的真实状态，不表示门禁被绕过。

## 9. 未完成项

- QD-KI-010/012：奖励内容、消费者、fallback 执行和 UI。
- QD-KI-011：两个 Boss 与实际地图出生/过渡。
- QD-KI-003/007/008：正式资产 provenance、字体与商业本地化。

## 10. 下一步前置条件

- 进入 G2.1，只实现 M08 五区、三风脉台、三事件、五地标的通用地图运行时与测试。
- 不提前实现 G2.2 Boss、G2.3 Reward 内容或 G2.6 UI；任何新机制继续先走 CR/ADR。

## 11. 结论

`COMPLETE`。
