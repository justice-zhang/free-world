# Codex 结果报告：Qinglan Demo G1.4 心诀、候选与显化

- 任务：实现 M05 六心诀、18 Offer、三 Synergy、六 Evolution 资格/转换与固定 Seed 构筑矩阵
- 里程碑：Qinglan Demo G1.4
- 分支：`codex/qinglan-demo-implementation`
- Git Commit：本报告所在提交
- 日期：2026-08-05

## 1. 实现范围

完成 `qinglan.pack.demo` 0.3.0 / Schema 6 的构筑切片：六个 5 级心诀、12 个普通升级 Offer、
6 个锁定显化 Offer、3 条通用 Synergy、6 条武器 L8＋心诀 L1 的 Evolution 资格链、6 个可执行显化
结果技能和 1 个隐藏流影风痕技能。Pack 从 28 个定义增加至 68 个定义，并补齐英文/简体中文名称与描述。

本包不实现显化宝匣 Reward Choice Context、升级卡 UI、敌人、Encounter 或正式资源。六个显化 Offer
保持初始锁定，不会混入普通 Level-up Reroll/Banish/Skip；CR-2026-007 的受控候选、回退和幂等事务
继续由 G1.7 实现。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Assets/Game/Editor/QinglanG14ContentSetup.cs` | 可重复生成心诀、结果技能、Offer、Evolution、Synergy、本地化和 Pack 0.3.0 |
| `Assets/GameAssets/Placeholder/QinglanDemo/*` | 40 个新增 Placeholder 内容定义及 `.meta`；更新 Pack/Baked Catalog |
| `Assets/GameAssets/Localization/UI*.asset` | 六心诀、六结果、六 Evolution、18 Offer、三 Synergy 的双语 Key |
| `Assets/Tests/EditMode/QinglanG14ProgressionTests.cs` | Pack、Modifier、Offer 隔离、Synergy、六转换、Preview Golden 与 0 B |
| `Assets/Tests/EditMode/QinglanG13WeaponSkillTests.cs` | 旧切片断言改为最低版本/数量，允许同一 Pack 后续扩展 |
| `Docs/DemoDevelopment/03_CONTENT_CATALOG_AND_IDS.md` | 冻结六个 Result SkillId 和隐藏风痕 ID |
| `Docs/DemoDevelopment/12_G1_4_PROGRESSION_SLICE.md` | G1.4 明细设计、数值、拓扑、Golden、测试与后续边界 |
| `Docs/DemoDevelopment/Modules/M05_PASSIVES_EVOLUTIONS_OFFERS.md` | 追加 G1.4 实施冻结与有界传播决定 |
| `Docs/DemoDevelopment/06_REQUIREMENTS_TRACEABILITY.md`、`README.md` | 更新 R-003—005 和实施文档索引 |
| `Docs/KNOWN_ISSUES.md` | 登记 QD-KI-009：G1.7 前显化宝匣事务仍未实现 |
| `Docs/EXECUTION_LOG.md` | G1.4 范围、证据与 G1.5 交接 |

## 3. 关键决定

- 心诀准入等级批准为 L1，武器要求 L8。Schema 没有 `PassiveLevelAtLeast`，未使用 Stat 阈值间接冒充
  心诀等级，也未扩展冻结公共 API。
- 每个 Modifier 明确 Stat/Operation/Value/Priority/StackingGroup；心诀每级/属性使用唯一 StackingGroup，
  防止升级互相覆盖。
- 普通池只包含 6 Skill＋6 Passive Offer；6 Evolution Offer 初始锁定，BuildState 仍实时维护 Eligibility，
  供 G1.7 的受控奖励适配器提交。
- 三条 Synergy 只使用 OwnsContent、AddModifier 和 AddEffectOp；Simulation 没有任何 `qinglan.*` 字符串分支。
- 地脉生春枝首版在 Area 每次命中时继续生成传播，6 秒预览产生 15,990 Hits，未通过预算审查；最终改为
  每次击杀固定 1 个主藤丛＋最多 2 个单代邻域，Golden 为 480 Hits。
- 青岚流影剑按 Timer 采样 Owner 位置留下短时风痕。严格“静止不落痕”需要当前不存在的通用移动 Trigger；
  若 G2 手感评审要求该差异，必须先提交通用 CR，不能按具体 Skill ID 硬编码。
- 本包只新增 Editor、测试、内容和文档，五个冻结公共程序集 API Hash 全部不变，无需新增 ADR。

## 4. 实际执行的命令

```text
Unity.exe -batchmode -quit -projectPath E:/ai/free-world -executeMethod Game.Editor.QinglanG14ContentSetup.RunFromCommandLine
Unity.exe -batchmode -projectPath E:/ai/free-world -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.QinglanG14ProgressionTests
Unity.exe -batchmode -projectPath E:/ai/free-world -runTests -testPlatform EditMode -testResults TestResults/QinglanDemo/G1.4/editmode-final.xml
Unity.exe -batchmode -projectPath E:/ai/free-world -runTests -testPlatform PlayMode -testResults TestResults/QinglanDemo/G1.4/playmode.xml
Unity.exe -batchmode -quit -projectPath E:/ai/free-world -executeMethod Game.Editor.ProjectValidationCommand.Run
Unity.exe -batchmode -quit -projectPath E:/ai/free-world -executeMethod Game.Editor.M10ApiFreezeCommand.Run
M10_TICK_COUNT=900 M10_WARMUP_TICKS=120 Unity.exe -batchmode -quit -executeMethod Game.Editor.M10PerformanceCommand.Run
dotnet build free-world.slnx --nologo
rg（Simulation 内容 ID、禁用查找 API）
PowerShell（NUnit XML、Baked JSON、SHA-256、定义分类、GUID 唯一性）
git diff --check（手写文件子集；Unity 生成 YAML 按 M0-KI-007 排除）
```

Unity 命令使用本机已激活许可在沙箱外执行。全量 EditMode 首轮为 220/222：旧 G1.3 测试写死 Pack
0.2.0/28，六个 Evolution 定义缺少语言表正文；修复扩展断言并补齐双语文本、重 Bake 后最终 222/222。
传播 15,990 Hits 的首版和 220/222 的中间结果均未被计为通过。

## 5. 测试和构建结果

| 检查 | 结果 | 证据 |
|---|---|---|
| 编译 | PASS | `dotnet build free-world.slnx`：0 error、27 个既有序列化 DTO CS0649 warning；Unity 编译成功 |
| G1.4 Focused EditMode | PASS | `editmode-focused.xml`，6/6；六显化 Golden、Offer 隔离、六转换与三 Synergy |
| 全量 EditMode | PASS | `editmode-final.xml`，222/222 |
| 全量 PlayMode 回归 | PASS | `playmode.xml`，9/9 |
| 内容验证 | PASS | `validation.log` 含 `[Project Validation] PASS`；68 定义与双语 Key 通过 |
| API Freeze | PASS | Core 168、Content 918、Simulation 1160、Application 346、Platform 73；Hash 与 G1.3 相同 |
| Windows Build | NOT RUN | G1.4 路线不要求 Player；完整 Placeholder Pack 在 G1.7 构建 |
| 性能/确定性短测 | PASS | 900 Tick p99 9.2065 ms、0 B、0 GC、无增长、确定性 PASS；六显化 Preview 0 B |

性能短测维持 1,500 敌人、3,000 投射物、5,000 拾取物和 200 VFX 请求；模拟 Checksum
`5b38929fd7e3a644`、渲染 Checksum `c8546daede9c256e`。Baked Catalog Content Hash 为
`ab26f20b76412404f914168e75689528faaf48040e4265131f73fb1a97fc6e1a`。

## 6. 未执行项目及原因

- Windows x64 Development/Release Build：`NOT RUN`，路线规定 G1.7 在完整 Placeholder Pack 后执行。
- 显化宝匣 Reward Choice、空资格 fallback、一次提交与暂停：`NOT RUN`，CR-2026-007 首次实现包为 G1.7。
- 升级卡 UI 与键鼠/手柄 PlayMode：`NOT RUN`，属于 G2.6。
- 正式平衡、实际移动手感、正式资产和目标硬件 GPU：`NOT RUN`，属于 G2/G3。

## 7. 已知限制和风险

- Evolution 结果从 L1 开始；这是现有 Transform 契约，不代表正式数值平衡已冻结。
- 青岚流影剑 Timer 位置采样在静止时仍会落风痕；当前不扩展 Schema/API，风险已记录在 M05 和 G1.4 设计。
- Preview 使用静止合成目标；只证明内容图、确定性和 CPU 分配，不等于移动敌群或 GPU 预算。
- Synergy 一次激活锁存，Evolution 后不撤销；遵守 M6-KI-003，不假设可逆联动。
- QD-KI-009 在 G1.7 前阻止显化宝匣闭环；QD-KI-003/007/008 继续阻止 Release。

## 8. 下一步前置条件

- G1.5 只实现 M07 六敌人、四精英词缀、攻击技能和行为验证，不修改本包 Offer/Evolution 真值。
- 复用现有稠密 EnemyRuntime、Skill 模块、状态和 Placeholder Presentation；不得逐敌人 MonoBehaviour.Update。
- 新行为若现有 Enemy/Affix 模块无法表达，先提交通用 Change Request，不在具体敌人 ID 上分支。

## 9. 结论

`COMPLETE`。G1.4 的强制编译、Focused/全量 EditMode、全量 PlayMode、Project Validation、API Freeze
和性能门禁均有实际 PASS 证据；Player Build、Reward Choice 与 UI 保持 `NOT RUN`，未被误报为通过。
