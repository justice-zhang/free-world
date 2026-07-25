# Codex 结果报告

- 任务：M2 固定 Tick 模拟内核严格审查门禁
- 里程碑：M2
- 基准：当前分支 `HEAD`
- 分支：`codex/m2-simulation-kernel`
- Git Commit：未创建
- 日期：2026-07-25
- 最终结论：`PASS`

## 1. 实现范围

先只读复核 M2 提示词、验收标准、全部工作树差异、程序集图、禁用模式、内容/存档/
资产/本地化规则和未修改实现的完整门禁。只读审查发现并用失败测试确认两个 M2
范围内问题：

1. 一次 `FixedTickRunner.Advance` 执行多个追赶 Tick 时，前序 Tick 的事件在调用者
   重新获得控制前被清空。
2. `MovementSystem` 把速度阈值同时用于状态判定和位置积分，导致极小但合法的非零
   速度完全不移动。

仅移动事件缓冲的清理边界、修正非零速度积分、添加两个回归测试并同步既有 M2 契约。
未增加系统、内容、表现、存档、第三方包或后续里程碑功能。

## 2. 新增、修改和删除文件

相对当前分支 `HEAD` 的最终工作树：

- 修改 3 个：
  - `Docs/ADR/0002-simulation-model.md`
  - `Docs/ARCHITECTURE.md`
  - `Docs/TEST_PLAN.md`
- 新增 23 个：
  - `Assets/Game/Simulation/EntityStores.cs` 及 `.meta`
  - `Assets/Game/Simulation/HeadlessSimulationHarness.cs` 及 `.meta`
  - `Assets/Game/Simulation/RenderSnapshot.cs` 及 `.meta`
  - `Assets/Game/Simulation/SimulationBuffers.cs` 及 `.meta`
  - `Assets/Game/Simulation/SimulationClock.cs` 及 `.meta`
  - `Assets/Game/Simulation/SimulationPrimitives.cs` 及 `.meta`
  - `Assets/Game/Simulation/SimulationSystems.cs` 及 `.meta`
  - `Assets/Game/Simulation/SimulationWorld.cs` 及 `.meta`
  - `Assets/Game/Simulation/SpatialGrid.cs` 及 `.meta`
  - `Assets/Tests/EditMode/SimulationKernelTests.cs` 及 `.meta`
  - `Docs/SIMULATION_KERNEL.md`
  - `Docs/Reports/2026-07-25-m2-simulation-kernel.md`
  - `Docs/Reports/2026-07-25-m2-review-gate.md`
- 删除 0 个。
- 暂存 0 个。

本次严格审查实际修复的文件：

- `Assets/Game/Simulation/SimulationBuffers.cs`
- `Assets/Game/Simulation/SimulationClock.cs`
- `Assets/Game/Simulation/SimulationSystems.cs`
- `Assets/Game/Simulation/SimulationWorld.cs`
- `Assets/Tests/EditMode/SimulationKernelTests.cs`
- `Docs/ADR/0002-simulation-model.md`
- `Docs/ARCHITECTURE.md`
- `Docs/SIMULATION_KERNEL.md`
- `Docs/TEST_PLAN.md`
- `Docs/Reports/2026-07-25-m2-simulation-kernel.md`
- `Docs/Reports/2026-07-25-m2-review-gate.md`

## 3. 关键决定

- Event Buffer 的消费边界必须与调用者可观察边界一致。同一次 Runner Advance 的所有
  Tick 事件形成一个批次；下一次实际执行 Tick 的 Advance 或 Step 才开始新批次。
  零 Tick Advance 不清空最新事件。
- 位置积分不使用“移动阈值”裁掉合法速度。M2 对任意非零速度积分并设置 Moving；
  零速度仍保持原朝向并清除 Moving。
- 保持 M2 四系统 Pipeline、30 Hz、Store、Handle、网格和快照 API 不变。
- 没有引入事件消费系统、View、Jobs/Burst 或 M3 逻辑。

