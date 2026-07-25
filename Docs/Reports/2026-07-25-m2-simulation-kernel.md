# Codex 结果报告

- 任务：固定 Tick 模拟内核
- 里程碑：M2
- 分支：`codex/m2-simulation-kernel`
- Git Commit：未创建
- 日期：2026-07-25

## 1. 实现范围

完成与 Unity 表现层隔离的 30 Hz 固定 Tick 世界、Generation-safe 实体 Handle、
四个专用 Dense Store、显式系统 Pipeline、确定性 RandomStream、统一 2D Spatial
Grid、结构命令/事件缓冲、前后 Tick RenderSnapshot、诊断计数和 Headless Harness。

M2 Pipeline 仅包含 Movement、Lifetime、Cleanup、SnapshotBuild。未实现 M3 及后续的
伤害、状态、技能、敌人 AI、地图、正式 View 或 Jobs/Burst 后端。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Assets/Game/Simulation/SimulationPrimitives.cs`（及 `.meta`） | EntityHandle、EntityKind、状态、诊断、RandomStream |
| `Assets/Game/Simulation/SimulationClock.cs`（及 `.meta`） | 30 Hz 累积时钟、追赶上限、暂停、单步、FixedTickRunner |
| `Assets/Game/Simulation/EntityStores.cs`（及 `.meta`） | Actor/Projectile/Area/Pickup Dense Store、Free List、Generation、Swap-back |
| `Assets/Game/Simulation/SpatialGrid.cs`（及 `.meta`） | 统一 2D Grid、插入/更新/删除/半径与邻近查询 |
| `Assets/Game/Simulation/SimulationBuffers.cs`（及 `.meta`） | FIFO 结构命令缓冲和 Tick 事件缓冲 |
| `Assets/Game/Simulation/RenderSnapshot.cs`（及 `.meta`） | 前后位置、朝向、标记和插值 |
| `Assets/Game/Simulation/SimulationWorld.cs`（及 `.meta`） | World、ISimulationSystem、显式 Pipeline、Store/服务组合 |
| `Assets/Game/Simulation/SimulationSystems.cs`（及 `.meta`） | Movement、Lifetime、Cleanup、SnapshotBuild |
| `Assets/Game/Simulation/HeadlessSimulationHarness.cs`（及 `.meta`） | 固定种子纯 C# Harness 和 invariant 摘要 |
| `Assets/Tests/EditMode/SimulationKernelTests.cs`（及 `.meta`） | 17 个 M2 EditMode 测试 |
| `Docs/ADR/0002-simulation-model.md` | 记录 M2 时钟、Handle、Store、随机、缓冲、网格和快照决策 |
| `Docs/ARCHITECTURE.md` | 更新 M2 实际 Pipeline、Store 生命周期、网格和快照格式 |
| `Docs/TEST_PLAN.md` | 登记 M2 已落地自动化覆盖 |
| `Docs/SIMULATION_KERNEL.md` | 新增 M2 模拟内核操作与数据契约 |
| `Docs/Reports/2026-07-25-m2-simulation-kernel.md` | 本结果报告 |

## 3. 关键架构决定

- 30 Hz 是 M2 锁定常量。Catch-up 上限只限制一次 Advance 的工作量，不丢弃积压。
- Handle 是 Store 局部 `(Index, Generation)`；跨 Store 标识必须附带 EntityKind。
- 四个 Store 独立持有 Dense 数据。内部共享固定运动列实现以避免四份同构错误，
  但不提供组件注册、Archetype 或通用 ECS 查询。
- 系统遍历不直接改变 Store 结构。Lifetime 只排队，Cleanup 是唯一 M2 结构变更点。
- 派生 RandomStream 与父流调用位置无关；复制值类型流会复制当前序列状态。
- Tick 前捕获 Previous，Cleanup 后构建 Current；新实体以前后相同状态进入快照。
- 单线程 Dictionary 后端先保证网格和快照索引正确性；是否更换 Jobs/Burst 后端
  留给有 Profiler/基准证据的后续任务。

长期约束已更新到 ADR 0002。

## 4. 实际执行的命令

```text
git -c safe.directory=E:/ai/free-world status --short
git -c safe.directory=E:/ai/free-world diff --stat
git -c safe.directory=E:/ai/free-world diff --cached --stat

$env:UNITY_PATH='C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe'
.\Scripts\test.ps1 -Platform All -ResultsDirectory 'TestResults\M2Baseline'

# 当前项目被现存 Unity Editor 锁定后，复制 Assets/Packages/ProjectSettings 到：
C:\Users\18321\AppData\Local\Temp\free-world-m2-baseline-ac82b8d7d6844d5aa3d8c3c5a3ca93e4
.\Scripts\test.ps1 -Platform All
  -ProjectPath <上述临时目录>
  -ResultsDirectory 'E:\ai\free-world\TestResults\M2BaselineTemp'

git -c safe.directory=E:/ai/free-world switch -c codex/m2-simulation-kernel

