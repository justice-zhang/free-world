# Codex 结果报告：Qinglan Demo G1.3 六武器与技能运行时

- 任务：实现 M04 六把武器、等级成长、隐藏辅助技能、Preview/ProcDepth/Cleanup 与 Starting Skill
- 里程碑：Qinglan Demo G1.3
- 分支：`codex/qinglan-demo-implementation`
- Git Commit：本报告所在提交
- 日期：2026-08-05

## 1. 实现范围

完成 `qinglan.pack.demo` 0.2.0 的武器切片：六把 8 级主武器、十个隐藏辅助技能、陆青野 Starting Skill、
中英本地化、Schema 6 Baked Catalog，以及回返、标记引爆、潮汐交替、OnKill 灵藤区域的真实执行闭环。
同时扩展通用 Secondary/OutboundReturn 运行时、事件型 Preview 和对应 EditMode Golden。

本包不创建六心诀、六显化、Offer、敌人、Boss、Encounter 或正式资源。飞轮回爆、震岳护域/反震、
藤丛生长/传播已定义为隐藏技能，但只允许 G1.4 Evolution 组合，基础武器不会提前取得显化行为。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Assets/Game/Content/Runtime/ContentValidator.cs` | Secondary 双引用、回返辅助技能与机制输出引用类型验证 |
| `Assets/Game/Simulation/SkillRuntime*.cs`、`SkillSystems.cs` | 原目标传播、确定性交替、等级闭包、回收 Gate、命中额度与回收清理 |
| `Assets/Game/Simulation/QinglanRuntime.cs` | 按稳定输出 ID 查询当前机制输出的内部消费者 |
| `Assets/Game/Simulation/SkillPreviewHarness.cs` | 非 Timer 技能的通用合成触发上下文和零分配 Preview |
| `Assets/Game/Editor/QinglanG13ContentSetup.cs` | 可重复生成 16 个技能、双语文本、Pack 0.2.0 与 Baked Catalog |
| `Assets/GameAssets/Placeholder/QinglanDemo/*` | 六主武器、十隐藏技能、Character/Pack/Baked JSON |
| `Assets/GameAssets/Localization/UI*.asset` | 32 个新增英文与简体中文名称/描述 Key |
| `Assets/Tests/EditMode/QinglanG13WeaponSkillTests.cs` | 内容图、回返 Gate、原子引爆、相位、生命周期、Golden 与 0 B |
| `Assets/Tests/EditMode/QinglanG12CharacterCombatTests.cs` | 允许后续切片扩展同一 Pack，并验证 Starting Skill 仍是有效可执行 Skill |
| `Docs/DemoDevelopment/*`、`Docs/EXECUTION_LOG.md` | G1.3 锁定值、需求状态、证据与 G1.4 边界 |

## 3. 关键架构决定

- Simulation 不比较任何 `qinglan.*` ID；回返 Gate 读取 Delivery 声明的机制输出 ID，并通过通用机制查询。
- `SpawnSecondarySkill` 保留原命中目标；两个引用以实例 `ActivationSequence` 交替，不依赖动画事件或时间浮点。
- 主技能创建时预注册隐藏依赖闭包，升级向下传播且深度上限 16；ProcDepth 仍由 CombatRules 独立约束。
- OutboundReturn 的回程命中额度耗尽后只禁用继续命中，仍返回 Owner 后统一触发/清理，避免回收事件丢失。
- Placeholder Presentation ID 使用 `placeholder.*` 命名并依赖程序化 fallback；不伪造尚不存在的 Addressable 正式资源。
- 所有新增运行时成员均为内部实现；五个冻结公共程序集签名与 Hash 不变，无需新 ADR。

## 4. 实际执行的命令

```text
Unity.exe -batchmode -nographics -projectPath E:/ai/free-world -executeMethod Game.Editor.QinglanG13ContentSetup.RunFromCommandLine
Unity.exe -batchmode -nographics -projectPath E:/ai/free-world -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.QinglanG13WeaponSkillTests
Unity.exe -batchmode -nographics -projectPath E:/ai/free-world -runTests -testPlatform EditMode -testResults TestResults/QinglanDemo/G1.3/editmode-final.xml
Unity.exe -batchmode -nographics -projectPath E:/ai/free-world -runTests -testPlatform PlayMode -testResults TestResults/QinglanDemo/G1.3/playmode.xml
Unity.exe -batchmode -nographics -projectPath E:/ai/free-world -executeMethod Game.Editor.ProjectValidationCommand.Run
Unity.exe -batchmode -nographics -projectPath E:/ai/free-world -executeMethod Game.Editor.M10ApiFreezeCommand.Run
M10_TICK_COUNT=900 M10_WARMUP_TICKS=120 Unity.exe -batchmode -nographics -executeMethod Game.Editor.M10PerformanceCommand.Run
dotnet build free-world.slnx --nologo
rg（Simulation 内容 ID、Core UnityEngine、变更代码禁用查找 API）
PowerShell（.meta GUID 唯一性、NUnit XML、JSON、SHA-256 检查）
git diff --cached --check（手写文件子集；Unity 生成 YAML 按 M0-KI-007 排除）
```

最初两次在受限沙箱内启动 Unity 时，许可客户端返回 `com.unity.editor.headless` 无授权且没有生成 NUnit
XML；改为使用本机已激活许可在沙箱外执行后，所有下列证据实际生成。许可启动失败没有被计为测试通过。
完整 cached diff 只报告 Unity 自动序列化空字段的尾随空格；手写 C#、Markdown 和 JSON 子集为零问题，
按已接受的 M0-KI-007 保留生成格式，不手工批量重写 `.asset`/`.meta`。

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| 编译 | PASS | `dotnet build free-world.slnx`：0 error、27 个既有序列化 DTO CS0649 warning；Unity 编译成功 |
| G1.3 Focused EditMode | PASS | `editmode-focused-final.xml`，7/7；18 个 Preview Golden 全部精确匹配 |
| 全量 EditMode | PASS | `editmode-final.xml`，216/216 |
| 全量 PlayMode 回归 | PASS | `playmode.xml`，9/9 |
| 内容验证 | PASS | `validation.log` 含 `[Project Validation] PASS`；28 定义与 Placeholder Presentation 规则通过 |
| API Freeze | PASS | Core 168、Content 918、Simulation 1160、Application 346、Platform 73；Hash 与 G1.2 相同 |
| 构建 | NOT RUN | G1.3 路线不要求 Build；完整 Placeholder Pack 在 G1.7 构建 |
| 性能/Soak | PASS | 900 Tick p99 11.4037 ms、0 B、0 GC、无增长、确定性 PASS；18 个 Preview 固定 Tick 均 0 B |

性能短测维持 1,500 敌人、3,000 投射物、5,000 拾取物和 200 VFX 请求；模拟 Checksum
`5b38929fd7e3a644`、渲染 Checksum `c8546daede9c256e`。Baked Catalog Content Hash 为
`139c9c504f9a5a2625b4b6e669b9642fb8cb961c60e32df39db5aa9590de31f8`。

## 6. 构建产物

- 配置：`NOT RUN`
- 路径：无
- 文件 Hash：无
- Build Manifest：无

G1.3 没有生成 Player。可再生测试/性能证据位于 `TestResults/QinglanDemo/G1.3` 并由仓库忽略；纳入
提交的 Baked JSON SHA-256 为 `81A54569064B17C9ABDA00A0EF4A38136DC2A0FBC5A7F54F630DDB3068AAD82F`。

## 7. 未执行项目

- Windows x64 Development/Release Build：`NOT RUN`，路线规定 G1.7 在完整 Placeholder Pack 后执行。
- 六显化前后 Preview 差异和三构筑矩阵：`NOT RUN`，G1.4 尚未创建 Evolution/Passive/Offer 内容。
- 正式资产、目标硬件 GPU、完整局和数值平衡：`NOT RUN`，分别属于 G3.1—G3.4 与 G2.7/G2.8。

## 8. 已知限制和风险

- Preview 使用静止合成目标和声明匹配事件，只是设计回归，不等于实际敌群、正式平衡或 GPU 指标。
- 四类显化辅助技能已可执行但未装配；G1.4 必须通过 Evolution/Reward 数据组合，不能写武器 ID 分支。
- Placeholder Presentation 只允许 Development fallback；正式资源仍被 QD-KI-003/007/008 阻断 Release。

## 9. 未完成项

- G1.4—G1.7 的心诀显化、敌人、灵物奇物、地图/Boss 和完整 Placeholder Pack。
- G2.1—G2.8 的可玩垂直切片、表现/输入与完整局外事务。
- G3.1—G3.6 的正式生产、Release、目标硬件与商业验收。

## 10. 下一步前置条件

- G1.4 只实现 M05 六心诀、六显化、Offer/资格/候选和三条构筑矩阵。
- 复用本包隐藏技能、标签、状态和等级传播；显化行为只能由内容引用/组合表达。
- 若现有 Evolution/Offer 模块不能表达新机制，必须先提交 Change Request，不能在具体武器中硬编码。

## 11. 结论

`COMPLETE`。G1.3 的强制编译、Focused/全量 EditMode、全量 PlayMode 回归、Project Validation、API Freeze
和性能门禁均有实际 PASS 证据；Build 与 G1.4 显化差异保持 `NOT RUN`，未被误报为通过。