## 4. 验收矩阵

| 验收项 | 结果 | 证据 |
|---|---|---|
| SimulationClock 固定 30 Hz、累积时间、追赶上限 | PASS | `FixedTickProducesSameTicksAndStateAcrossPresentationDeltas`、`ClockCapsCatchUpAndRetainsBacklog` |
| 暂停和单步 | PASS | `PauseIgnoresElapsedTimeAndSingleStepRunsExactlyOnce` |
| EntityHandle Index/Generation、Free List、失效检测 | PASS | Handle 删除、复用和失效读写测试 |
| Actor/Projectile/Area/Pickup Dense Store | PASS | Store 创建、扩容、复用测试 |
| Swap-back 后其他 Handle 有效 | PASS | `SwapBackKeepsMovedEntityHandleValid` |
| SimulationWorld、ISimulationSystem、显式 M2 Pipeline | PASS | 默认顺序和实际调用顺序测试 |
| Pipeline 仅含移动、生命周期、清理、快照 | PASS | 静态源码审查与顺序断言 |
| 任意非零速度正确移动 | PASS | 新增 `MovementIntegratesAnyNonZeroVelocity` |
| RandomStream 种子、派生流和调用规则 | PASS | 固定序列及父流调用无关派生测试 |
| 禁止 UnityEngine.Random | PASS | Simulation 全源扫描零命中 |
| Spatial Grid 插入、更新、删除、半径、邻近 | PASS | 暴力比对及更新/删除/邻近测试 |
| 命令缓冲避免遍历期结构改变 | PASS | Lifetime 只排队、Cleanup 应用；结构命令测试 |
| 追赶 Tick 事件不会在 Advance 返回前丢失 | PASS | 新增 `CatchUpAdvanceRetainsEventsFromEveryExecutedTick` |
| RenderSnapshot 前后位置、朝向、标记及插值 | PASS | Snapshot 内容和插值测试 |
| Headless Harness 创建 Actor、移动并导出摘要 | PASS | 固定种子摘要测试 |
| Headless Harness 不创建 GameObject | PASS | GameObject 数量前后相同测试 |
| 活动、创建、删除、失效 Handle、Tick 时间诊断 | PASS | 生命周期/清理/诊断测试 |
| 不同 Render Delta 得到相同 Tick 和结果 | PASS | 60 FPS 与 20 FPS 等总时长测试 |
| 固定种子重复产生相同移动结果 | PASS | 180 Tick、8 Actor 重复摘要测试 |
| 无效 Handle 不读写复用后的其他实体 | PASS | 旧 Handle 读写失败且新实体状态未改变 |
| Simulation Assembly 不引用 MonoBehaviour/Scene/表现资源 | PASS | asmdef `noEngineReferences=true`；源码扫描；治理测试 |
| 无逐实体 Update | PASS | Simulation 无 MonoBehaviour/Update；批量 Dense 循环 |
| M1 内容加载保持通过 | PASS | 最终 PlayMode 5/5 |
| ADR 0002、执行顺序、Store/Handle/快照文档同步 | PASS | ADR、ARCHITECTURE、SIMULATION_KERNEL |
| 不实现伤害、状态、技能、AI、地图 | PASS | 全部 Diff 审查 |
| 不提前全面 Job 化 | PASS | 单线程实现，无 Jobs/Burst API |
| 不使用静态全局 World | PASS | 静态访问扫描零命中，World 由 Runner 显式持有 |
| asmdef 依赖无循环 | PASS | 本地 asmdef DFS：`CYCLES NONE`；EditMode 治理测试 |
| UI/View 不直接写 Simulation Store | PASS | Presentation/UI 源码扫描零命中 |
| 内容 Schema、稳定 ID、存档格式未改变 | PASS | 对应路径和 Packages/ProjectSettings Diff 为空 |
| 正式资产、第三方资产、provenance 规则 | PASS | 未新增或修改任何表现/第三方资产 |
| 用户可见文字本地化规则 | PASS | 未新增 UI/View 或用户可见文案 |
| 编译 | PASS | 最终 Unity 测试和 Development Build 均成功，无编译错误 |
| EditMode | PASS | `TestResults/M2ReviewFinal/editmode.xml`：50/50 |
| PlayMode | PASS | `TestResults/M2ReviewFinal/playmode.xml`：5/5 |
| 内容/项目验证 | PASS | 最终日志含 `[Project Validation] PASS` |
| Windows x64 Development Build | PASS | BuildManifest `result=Succeeded` |
| 30 分钟性能/Soak | NOT RUN | M2 未修改已测量热点；正式 Soak 属后续性能门禁 |
| 构建后 Player 交互 Smoke | NOT RUN | M2 未修改场景、输入或 UI；实际 Development Build 已生成 |

