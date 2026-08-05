# 公共 API 冻结基线

M10 首次冻结、G1.1 按 ADR 0013—0015 更新以下程序集的公开类型与 public 成员 API。
`Game.Editor.CoreApiFreezeValidator` 使用编译后反射
输出规范化类型/成员签名并计算 SHA-256；Project Validation 会在签名数量或 Hash 漂移时失败。

| 程序集 | 签名数 | SHA-256 |
|---|---:|---|
| `Game.Core` | 168 | `25766747b7014e0386506567e5e3c35f78b6dc5d00d850b00c35d28eb8d7e176` |
| `Game.Content.Runtime` | 940 | `cd72d779cf1ae53f0875d06140706e194081588b7a0429efd4e490ae72e35b00` |
| `Game.Simulation` | 1160 | `a6555342a937f674d827f83eea0b0100fe2feeafff92f0e53b58e9fd7b39181f` |
| `Game.Application` | 346 | `bea7fe9998f2ae9f872a505e9f36cee00a9ddfd26e5af8e105916ea4b3d46197` |
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
