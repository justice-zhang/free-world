# Codex 结果报告

- 任务：按 Demo 文档顺序实现十二分钟 Encounter 数据切片
- 里程碑：Qinglan Demo G1.6
- 分支：`codex/qinglan-demo-implementation`
- Git Commit：提交后回填；本报告与实现同一提交
- 日期：2026-08-06

## 1. 实现范围

完成九段 0—720 秒普通敌群时间轴、两个固定时点精英、四词缀池、并发预留、12:00 停止边界、
Timeline Analyzer 和双实例 21,600 Tick 无头验证。通过 CR-2026-016 / ADR 0017 追加通用一次性
EliteRule，并保持旧 Schema/构造/Hash 兼容。

未实现折枝、听风、地图目标、奖励选择、实际出生公平或正式表现：分别属于 G2.2、G2.1、G1.7/G2.3、
G2.6/G2.8 和 G3。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Assets/Game/Content/*` | EliteRule Runtime/DTO/Authoring/Validation/Hash 契约 |
| `Assets/Game/Simulation/SpawnRuntime.cs` | 一次性规则、槽位预留、优先级与停止预算清零 |
| `Assets/Game/Simulation/QinglanEncounterHeadlessHarness.cs` | 12 分钟固定 Seed 生产管线验证 |
| `Assets/Game/Editor/QinglanG16*` | 内容配置与 JSON 证据导出命令 |
| `Assets/GameAssets/Placeholder/QinglanDemo/*` | Encounter、Pack 0.5.0、Baked Catalog、双语表 |
| `Assets/Tests/EditMode/QinglanG16EncounterTests.cs` | 六项 G1.6 专项测试 |
| `Docs/ADR/0017-*`、`Docs/ChangeRequests/CR-2026-016-*` | 决策、兼容、迁移与回滚 |
| `Docs/DemoDevelopment/*`、治理文档 | 实现设计、追踪、测试、性能、风险与执行记录 |

## 3. 关键架构决定

- 固定精英是与 BossRule 平行的一次性规则，不复用概率 Elite 或伪装 Boss。
- 未触发一次性规则先于普通预算排队并预留并发槽；容量暂满只在当前 Phase 重试。
- 只在 EliteRules 非空时追加 Hash，旧 DTO 缺失字段读取为空。
- 最后 Phase 结束即清空预算/冷却；Boss 内容只能由 G2.2 追加。

## 4. 实际执行的命令

```text
dotnet build free-world.slnx --nologo
Unity.exe -batchmode -nographics -projectPath E:\ai\free-world -executeMethod Game.Editor.QinglanG16ContentSetup.RunFromCommandLine
Unity.exe ... -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.QinglanG16EncounterTests ...
Unity.exe ... -runTests -testPlatform EditMode ...
Unity.exe ... -runTests -testPlatform PlayMode ...
Unity.exe ... -executeMethod Game.Editor.QinglanG16HeadlessCommand.Run
Unity.exe ... -executeMethod Game.Editor.M10PerformanceCommand.Run
Unity.exe ... -executeMethod Game.Editor.ProjectValidationCommand.Run
Unity.exe ... -executeMethod Game.Editor.M10ApiFreezeCommand.Run
Get-FileHash Assets/GameAssets/Placeholder/QinglanDemo/QinglanDemoContentPack.baked.json -Algorithm SHA256
```

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| 编译 | PASS | `dotnet build`：0 error，27 个既有 warning |
| Focused EditMode | PASS | `editmode-focused-final.xml`：7/7；首轮 5/6 失败证据保留 |
| 全量 EditMode | PASS | `editmode-final.xml`：235/235；首轮 233/234 失败证据保留 |
| PlayMode | PASS | `playmode-final.xml`：9/9 |
| 内容/项目验证 | PASS | `project-validation-final.log` |
| API Freeze | PASS | Content Runtime 940 / `cd72d779cf1ae53f0875d06140706e194081588b7a0429efd4e490ae72e35b00` |
| 12 分钟 Headless | PASS | 2,582 Spawn、2,571 Death、2 Elite、0 Boss、0 InvalidHandle、Checksum `e86df634f50d29e8` |
| 性能短测 | PASS | `performance-final.json`：Tick p99 4.1759 ms、Render p99 0.7682 ms、0 B、0 GC |
| Windows Development Build | NOT RUN | G1.7 完整 Pack 门禁 |

首轮性能为 `FAIL`：p99 4.2761 ms、0 B，但脚本重编译后的测量窗发生 1/1/1 次 GC。恢复正式配置的
300 Tick 预热后复测通过，未调整阈值。首个全量 EditMode 失败是测试夹具中的具体内容 ID 字面量；改为
通用测试 ID 后通过。没有把任一失败轮次描述为通过。

## 6. 构建产物

- 配置：Baked Placeholder Content Catalog，Pack 0.5.0 / Schema 6 / 94 definitions
- 路径：`Assets/GameAssets/Placeholder/QinglanDemo/QinglanDemoContentPack.baked.json`
- 文件 SHA-256：`9DFB76E4F3ED442160D43A9DD599D4AF439E859D8F06B717BF685D79EA9C167A`
- Content Hash：`798dbb302dda57b9f0158e83010ee89392ffdc291cc629715ba357b691ebd5ad`
- Player Build / Manifest：`NOT RUN`，由 G1.7 执行

## 7. 未执行项目

- 两 Boss 一次：`NOT RUN`，G2.2 尚未创建两个 Boss。
- 实际地图出生公平、压力可读、Boss 过渡：`NOT RUN`，由 G2.6/G2.8 PlayMode 执行。
- Release、GPU、目标硬件 1% Low：`NOT RUN`，属于 G3。

## 8. 已知限制和风险

- 无头 Harness 为持续覆盖预算曲线而周期清敌，Peak 16 不代表设计并发或正式平衡。
- P4 只为中段 Boss 预留低压窗；没有 Boss 时不可据此验收完整时间轴体验。
- Null Device Render 探针不是正式 GPU 证据。

## 9. 未完成项

- G1.7 Reward Choice Context、完整 Pack 门禁和 Development Build。
- G2.2 BossRule 与 G2.6/G2.8 实际地图 PlayMode。

## 10. 下一步前置条件

- 以本提交的 Pack 0.5.0、Schema 6 和 API Freeze 为 G1.7 基线。
- 不在 G1.7 提前实现地图、Boss、拾取、奇物或正式资产。

## 11. 结论

`COMPLETE`。G1.6 当前强制检查全部通过；明确延期项均保持 `NOT RUN`。