# 实现中编译/EditMode 检查，临时目录随后删除：
.\Scripts\test.ps1 -Platform EditMode
  -ProjectPath C:\Users\18321\AppData\Local\Temp\free-world-m2-compile-9a5d99b0d6c3419ba3e36f1a23f266c0
  -ResultsDirectory 'E:\ai\free-world\TestResults\M2ImplementationCheck'

rg -n '(UnityEngine|GameObject|MonoBehaviour|SceneManager|UnityEditor|UnityEngine\.Random|Resources\.Load|GameObject\.Find|FindObjectOfType|System\.Linq)' Assets/Game/Simulation -g '*.cs'
git -c safe.directory=E:/ai/free-world diff --check

# 最终门禁使用同一个干净临时副本，随后删除：
.\Scripts\test.ps1 -Platform All
  -ProjectPath C:\Users\18321\AppData\Local\Temp\free-world-m2-final-9360602560194fda9d6e0aa0a65d3f7d
  -ResultsDirectory 'E:\ai\free-world\TestResults\M2Final'
.\Scripts\validate.ps1
  -ProjectPath <上述临时目录>
  -LogPath 'E:\ai\free-world\TestResults\M2Final\validation.log'
.\Scripts\build-windows.ps1
  -ProjectPath <上述临时目录>
  -OutputPath 'E:\ai\free-world\Builds\M2WindowsDevelopment\AzureSword.exe'
  -LogPath 'E:\ai\free-world\TestResults\M2Final\build-windows.log'

Get-FileHash -Algorithm SHA256 -LiteralPath 'Builds/M2WindowsDevelopment/AzureSword.exe'

# 严格审查修复后的最终门禁：
.\Scripts\test.ps1 -Platform All -ResultsDirectory 'TestResults\M2ReviewFinal'
.\Scripts\validate.ps1 -LogPath 'TestResults\M2ReviewFinal\validation.log'
.\Scripts\build-windows.ps1
  -OutputPath 'Builds\M2ReviewFinal\AzureSword.exe'
  -LogPath 'TestResults\M2ReviewFinal\build-windows.log'
Get-FileHash -Algorithm SHA256 -LiteralPath 'Builds/M2ReviewFinal/AzureSword.exe'
```

基线第一次命令真实结果为 Unity 退出码 `1`，原因是同一路径已有 Unity Editor
会话及 `Temp/UnityLockfile`；没有生成 XML。未终止该会话，改用源文件完全一致的临时
项目重跑，得到 EditMode 33/33、PlayMode 5/5 PASS。

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| 编译 | PASS | 最终 Unity 测试导入和 Windows Build 均成功；最终日志无 `error CS` |
| EditMode | PASS | `TestResults/M2ReviewFinal/editmode.xml`：50/50，失败 0，跳过 0 |
| PlayMode | PASS | `TestResults/M2ReviewFinal/playmode.xml`：5/5，失败 0，跳过 0 |
| 内容验证 | PASS | `TestResults/M2ReviewFinal/validation.log` 含 `[Project Validation] PASS` |
| 构建 | PASS | BuildManifest `result=Succeeded`，Windows x64 Development |
| 性能/Soak | NOT RUN | M2 未做热点优化；30 分钟 Soak 属于后续压力门禁 |

最终 50 个 EditMode 包含原有 M1 内容测试和 17 个 M2 测试；5 个 PlayMode
Bootstrap/M1 内容加载测试保持通过。

## 6. 构建产物

- 配置：Unity `6000.3.20f1`，`StandaloneWindows64`，Development
- 路径：`Builds/M2ReviewFinal/AzureSword.exe`
- 文件 Hash：`SHA256 5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6`
- Build Manifest：`Builds/M2ReviewFinal/BuildManifest.json`

## 7. 未执行项目

- 30 分钟 Headless Soak：未运行；M2 要求的是功能 Harness 和确定性测试，正式
  Soak/性能 JSON 门禁在后续压力里程碑执行。
- 构建后 Player 交互 Smoke：未运行；本任务没有修改场景、输入、UI 或生命周期
  Bootstrap，实际 Windows Development Build 已生成。

## 8. 已知限制和风险

- Store Handle 不是跨 Store 全局 ID，使用方必须同时保留 EntityKind。
- Spatial Grid 查询顺序不是契约；后续依赖稳定优先级的逻辑必须显式选择或排序。
- 网格和快照索引当前为正确性优先的单线程 Dictionary 实现，尚无目标规模基准。
- Diagnostics 的 Tick 时间依赖主机计时，只用于观测，不可参与确定性规则。
- 初次实现阶段仓库曾被既有 Unity Editor 会话锁定，因而当时使用相同源文件的临时
  副本；严格审查的最终 50/50、PlayMode、验证和构建直接在当前工作树执行通过。

## 9. 未完成项

- M2 强制交付范围内无未完成项。

## 10. 下一步前置条件

- 人工执行 M2 Review Gate，审查本分支 Diff、测试 XML、验证日志和 BuildManifest。
- Review Gate 接受前不得开始 M3。
- M3 必须复用 M2 Command/Event Buffer、Generation Handle 和固定 Pipeline 扩展点，
  不得绕过 Cleanup 直接进行系统遍历期结构变化。

## 11. 结论

`COMPLETE`
