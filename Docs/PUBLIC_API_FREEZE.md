# 公共 API 冻结基线

M10 首次冻结、G1.1 按 ADR 0013—0015 更新以下程序集的公开类型与 public 成员 API。
`Game.Editor.CoreApiFreezeValidator` 使用编译后反射
输出规范化类型/成员签名并计算 SHA-256；Project Validation 会在签名数量或 Hash 漂移时失败。

| 程序集 | 签名数 | SHA-256 |
|---|---:|---|
| `Game.Core` | 168 | `25766747b7014e0386506567e5e3c35f78b6dc5d00d850b00c35d28eb8d7e176` |
| `Game.Content.Runtime` | 940 | `cd72d779cf1ae53f0875d06140706e194081588b7a0429efd4e490ae72e35b00` |
| `Game.Simulation` | 1406 | `b901d06158d38b41b9d0024f9ac73112503c64cbbd22eb9f473fec1121d0ab82` |
| `Game.Application` | 590 | `a595031235a1fc890d30311afc572aaefe9401a16055dfa249c6cdd0427293bc` |
| `Game.Platform.Abstractions` | 73 | `8eb5f2ccca0f5845a55d90c9f00fb42eae59cc82d81e98369995e84428a51738` |

## 变更协议

1. 先提交并接受 ADR；说明旧 API 的消费者、二进制/源码兼容性、Content/Save Schema 影响、迁移
   步骤、回滚方案和测试。
2. 优先增加兼容 API 或提供明确弃用周期；不得只为使验证通过而无说明地替换 Hash。
3. 修改实现和测试后运行完整 EditMode、PlayMode、Project Validation、性能基准和受影响构建。
4. 只有审查接受 API 变化后，才可更新 `CoreApiFreezeValidator` 与本文件中的签名数和 Hash。

内部类型和成员不进入冻结 Hash，但仍受程序集方向、内容扩展和存档稳定 ID 规则约束。规范化
输出不依赖元数据 Token 或编译顺序，因此相同源码应得到相同基线。

## Qinglan Demo G0.3 已批准变更窗口

ADR 0013—0015 已批准 G1.1 在不改变依赖方向的前提下追加下列公共契约。G1.1 更新前的 M10
基线为 Core 147、Content 663、Simulation 1002、Application 305、Platform 73；规范化逐行差异保存在
`TestResults/QinglanDemo/G1.1/api-signature-diff.txt`。审计结果无旧构造函数或成员删除；唯一规范旧行
变化是 ADR 0015 明确规定的 `SaveSchema.CurrentVersion` 常量值 2→3，该成员仍保留并进入弃用周期。

| Assembly | 批准追加范围 | 明确禁止 |
|---|---|---|
| `Game.Core` | 4 个 BuiltInStatId；DamageChannelId/内建通道；必要的稳定事务值类型 | 删除/重排旧 Stat、具体 Qinglan ID |
| `Game.Content.Runtime` | Schema 6 常量、14 类定义、模块引用操作数、RewardOp/纯值枚举 | 改写 Schema 1—5 构造/Hash |
| `Game.Simulation` | 24 项 Demo Pipeline、Mechanic/Reward/Map/Boss/Affix Runtime、Movement/Damage 纯值与快照 | UnityEngine、Scene、存档/平台写入 |
| `Game.Application` | Profile 3、按 kind Save 版本、Meta/RewardChoice/RunResult/Commit 契约 | RuntimeIndex/EntityHandle 持久化 |
| `Game.Platform.Abstractions` | 无计划变化 | 为 Demo 绕过 Application 直接调平台 |

G1.1 Freeze 更新门禁：

1. 在旧 Hash 下运行 Project Validation，预期只因批准签名漂移而 FAIL，并保存规范签名 diff；
2. 证明 diff 仅含上表追加项，旧 public 类型/成员签名仍存在；
3. 运行完整 EditMode、PlayMode、内容验证、性能短测和 Development Build；
4. 再更新 Validator 签名数/Hash 与本文件，并重跑全部门禁至 PASS；
5. 报告同时保留“旧 Hash 预期 FAIL”和“新 Hash 最终 PASS”，不得删除前者。

若实现需要删除、改名、重排或改变旧成员语义，G0.3 授权不足，必须新提交 CR/ADR。

## Qinglan Demo G1.5 批准追加

ADR 0016 为 CR-2026-011 的执行闭环批准 `Game.Content.Runtime` 追加 5 条规范签名，其他冻结
程序集保持完全不变：

```text
C RuntimeEliteAffixDefinition(..., Int32 maximumGeneration, Single rewardMultiplier, ContentId presentation)
F RewardOperationCode.SpawnEnemy = 11
F SkillModuleIds.TargetingAlliesCircle
P RuntimeEliteAffixDefinition.MaximumGeneration
P RuntimeEliteAffixDefinition.RewardMultiplier
```