## 5. FAIL 最小复现、根因和修复

修复前新增两个回归测试后，`TestResults/M2ReviewReproduction/editmode.xml` 为
`48 PASS / 2 FAIL`：

| FAIL | 最小复现 | 根因 | 最小修复 |
|---|---|---|---|
| Catch-up 事件丢失 | 创建 lifetime=0 的 Projectile，一次 Advance 2 Tick；期望 Tick 1 Removed 事件 1 个，实际 0 | `SimulationWorld.RunTick` 每 Tick 调用 `Events.BeginTick`，但调用者只能在整个 Advance 返回后消费 | Runner 在一个实际执行 Tick 的 Advance/Step 开始时清理一次；World 每 Tick 不再清理 |
| 微小速度冻结 | Actor 速度 `(0.00001, 0)` 运行 1 Tick；期望 X=`3.33333332E-07`，实际 0 | `LengthSquared > 1e-8` 同时控制位置积分，合法非零速度低于阈值时完全跳过 | 始终执行 `position += velocity * dt`；用分量非零判断 Moving/朝向 |

修复后同一测试程序集为 `50/50 PASS`。

## 6. 范围外改动

- `NONE`。
- `Assets/Game/Infrastructure/GameBootstrapper.cs` 中已有一次低频
  `Destroy(gameObject)`，不在当前 Diff，且属于重复 Bootstrap 清理，不是高频实体路径。
- 构建和测试生成物位于 Git 忽略目录，不属于源代码改动。

## 7. 架构违规

- 最终未发现未解决的架构违规。
- Game.Simulation 仅依赖 `Game.Core`、`Game.Content.Runtime`，并保持
  `noEngineReferences: true`。
- Simulation 中 UnityEngine、反射、LINQ、Service Locator、Instantiate/Destroy、
  场景和表现类型扫描均为零命中。
- Presentation/UI 对四类 Store 写 API 的扫描为零命中。

## 8. 实际执行的命令

