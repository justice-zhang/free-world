# Codex 结果报告

- 任务：按 Demo 路线实现 G2.4 不可变 RunResult 与 Title—Hub 游戏流程
- 里程碑：G2.4 / M01 Game Flow
- 分支：`codex/qinglan-demo-implementation`
- Git Commit：本工作包单一提交
- 日期：2026-08-08

## 1. 实现范围

完成不可变 RunDescriptor、四种 Outcome、Build/Map/Boss/Reward/Pack 结果聚合、最终 Boss 双条件胜利、
Title→角色→地图→Run→Result→Hub→再次出发 Coordinator、真实青岚运行装配和全 Entity 幂等释放。
未提前实现 G2.5 Profile/Recovery/平台事务、G2.6 实际 UI/输入、G2.8 Windows Build 或 G3 正式资产。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Assets/Game/Application/QinglanRunResults.cs` | Descriptor、Pack/Build/探索快照、四 Outcome 和扩展 RunResult |
| `QinglanRunResultBuilder.cs`、`RunSession.cs` | 结束边界聚合、稳定事务、最终 Boss 胜利门槛与兼容构造 |
| `DemoRunCoordinator.cs` | DemoFlowStage、合法转换、Preparing/Ending、Hub/再次出发和结果所有权 |
| `GameApplication.cs` | 依赖排序 Pack Version/Content Hash 快照 |
| `Assets/Game/Infrastructure/QinglanDemoRunFactory.cs` | 真实内容装配、角色起始能力和运行资源 Handle |
| `ProgressionRuntime.cs`、`CombatSystems.cs` | Elite/Boss 击杀统计 |
| `BuildState.cs`、`QinglanRuntime.cs`、`SimulationWorld.cs` | Evolution 快照、事务查询、Seed/RunId 分离装配 |
| `QinglanG24RunFlowTests.cs`、`QinglanG24GameFlowPlayModeTests.cs` | 7 项 EditMode、1 项连续两局 PlayMode |
| `Docs/ADR/0022-*`、`PUBLIC_API_FREEZE.md` | API 兼容、迁移、回滚和新冻结基线 |
| `Docs/DemoDevelopment/19_G2_4_*` | 完整流程、字段、状态、资源和 G2.5 边界设计 |

## 3. 关键架构决定

- Descriptor 在 Run 前复制 Pack ID/Version/Hash；Result 在结束点深复制稳定 ID，不保留 World 别名。
- 胜利同时要求 Encounter Boss 数量与最终 Boss 奖励事务提交，避免首通结果丢失。
- `DemoFlowStage` 独立于旧 `GameState`，保留旧页面/暂停消费者兼容。
- G2.4 只冻结 `HasUncommittedResult`，不触发 M8 保存或平台；ADR 0022 固化 G2.5 接入顺序。
- Run Handle 负责集中回收 Actor、Projectile、Area、Pickup；Dispose 可重复调用。
- API 审计为 Simulation +6、Application +95、删除 0；另外三个冻结程序集逐字节不变。

## 4. 实际执行的命令

```text
dotnet build free-world.slnx --nologo -v:minimal
Unity.exe ... -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.QinglanG24RunFlowTests ...
Unity.exe ... -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.QinglanG24GameFlowPlayModeTests ...
Unity.exe ... -runTests -testPlatform EditMode ...
Unity.exe ... -runTests -testPlatform PlayMode ...
Unity.exe ... -executeMethod Game.Editor.M10ApiFreezeCommand.Run ...
Compare-Object G2.3/api-final/*.signatures.txt G2.4/api-current/*.signatures.txt
Unity.exe ... -executeMethod Game.Editor.ProjectValidationCommand.Run ...
Unity.exe ... -executeMethod Game.Editor.QinglanG16HeadlessCommand.Run ...
M10_ENEMY_ID=qinglan.enemy.grass_spirit M10_TICK_COUNT=900 M10_WARMUP_TICKS=300 Unity.exe ... -executeMethod Game.Editor.M10PerformanceCommand.Run ...
git diff --check
```

新增代码第一次 Unity 导入因缺少 `ObjectiveState` namespace 失败；首次新增 PlayMode 测试因缺少 Core/
Simulation using 且误读 Runner.World 失败；首次聚焦 EditMode 因测试使用不存在的技能 ID 失败。三项均
保留为 FAIL/NOT RUN 证据，修正后重新运行并取得下表最终结果。

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| 编译 | PASS | `dotnet build`，0 error；27 条既有序列化 DTO CS0649 警告 |
| Focused EditMode | PASS | `focused-editmode-release.xml`，7/7 |
| Focused PlayMode | PASS | `focused-playmode-release.xml`，1/1，含连续两局 |
| 全量 EditMode | PASS | `editmode-release.xml`，268/268 |
| 全量 PlayMode | PASS | `playmode-release.xml`，12/12 |
| 内容/治理验证 | PASS | `project-validation-final.log` 含 `[Project Validation] PASS` |
| API Freeze | PASS | Simulation 1402 / `533fa9b4...61c82`；Application 450 / `e423cdb7...71a4` |
| 12 分钟 Headless | PASS | 21,600 Tick×2、2 Boss、0 InvalidHandle、Checksum `049cb8bdc48092eb` |
| 性能/Soak | PASS | Tick p99 4.2181 ms、Render p99 0.6962 ms、0 B、GC 0/0/0 |
| Windows Build | NOT RUN | G2.8 对完整可玩切片执行 |

## 6. 构建产物

- 配置：`qinglan.pack.demo` 0.8.0 / Schema 6 / 150 definitions（G2.4 未改内容）
- 路径：已检入 Baked Catalog；测试证据位于 `TestResults/QinglanDemo/G2.4/`
- Content Hash：`5f233508384d0f9b4b5babc98571ccd45e0d35319c776f4de217ae99e3107c9d`
- Build Manifest：Windows Player `NOT RUN`；G2.8 生成

## 7. 未执行项目

- Profile v3 原子合并、保存重试、Recovery 清理、RunCompleted/平台事件：G2.5。
- 实际标题/选择/结算/据点页面、键鼠/手柄与可访问性：G2.6。
- Scene/Addressables/View Owner 的真实卸载和 Windows x64 Development Build：G2.8。
- 正式视觉、音频、字体、目标硬件 GPU/1% Low：G3。

## 8. 已知限制和风险

- G2.4 的 Hub/再次出发测试允许清除仅在内存的未提交结果；G2.5 必须在真实玩家路径上先提交或阻止离开。
- Result Builder 是低频托管集合路径，不得被移动到固定 Tick。
- `RecoveryRejected` 目前只有纯 Application 命令测试，实际损坏文件检测与删除由 G2.5 接入。
- 当前仍为程序化 Placeholder，没有玩家可见页面或正式表现。

## 9. 未完成项

- G2.4 代码、测试、API Freeze 和文档范围内无未完成项；持久化与 UI 按后续独立工作包继续。

## 10. 下一步前置条件

- G2.5 只消费不可变 `LatestResult`，不得重新读取已释放 World。
- Profile 保存成功后才清 Recovery、发布 RunCompleted/平台事件并允许显示“已保存”。
- `Delta.TransactionId` 重复提交必须返回 AlreadyCommitted，不能重复发放。

## 11. 结论

`COMPLETE`