旧 `RuntimeEliteAffixDefinition` 构造函数仍存在并使用 0 代/1 倍默认值，没有签名删除或替换。旧 Hash
下 Project Validation 仅报告 `Game.Content.Runtime` 从 918/`ca5937…` 漂移到
923/`ebef43…`；Core、Simulation、Application 与 Platform 的签名数和 Hash 不变。规范签名输出与
旧 Hash 失败日志保存在 `TestResults/QinglanDemo/G1.5/`，更新后必须由完整门禁重新证明 PASS。

## Qinglan Demo G1.6 批准追加

CR-2026-016 与 ADR 0017 为 M09 的固定时点一次性精英补齐 Schema 6 `EliteRules[]`。规范 API 从
923 条追加 17 条至 940 条：

```text
T/C/F EncounterEliteRuleDto（5 个字段）
T/C RuntimeEncounterEliteRule
C RuntimeEncounterPhase(..., RuntimeEncounterEliteRule[] eliteRules, ...)
P RuntimeEncounterEliteRule.EnemyId / SpawnTimeSeconds / Pattern / AnchorId / AffixPoolIds
P RuntimeEncounterPhase.EliteRules
F EncounterPhaseDto.elites
```

旧 `RuntimeEncounterPhase` 构造函数仍存在并默认空规则；Schema 1—5 不读取新 DTO 字段，已有 M5
Fixture Hash 不变。旧 Hash 下 Project Validation 只报告 `Game.Content.Runtime` 从
923/`ebef43…` 漂移到 940/`cd72d7…`；Core、Simulation、Application 与 Platform 签名文件逐字节不变。
规范签名、旧 Hash 失败日志与最终捕获保存在 `TestResults/QinglanDemo/G1.6/`。

## Qinglan Demo G1.7 批准追加

CR-2026-007/008 与 ADR 0018 完成 G1.1 Reward 骨架的受控 Evolution 选择。规范 diff 为
Simulation 32 条、Application 9 条追加，零删除；Core、Content Runtime、Platform 逐字节不变。

Simulation 追加 `RewardChoiceRuntime`、`RewardChoiceSnapshot`、Request/Resolution 状态枚举、
请求/选择方法、只读事务/候选/fallback/随机诊断，以及 `ProgressionRuntime.RewardChoices`。Application
追加带 RunId/Sequence 的 RewardChoice 构造/属性、`GameState.RewardChoice`、状态机入口、
`RunSession.CurrentRewardChoice/SelectReward/SelectRewardAt`。

旧 Hash 下 Project Validation 同时报告 Simulation `a65553…`→`57e294…` 与 Application
`bea7fe…`→`f57fe0…`；没有旧构造、属性、方法或枚举数值删除/替换。规范签名和失败日志保存在
`TestResults/QinglanDemo/G1.7/`。

## Qinglan Demo G2.1 批准追加

ADR 0019 为 M08 旧演武场运行时补齐 `MapObjectiveRuntime` 的公开命令、只读快照、结果状态、
地标状态与输出事务投影。规范 diff 为 `Game.Simulation` 81 条追加、零删除；Core、Content Runtime、
Application 与 Platform 签名逐字节不变。

旧 `MapObjectiveRuntime(int capacity)`、`TryAdd`、`TryTransition` 与 `TryGetState` 均保留；新四容量构造
函数只扩展固定容量配置。新增 API 不暴露 Unity Object、Scene 或运行时索引，目标/事件/地标和输出
只使用稳定 `ContentId`、`SpatialEntity` 与 `RewardTransactionId`。旧 Hash 下 Project Validation 仅报告
Simulation `57e294…`→`fd387b…`；规范签名、81/0 差异和失败日志保存在
`TestResults/QinglanDemo/G2.1/`。

## Qinglan Demo G2.2 批准追加

ADR 0020 接受通用 `BossPhaseRuntime` 的公开快照、阶段转换、三规则修正、Boss-owned Effect 生命周期
和容量构造。规范 diff 为 `Game.Simulation` 58 条追加、零删除；Core、Content Runtime、Application 与
Platform 签名逐字节不变。

公开无参 `BossPhaseRuntime()` 被显式保留，既有二进制构造签名未删除；新增值只使用稳定
`ContentId`、`EntityHandle`、`RewardTransactionId` 和纯值状态，不暴露 Unity Object 或运行时索引。
旧 Hash 下 Project Validation 仅报告 Simulation `fd387b…`→`e41c43…`；规范签名、58/0 差异、旧 Hash
失败日志和最终捕获保存在 `TestResults/QinglanDemo/G2.2/`。

## Qinglan Demo G2.3 批准追加

ADR 0021 接受通用 `RewardRuntime` 的 Reward/Pickup/Relic 消费、三槽库存、Relic Choice、局内永久结果
投影和只读诊断。规范 diff 为 `Game.Simulation` 65 条追加、零删除；Core、Content Runtime、Application
与 Platform 签名逐字节不变。

