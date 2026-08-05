# Codex 结果报告：Qinglan Demo G1.2 角色与战斗切片

- 任务：实现陆青野、乘风机制、基础战斗属性与七个状态的实际 Demo 内容
- 里程碑：Qinglan Demo G1.2
- 分支：`codex/qinglan-demo-implementation`
- Git Commit：本报告所在提交
- 日期：2026-08-05

## 1. 实现范围

完成 `qinglan.pack.demo` 的首个实际 Placeholder 内容切片：陆青野 Character、乘风 CharacterMechanic、
三档 Trait 输出和七个 StatusEffect，共 12 个 Schema 6 定义；同时完成真实位移积累、实际受伤严格降
一档、固定容量档位事件、原子绑定/Cleanup、活动状态标签免伤消费者、中英本地化与固定 Seed 回归。

本包不创建武器、心诀、敌人、Boss、Encounter 或正式资源。游风剑和 Character Starting Skill 回填
属于 G1.3；Boss 控制转换属于 G2.2；WASD/摇杆、HUD 与音效专项 PlayMode 属于 G2.6。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Assets/Game/Simulation/QinglanRuntime.cs` | 乘风固定容量事件、有限输入保护、严格单档受伤与 Cleanup |
| `Assets/Game/Simulation/QinglanCharacterBinding.cs` | Character→Mechanic 稳定 ID 解析、容量预检、原子附加和回滚 |
| `Assets/Game/Simulation/StatusDamagePolicy.cs`、`CombatSystems.cs` | 活动状态通用伤害免疫标签消费者与集中伤害管线接入 |
| `Assets/Game/Simulation/SimulationWorld.cs` | Mechanic Tick 批次、事件 Flush 和 Actor 清理解绑 |
| `Assets/Game/Editor/QinglanG12ContentSetup.cs` | 可重复生成 12 定义、双语文本和 Baked Catalog 的 G1.2 工具 |
| `Assets/Game/Editor/ContentCreationService.cs` | 本地化工具增加显式中英文名称/描述重载 |
| `Assets/GameAssets/Placeholder/QinglanDemo/*` | Character、Mechanic、3 Trait、7 Status 与 Pack/Baked JSON |
| `Assets/GameAssets/Localization/UI*.asset` | 24 个内容 Key 的英文与简体中文非空条目 |
| `Assets/Tests/EditMode/QinglanG12CharacterCombatTests.cs` | 内容、位移、伤害、状态、生命周期、Seed、54,000 Tick 分配测试 |
| `Assets/Tests/EditMode/QinglanG11ContractsTests.cs` | 按 G1.2 批准的“受伤严格降一档”更新通用 Fixture 期望 |
| `Docs/DemoDevelopment/*`、`Docs/EXECUTION_LOG.md` | 数值冻结、ID、需求状态、证据与下一工作包边界 |

## 3. 关键架构决定

- Simulation 不比较任何 `qinglan.*` ID；Character 机制由启动期稳定 ID 绑定，伤害免疫由通用 ContentTag 消费。
- 乘风只消费 `IMapRuntime` 解析后的 `PlayerCommand` 实际位移；传送、击退、暂停和硬边界零位移不积累。
- 实际 Shield/Health 损失按 Tick＋Target 去重；先扣 `LossOnDamage=8`，再钳入目标档区间，保证严格只降一档。
- 档位事件使用预分配 pending/batch 数组；非有限值和容量溢出拒绝并计数，不污染模拟状态。
- 所有新增运行时 API 均为程序集内部实现；五个已冻结公共程序集的签名和 Hash 不变，无需新 ADR。

## 4. 实际执行的命令

```text
Unity.exe -batchmode -nographics -projectPath E:/ai/free-world -executeMethod Game.Editor.QinglanG12ContentSetup.RunFromCommandLine
Unity.exe -batchmode -nographics -projectPath E:/ai/free-world -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.QinglanG12CharacterCombatTests
Unity.exe -batchmode -nographics -projectPath E:/ai/free-world -runTests -testPlatform EditMode -testResults TestResults/QinglanDemo/G1.2/editmode-final.xml
Unity.exe -batchmode -nographics -projectPath E:/ai/free-world -runTests -testPlatform PlayMode -testResults TestResults/QinglanDemo/G1.2/playmode.xml
Unity.exe -batchmode -nographics -projectPath E:/ai/free-world -executeMethod Game.Editor.ProjectValidationCommand.Run
Unity.exe -batchmode -nographics -projectPath E:/ai/free-world -executeMethod Game.Editor.M10ApiFreezeCommand.Run
Unity.exe -batchmode -nographics -projectPath E:/ai/free-world -executeMethod Game.Editor.M10PerformanceCommand.Run（900 Tick、目标实体规模）
dotnet build free-world.slnx --nologo
rg（Simulation 内容 ID、Core UnityEngine、禁用查找 API、.meta/GUID 静态检查）
git diff --check
```

首次误用不存在的 `FreeWorld.sln` 路径，MSBuild 返回 MSB1009；确认仓库实际入口为 `free-world.slnx`
后重跑并通过。此失败未被表述为编译门禁通过证据。

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| 编译 | PASS | `dotnet build free-world.slnx`：0 error、27 个既有序列化 DTO CS0649 warning；Unity 编译成功 |
| G1.2 Focused EditMode | PASS | `editmode-focused-final.xml`，6/6；Golden `0xFD82A621E9E5AD8E` |
| 全量 EditMode | PASS | `editmode-final.xml`，209/209 |
| 全量 PlayMode 回归 | PASS | `playmode.xml`，9/9 |
| 内容验证 | PASS | `validation.log` 含 `[Project Validation] PASS`；12 定义与双语 Key 完整 |
| API Freeze | PASS | Core 168、Content 918、Simulation 1160、Application 346、Platform 73；Hash 与 G1.1 相同 |
| 构建 | NOT RUN | G1.2 路线不要求 Build；完整 Placeholder Pack 在 G1.7 构建 |
| 性能/Soak | PASS | 900 Tick p99 10.1121 ms、0 B、0 GC；54,000 Tick 乘风热路径 0 B |

性能短测维持 1,500 敌人、3,000 投射物、5,000 拾取物和 200 VFX 请求；模拟 Checksum
`ae501d8d2f09f448`、渲染 Checksum `246842ccba5b197a`，均和 G1.1 相同。Baked Catalog Content Hash
为 `bfbf469d80a6a7fc2b389e7e8a973c2232dde781392a7486ddc0124eb3578b8d`。

## 6. 构建产物

- 配置：`NOT RUN`
- 路径：无
- 文件 Hash：无
- Build Manifest：无

G1.2 没有生成 Player。可再生测试与性能证据位于 `TestResults/QinglanDemo/G1.2` 并由仓库忽略；
纳入提交的 Baked JSON SHA-256 为 `14183194EACB7834BF760F3BB05399A85BE9CBB00142340EB43942ABF91B232A`。

## 7. 未执行项目

- Windows x64 Development/Release Build：`NOT RUN`，路线规定 G1.7 在完整 Placeholder Pack 后执行。
- G2.6 的 WASD/摇杆乘风、HUD 档位和音效一致性专项 PlayMode：`NOT RUN`；现有 9 项 PlayMode 只证明
  既有场景/生命周期回归，不关闭 R-002 的表现与输入部分。
- Boss 控制递减、正式资产、目标硬件 GPU 与完整局：`NOT RUN`，分别属于 G2.2、G3 和 G2.7/G2.8。

## 8. 已知限制和风险

- Character 尚无 Starting Skill；G1.3 必须在同一 Pack 新增游风剑后回填并重新 Bake。
- 三档 Trait 已冻结为通用输出数据，但其对本命/亲和 Delivery 的消费由 G1.3 验证。
- `rooted` 当前对普通目标 Override MoveSpeed=0；Boss 控制转换必须在 G2.2 通过通用 Boss 规则实现。
- 正式资产、FirstParty provenance 和字体许可证仍由 QD-KI-003/007/008 阻断 Release。

## 9. 未完成项

- G1.3—G1.7 的武器、心诀、敌人、灵物奇物、地图/Boss 和完整 Placeholder Pack。
- G2.1—G2.8 的可玩垂直切片、表现/输入与完整局外事务。
- G3.1—G3.6 的正式生产、Release、目标硬件与商业验收。

## 10. 下一步前置条件

- G1.3 只实现 M04 六把武器、等级成长、辅助技能、Preview/ProcDepth/Cleanup，并回填 Starting Skill。
- 必须复用 G1.1/G1.2 通用 Skill Delivery、状态操作数和乘风输出标签；不得加入陆青野 ID 分支。
- 若六武器存在当前模块无法表达的新机制，先提交 Change Request，再修改 Schema 或冻结 API。

## 11. 结论

`COMPLETE`。G1.2 的强制编译、Focused/全量 EditMode、全量 PlayMode 回归、Project Validation、API Freeze
和性能门禁均有实际 PASS 证据；Build 与 G2.6 专项 PlayMode 保持 `NOT RUN`，未被误报为通过。
