# Codex 结果报告

- 任务：按 Demo 文档顺序实现六类敌人与四种精英词缀
- 里程碑：G1.5
- 分支：`codex/qinglan-demo-implementation`
- Git Commit：本报告所在提交
- 日期：2026-08-05

## 1. 实现范围

完成 M07 的六类普通敌人、九个敌人/精英技能、三个状态、四个可组合精英词缀、两个死亡 Reward、
固定两槽词缀组合、一代分裂和友军圆形目标模块。`qinglan.pack.demo` 升级至 0.4.0 / Content Schema 6，
定义总数由 68 增至 93；全部新增资产为程序化 Placeholder。

本包不创建 12 分钟 Encounter、Boss、完整异相灵核三选一、正式 VFX/音频或正式敌人美术；这些分别属于
G1.6、G2.3、G2.6/G3。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Assets/Game/Simulation/EnemyRuntime.cs` | 编译并安装精英 Trait/Skill/Reward，固定两槽组合、一代分裂与死亡输出 |
| `Assets/Game/Simulation/SpawnRuntime.cs` | Encounter 词缀选择、Spawn 绑定、Generation 与战斗/奖励倍率 |
| `Assets/Game/Simulation/SkillTargetingExecutors.cs` | 无分配友军收集与最近友军稳定截断 |
| `Assets/Game/Simulation/CombatSystems.cs` | 死亡结算后执行受限 SpawnEnemy 输出 |
| `Assets/Game/Content/Runtime/QinglanContentDefinitions.cs` | SpawnEnemy 操作码、词缀 Generation/RewardMultiplier |
| `Assets/Game/Content/Runtime/QinglanContentDtos.cs` | 新词缀字段 JSON 往返和旧内容默认值 |
| `Assets/Game/Content/Runtime/QinglanContentValidation.cs` | 词缀、分裂输出和引用验证 |
| `Assets/Game/Content/Runtime/RuntimeSkillDefinition.cs` | `base.targeting.allies_circle` 模块 ID |
| `Assets/Game/Simulation/SkillModuleRegistry.cs` | 注册友军圆形 Targeting Executor |
| `Assets/Game/Editor/QinglanG15ContentSetup.cs` | 可重复执行的 G1.5 内容、Bake 与本地化生成器 |
| `Assets/Game/Editor/M10PerformanceCommand.cs` | 在全部预分配完成后建立 GC 测量基线 |
| `Assets/Game/Editor/CoreApiFreezeValidator.cs` | 更新经批准的 Content Runtime API Freeze Hash |
| `Assets/Tests/EditMode/QinglanG15EnemyEliteTests.cs` | 六项内容、组合、行为、分裂与 600 敌人专项测试 |
| `Assets/Tests/EditMode/QinglanG14ProgressionTests.cs` | 将 Pack 断言改为可累积版本/数量边界，保留 G1.4 语义验证 |
| `Assets/GameAssets/Placeholder/QinglanDemo/*` | 新增 25 个敌人/技能/状态/Reward/词缀定义并重建 Catalog |
| `Assets/GameAssets/Localization/*` | 新内容英文、简中 Key |
| `Docs/ADR/0016-g1-5-elite-affix-execution-and-allied-targeting.md` | 冻结友军目标、分裂与词缀执行契约 |
| `Docs/ChangeRequests/CR-2026-011-elite-affix-composition.md` | 记录实现决定与验收证据 |
| `Docs/DemoDevelopment/13_G1_5_ENEMY_ELITE_SLICE.md` | G1.5 参数、边界、测试与后续前置条件 |
| `Docs/CONTENT_SCHEMA.md`、`Docs/PUBLIC_API_FREEZE.md` | 同步 Schema 与五项批准 API 追加 |
| `Docs/DemoDevelopment/*`、`Docs/KNOWN_ISSUES.md` | 同步目录、追踪矩阵、M07 与延期奖励边界 |

## 3. 关键架构决定

- ADR 0016：`base.targeting.allies_circle` 排除 Owner、死亡中和敌对 Actor，按距离与稳定 ID 排序并受 I0 限制。
- `RewardOperationCode.SpawnEnemy` 只表达 1—2 个、`(0,1]` 倍率的有限子体生成；其余 Reward 操作仍由 G2.3 消费。
- 每个 Elite 最多组合两个兼容词缀；同时检查 Enemy Tags、已选词缀 Tags 和反向排除，Boss 不继承普通词缀。
- 分裂子体固定非 Elite、无词缀、Generation 递增且只允许一代；生命、伤害、XP 与 Loot 同比缩放。
- Spawn 时一次解析并安装 Modifier/Skill，Tick 不解析 ContentId；热路径无 LINQ、反射、字符串格式化或临时集合。
- 保留旧 `RuntimeEliteAffixDefinition`/`SpawnRequest` 构造路径；旧 Schema 4/5 Elite 行为不变。
- Content Runtime API 从 918/`ca5937...` 增至 923/`ebef438d...`；Core、Simulation、Application、Platform Hash 不变。

## 4. 实际执行的命令

```text
dotnet build Game.Simulation.csproj --no-restore
dotnet build Game.Editor.csproj --no-restore
dotnet build Game.Tests.EditMode.csproj
dotnet build free-world.slnx --nologo
Unity.exe -batchmode -nographics -quit -projectPath E:/ai/free-world -executeMethod Game.Editor.QinglanG15ContentSetup.RunFromCommandLine
Unity.exe -batchmode -nographics -projectPath E:/ai/free-world -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.QinglanG15EnemyEliteTests
Unity.exe -batchmode -nographics -projectPath E:/ai/free-world -runTests -testPlatform EditMode -testResults TestResults/QinglanDemo/G1.5/editmode-final.xml
Unity.exe -batchmode -nographics -projectPath E:/ai/free-world -runTests -testPlatform PlayMode -testResults TestResults/QinglanDemo/G1.5/playmode.xml
Unity.exe -batchmode -nographics -quit -projectPath E:/ai/free-world -executeMethod Game.Editor.ProjectValidationCommand.Run
Unity.exe -batchmode -nographics -quit -projectPath E:/ai/free-world -executeMethod Game.Editor.M10ApiFreezeCommand.Run
M10_TICK_COUNT=900 M10_WARMUP_TICKS=120 Unity.exe -batchmode -nographics -quit -projectPath E:/ai/free-world -executeMethod Game.Editor.M10PerformanceCommand.Run
M10_TICK_COUNT=900 M10_WARMUP_TICKS=300 Unity.exe -batchmode -nographics -quit -projectPath E:/ai/free-world -executeMethod Game.Editor.M10PerformanceCommand.Run
Get-FileHash -Algorithm SHA256 Assets/GameAssets/Placeholder/QinglanDemo/QinglanDemoContentPack.baked.json
git diff --check
```

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| 编译 | PASS | `dotnet build free-world.slnx --nologo`，0 error；既有 27 个 DTO/反序列化告警 |
| Focused EditMode | PASS | `TestResults/QinglanDemo/G1.5/editmode-focused-final.xml`，6/6 |
| 全量 EditMode | PASS | `TestResults/QinglanDemo/G1.5/editmode-final.xml`，228/228 |
| PlayMode | PASS | `TestResults/QinglanDemo/G1.5/playmode.xml`，9/9 |
| 内容验证 | PASS | `validation-final.log` 含 `[Project Validation] PASS`；Pack 0.4.0 / 93 |
| API Freeze | PASS | Content Runtime 923/`ebef438d...7192`；其余四程序集 Hash 不变 |
| 性能/Soak | PASS | `performance-final.json`：600/1200/2000/100，900 Tick，Simulation p99 4.7683 ms，Render p99 0.7472 ms，0 B、0 GC |
| 确定性 | PASS | World `4fb3be8f245bbdfb`、Render `d5db451ce0f5e71a` |
| 构建 | NOT RUN | G1.5 路线不要求 Player；完整 Placeholder Pack Build 在 G1.7 |

性能首轮与 300 Tick 预热复跑均为 `FAIL`：热路径为 0 B，但测量场景在 GC 基线之后分配，测量窗记录一次
全代 GC。调整 Harness 的基线位置后，同一 900 Tick 负载复测为上述 `PASS`；未把失败记录隐去或表述为通过。

## 6. 构建产物

- 配置：Content Pack 0.4.0 / Schema 6 / Placeholder
- 路径：`Assets/GameAssets/Placeholder/QinglanDemo/QinglanDemoContentPack.baked.json`
- 文件 SHA-256：`CFA2CE5B2DD373D4104D2EFE23CC3C87A3148C020C93BDC0589BB6057C66B864`
- Canonical Content Hash：`4d1e1a2094443e7ca688d841561375454a93e98c7213c9779068d0b8a8300e5f`
- Build Manifest：NOT RUN（本包未生成 Player）

## 7. 未执行项目

- 12 分钟、21,600 Tick Encounter Headless：NOT RUN；G1.6 首次创建时间轴后执行。
- 敌人视觉轮廓、危险预警、手柄/键鼠实机场景与音频 PlayMode：NOT RUN；属于 G2.6/G3。
- Windows x64 Development Build：NOT RUN；G1.7 完整 Placeholder Pack 门禁。
- 正式资源、目标 GPU 和商业平衡：NOT RUN；当前仍是程序化 Placeholder。

## 8. 已知限制和风险

- QD-KI-010：异相灵核 Reward 已绑定，但 AddCurrency/三选一、暂停、回退与幂等提交尚未由 RewardResolution 消费。
- Headless Render 指标是 Null Device 的 Snapshot/VFX CPU 探针，不代表正式 GPU Frame Time。
- 当前参数只用于机制验证；12 分钟密度、精英频率与最终商业平衡尚未冻结。

## 9. 未完成项

- 当前 G1.5 强制项无未完成项；延期项已明确归属 G1.6、G1.7、G2.3、G2.6/G3。

## 10. 下一步前置条件

- G1.6 只使用已冻结的六 Enemy 与四 Affix，创建 12 分钟 Encounter 并跑双实例 21,600 Tick。
- 达到精英、并发、停止生成、确定性与性能门禁后，方可进入 G1.7 Pack 集成。

## 11. 结论

`COMPLETE`
