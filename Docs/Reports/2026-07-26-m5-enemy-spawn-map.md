# Codex 结果报告

- 任务：敌人、刷怪、遭遇与地图运行时
- 里程碑：M5
- 分支：`codex/m5-enemy-spawn-map`
- Git Commit：待严格审查和 GitHub 集成后回填
- 日期：2026-07-26

## 1. 实现范围

完成 Schema 4 敌人、地图和 Encounter 作者数据及纯运行时定义；实现集中式敌人决策、
Steering、局部分离、轻量障碍规避、Spawn Scheduler、Spawn Request Buffer、八种 Spawn
Pattern、有限地图与最小无限区块地图运行时、Difficulty Snapshot，以及无表现层的五分钟
Headless Harness。创建两张程序化占位测试地图、四种测试敌人、一个测试 Boss 和一套共享
Encounter。

未实现正式地图/敌人美术、表现对象池、正式区块内容流送、30 分钟 Soak 或
1,500/3,000/5,000 目标实体压力测试；这些不属于 M5 当前交付边界。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Assets/Game/Content/Runtime/M5ContentDefinitions.cs`、`M5ContentDtos.cs` | Schema 4 Enemy/Map/Encounter 纯运行时模型、DTO 与确定性数据 |
| `Assets/Game/Content/Runtime/RuntimeContentDefinitions.cs`、`BakedContentCatalogDto.cs`、`ContentPackTopology.cs`、`ContentValidator.cs` | 接入 Schema 4、稳定引用、Hash、兼容边界和验证 |
| `Assets/Game/Content/Authoring/EnemyAuthoring.cs`、`MapAuthoring.cs`、`EncounterScheduleAuthoring.cs`、`ContentBaker.cs` | M5 作者数据、烘焙与版本门禁 |
| `Assets/Game/Simulation/MapRuntime.cs` | `IMapRuntime`、有限地图、最小无限区块地图与 Difficulty Snapshot |
| `Assets/Game/Simulation/SpawnRuntime.cs` | Spawn Request Buffer、八种生成图样及预算/阶段调度器 |
| `Assets/Game/Simulation/EnemyRuntime.cs`、`M5Systems.cs` | 集中式敌人状态、移动决策、分离、规避和系统入口 |
| `Assets/Game/Simulation/M5HeadlessHarness.cs` | 五分钟有限/无限地图无头验证与清理检查 |
| `Assets/Game/Simulation/SimulationWorld.cs`、`SimulationSystems.cs` | M5 显式 Pipeline、地图移动约束与 Cleanup 延迟创建 |
| `Assets/Game/Simulation/SkillRuntime.cs`、`SkillTargetingExecutors.cs` | 玩家与敌人共用 M4 Skill 时的阵营目标过滤 |
| `Assets/Game/Editor/M5TestContentSetup.cs` | 生成并烘焙全部程序化 Placeholder Fixture |
| `Assets/GameAssets/Placeholder/TestM5Content/**` | 两张测试 Scene、五个 Enemy、Encounter、两张 Map、Pack 和 baked Catalog |
| `Assets/Tests/EditMode/M5*.cs`、`StatusContentTests.cs` | Schema、行为、地图、刷怪、确定性、共享 Skill 与五分钟 Harness 测试 |
| `Docs/ADR/0007-m5-enemy-spawn-map-runtime.md` | M5 架构决定 |
| `Docs/CHANGE_REQUEST_M5_ENEMY_MAP_SCHEMA_V4.md` | Schema 4 Change Request 与证据边界 |
| `Docs/ARCHITECTURE.md`、`CONTENT_SCHEMA.md`、`PERFORMANCE_BUDGET.md`、`KNOWN_ISSUES.md` | 架构、Schema、性能事实边界与已知限制 |
| 对应新增 `.meta` | Unity 资产身份文件 |

## 3. 关键架构决定

- 采用 ADR 0007：Enemy、Map、Encounter 都是 Content 驱动的纯运行时定义，Scene 不拥有刷怪时间线。
- 所有敌人由单一紧凑 Sidecar 和集中系统推进，不创建逐敌人 `MonoBehaviour.Update`。
- 普通敌人只使用 Steering、局部分离和轴对齐障碍规避，不引入 NavMeshAgent。
- Spawn 请求只在 Cleanup 阶段应用，继续保持模拟结构变更的单一提交点。
- Schema 1–3 保持读取和旧 Hash 兼容；只有 Schema 4 才允许完整 M5 Enemy/Map/Encounter 数据。
- 无限地图 M5 只实现确定性区块签名与逻辑活动窗口，正式流送后续通过 `IMapRuntime` 扩展。

## 4. 实际执行的命令

```text
git remote -v
git fetch --prune --tags origin
git switch main
git pull --ff-only origin main
git switch -c codex/m5-enemy-spawn-map
Unity.exe -batchmode -nographics -projectPath F:\Code\AzureSword -executeMethod Game.Editor.M5TestContentSetup.RunFromCommandLine
Unity EditMode test run -> TestResults/M5Final/editmode-rerun.xml
Unity PlayMode test run -> TestResults/M5Final/playmode.xml
Unity project validation -> TestResults/M5Final/validation.log
Unity Windows x64 Development Build -> TestResults/M5Final/build.log
Get-FileHash Builds/WindowsDevelopment/AzureSword.exe -Algorithm SHA256
git diff --check
```

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| 编译 | PASS | EditMode 编译并执行；Windows Player 构建成功 |
| EditMode | PASS | `TestResults/M5Final/editmode-rerun.xml`：144/144，0 failed，0 skipped |
| PlayMode | PASS | `TestResults/M5Final/playmode.xml`：5/5，0 failed，0 skipped |
| 内容验证 | PASS | `TestResults/M5Final/validation.log`：`[Project Validation] PASS` |
| 构建 | PASS | `TestResults/M5Final/build.log`：`Build Finished, Result: Success`、`[M0 Build] PASS` |
| 五分钟 Headless 正确性/泄漏 | PASS | 两项 M5 Harness 测试包含在 EditMode 144/144 中；finite/infinite 各 9000 Tick |
| 30 分钟 Soak 与目标规模性能 | NOT RUN | M10 门禁；M5 不以小型 Harness 外推性能预算 |

## 6. 构建产物

- 配置：Unity 6000.3.20f1，Windows x64 Development
- 路径：`Builds/WindowsDevelopment/AzureSword.exe`
- 文件 Hash：`5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6`
- Build Manifest：`Builds/WindowsDevelopment/BuildManifest.json`，`result: Succeeded`

## 7. 未执行项目

- 30 分钟 Soak：按 `Docs/PERFORMANCE_BUDGET.md` 固定在 M10 执行。
- 1,500 敌人、3,000 投射物、5,000 拾取物压力与 Tick 分位 JSON：当前为 `NOT RUN`，M10 执行。
- 正式表现与地图内容流送：分别属于后续表现/内容里程碑，不在 M5 提前实现。

## 8. 已知限制和风险

- 无限地图当前只有确定性逻辑区块窗口，没有正式内容流送和区块存档。
- 障碍规避只支持轴对齐矩形与滑轴回退，不提供全局路径规划。
- `VisualProfileId` 已建立稳定边界，具体 View/Profile 解析留到 M7。
- 五分钟 Harness 是正确性、确定性和实体清理证据，不是最终性能基准。

## 9. 未完成项

- 当前 M5 强制交付项无未完成项。
- 严格里程碑审查和 GitHub 集成尚待本报告之后完成。

## 10. 下一步前置条件

- `13_MILESTONE_REVIEW_GATE.md` 的验收矩阵全部为 PASS，允许明确列出的 M10 项保持 NOT RUN。
- PR 合并后 `main` 与 `framework-m5` 指向同一最终提交，工作树保持干净。
- 开始 M6 前必须取得新的明确里程碑提示词。

## 11. 结论

`COMPLETE`

M5 当前强制编译、测试、内容验证和适用构建均已有真实 PASS 证据；M10 性能项明确为
`NOT RUN`，不计作 M5 已通过的性能结果。