```text
Get-Content AGENTS.md, MASTER_PLAN, ARCHITECTURE, CONTENT_SCHEMA,
  CODEX_WORKFLOW, EXECUTION_ORDER, M2 prompt, ADR 0002, TEST_PLAN,
  PERFORMANCE_BUDGET, CODEX_RESULT_REPORT, ProjectVersion

git -c safe.directory=E:/ai/free-world status --porcelain=v2
git -c safe.directory=E:/ai/free-world diff --name-status
git -c safe.directory=E:/ai/free-world diff --cached --name-status
git -c safe.directory=E:/ai/free-world ls-files --others --exclude-standard
git -c safe.directory=E:/ai/free-world diff --numstat

rg -n '<禁用模式集合>' Assets/Game/Simulation -g '*.cs'
rg -n '<禁用调用集合>' Assets/Game -g '*.cs'
rg -n '<Store 写入集合>' Assets/Game/Presentation Assets/Game/UI -g '*.cs'
# PowerShell 解析全部 Assets/**/*.asmdef 并执行 DFS：CYCLES NONE

$env:UNITY_PATH='C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe'
.\Scripts\test.ps1 -Platform All -ResultsDirectory 'TestResults\M2ReviewPreFix'
.\Scripts\validate.ps1 -LogPath 'TestResults\M2ReviewPreFix\validation.log'
.\Scripts\build-windows.ps1
  -OutputPath 'Builds\M2ReviewPreFix\AzureSword.exe'
  -LogPath 'TestResults\M2ReviewPreFix\build-windows.log'

.\Scripts\test.ps1 -Platform EditMode
  -ResultsDirectory 'TestResults\M2ReviewReproduction'
# 外层命令在 600 秒返回 124；Unity 已完成并写出 XML：48 PASS / 2 FAIL。

Unity.exe -batchmode -nographics -projectPath E:\ai\free-world
  -runTests -testPlatform EditMode
  -testResults TestResults\M2ReviewPostFix\editmode.xml
  -logFile TestResults\M2ReviewPostFix\editmode.log
# XML：50/50 PASS

.\Scripts\test.ps1 -Platform All -ResultsDirectory 'TestResults\M2ReviewFinal'
.\Scripts\validate.ps1 -LogPath 'TestResults\M2ReviewFinal\validation.log'
.\Scripts\build-windows.ps1
  -OutputPath 'Builds\M2ReviewFinal\AzureSword.exe'
  -LogPath 'TestResults\M2ReviewFinal\build-windows.log'

Get-FileHash -Algorithm SHA256 -LiteralPath 'Builds\M2ReviewFinal\AzureSword.exe'
git -c safe.directory=E:/ai/free-world diff --check
git -c safe.directory=E:/ai/free-world status --short
```

## 9. 测试和构建结果

| 检查 | 修复前完整门禁 | 回归复现 | 修复后最终结果 |
|---|---|---|---|
| 编译 | PASS | PASS | PASS |
| EditMode | 48/48 PASS | 48 PASS / 2 FAIL | 50/50 PASS |
| PlayMode | 5/5 PASS | NOT RUN | 5/5 PASS |
| 内容/项目验证 | PASS | NOT RUN | PASS |
| Windows Development Build | PASS | NOT RUN | PASS |
| 性能/Soak | NOT RUN | NOT RUN | NOT RUN |

## 10. 构建产物

- 配置：Unity `6000.3.20f1`，`StandaloneWindows64`，Development
- 路径：`Builds/M2ReviewFinal/AzureSword.exe`
- 文件 Hash：`SHA256 5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6`
- Build Manifest：`Builds/M2ReviewFinal/BuildManifest.json`

## 11. 未执行项目

- 30 分钟 Soak 和性能 JSON：`NOT RUN`，原因见验收矩阵。
- 构建后 Player 交互 Smoke：`NOT RUN`，原因见验收矩阵。
- 没有把上述项目描述为通过。

## 12. 已知限制和风险

- Store Handle 只在所属 Store 内有效，跨 Store 必须同时携带 EntityKind。
- Spatial Grid 查询顺序不构成契约。
- 网格和快照索引为单线程 Dictionary 后端，目标规模性能尚未基准化。
- Event Buffer 表示最近一次实际执行 Tick 的 Runner 批次；未来消费者必须在开始下一
  个 Tick 批次前消费，M7 接入表现层时需保持该时序。

## 13. 未解决问题

- 当前 M2 强制范围内无未解决的 `FAIL`。
- 性能/Soak 与 Player 交互 Smoke 保持 `NOT RUN`，不是 M2 本次功能验收失败。

## 14. 下一步前置条件

- 人工检查本报告、最终 Diff、`M2ReviewFinal` XML、验证日志和 BuildManifest。
- M2 审查被接受前不得开始 M3。

## 15. 结论

`PASS`