既有 `RewardRuntime(int transactionCapacity = 128)` 规范签名和行为保留；Demo 组合根通过内部容量构造
使用 4096 个整局事务和 512 个执行结构。新增值只使用稳定 `ContentId`、`RewardTransactionId`、
`EntityHandle` 和纯值快照，不暴露 Unity Object、Scene 或持久化 RuntimeIndex。旧 Hash 下 Project
Validation 按预期只报告 Simulation `e41c43…`→`4d5bfc…`；规范签名、65/0 对比、失败日志和最终捕获
保存在 `TestResults/QinglanDemo/G2.3/`。

## Qinglan Demo G2.4 批准追加

ADR 0022 接受不可变 Run Descriptor/Result、四种 Outcome、Demo Flow Coordinator/Factory 边界和局内统计
投影。规范 diff 为 `Game.Simulation` 6 条、`Game.Application` 95 条追加，均删除 0；Core、Content
Runtime 与 Platform Abstractions 逐字节不变。

旧 `RunSession(world, player, stateMachine, clock)`、`RunResult` 原公开属性、`RunEndReason` 数值 1—3 和
旧 `GameState` 全部保留。新增结果只暴露稳定 ContentId、Pack Version/Hash、只读集合和纯值 Checksum，
不暴露 Unity Object、Scene 或 RuntimeIndex。旧 Hash 下 Project Validation 按预期只报告 Simulation
`4d5bfc…`→`533fa9…` 与 Application `f57fe0…`→`e423cd…`；规范签名和失败日志保存在
`TestResults/QinglanDemo/G2.4/`。

## Qinglan Demo G2.5 批准追加

ADR 0023 接受 Profile 3 单一 Owner、Meta 购买/装配/设施投影、Recovery 拒绝、结果提交门禁和不可变
Run Meta/唯一奖励快照。规范 diff 为 `Game.Simulation` 1 条、`Game.Application` 73 条追加，删除均为 0；
Core、Content Runtime 与 Platform Abstractions 签名文件逐字节不变。

Simulation 唯一追加为 `BuildState.GrantMetaOutput(ContentId)`，只在开局装配低频应用已验证的 Trait、
UpgradeOffer 或 Synergy。Application 保留既有 DemoRunCoordinator、RunDescriptor、MetaLoadout 构造，追加
兼容重载、Profile Coordinator、Meta/Facility/Recovery 结果纯值和 `RunResultCommitted` 事件。公开类型不
暴露 Unity Object、Scene、RuntimeContentIndex 或存储实现。

旧 Hash 下 Project Validation 按预期只报告 Simulation `533fa9…`→`6966b5…` 与 Application
`e423cd…`→`743d38…`；规范签名、74/0 对比和失败日志保存在 `TestResults/QinglanDemo/G2.5/`。

## Qinglan Demo G2.6 批准变更

CR-2026-017 与 ADR 0024 接受通用 UI-safe Run 投影、交互 held 命令和 Settings Save Schema 3。
`Game.Application` 从 523 条变为 589 条：新增 67 条，唯一移除规范行是
`SaveSchema.SettingsCurrentVersion` 常量值从 2 替换为 3；旧 Settings 构造函数、字段和迁移入口均保留。

新增 API 只暴露稳定 ID 字符串、数值、枚举和固定容量可复用缓冲，不暴露 Unity Object、Scene、
Simulation Store、EntityHandle 或 RuntimeContentIndex。Core、Content Runtime、Simulation 与 Platform
Abstractions 的规范签名逐字节不变。旧 Hash 下 Project Validation 按预期仅报告 Application
`743d38…`→`279c5f…`；完整规范签名、68 行对比和旧 Hash 失败日志保存在
`TestResults/QinglanDemo/G2.6/`。

## Qinglan Demo G2.7 批准变更

CR-2026-018 与 ADR 0025 接受表现层读取已有稳定 Presentation/Profile/Affix 身份的最小只读桥接。
`Game.Simulation` 从 1403 条变为 1406 条，仅新增 `SkillRuntime.TryGetDeliveryPresentationId`、
`EnemyRuntime.TryGetAffixId` 和 `EnemyRuntime.GetAffixCount`；`Game.Application` 从 589 条变为 590 条，
仅新增 `RunSession.TryGetVisualOverlayId`。新增 4 条、删除 0。

查询不返回 Store、ActiveDeliveryRecord、Affix 可变对象、Unity Object 或运行时索引；Content Schema、
Save Schema、Assembly 方向和 30 Hz Tick 均不变。Core、Content Runtime、Platform Abstractions 规范签名
逐字节不变。旧 Hash 验证按预期只报告 Simulation `6966b5…`→`b901d0…` 与 Application
`279c5f…`→`a59503…`；规范签名和差异证据保存在 `TestResults/QinglanDemo/G2.7/`。
