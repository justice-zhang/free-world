# 08 G0.3 Schema、API、Save 与测试契约冻结

- 状态：`APPROVED`
- 日期：2026-08-04
- 适用：G1.1—G3.6
- 前置：G0.2 `CR-2026-004`—`CR-2026-015`
- ADR：0013、0014、0015、0016
- 实现状态：契约已批准，代码尚未实施

## 1. 决策追踪

| Formal CR | 契约 Owner | ADR | 首次实现包 | 退出证据 |
|---|---|---|---|---|
| CR-2026-004 | CharacterMechanicRuntime | 0013/0014 | G1.1 骨架、G1.2 内容 | Schema/位移/伤害/54,000 Tick |
| CR-2026-005 | Skill Delivery | 0013/0014 | G1.1 模块、G1.3 内容 | 往返相位、去重、Cleanup |
| CR-2026-006 | Status/Skill Runtime | 0013/0014 | G1.1 模块、G1.3/1.4 内容 | 查询/消费原子性/TriggerPosition |
| CR-2026-007 | RewardChoiceRuntime | 0013/0014 | G1.1 骨架、G2.3 内容 | 资格、暂停、回退、一次提交 |
| CR-2026-008 | Reward/Pickup/Relic | 0013/0014 | G1.1 骨架、G2.3 内容 | 操作码、随机隔离、幂等 |
| CR-2026-009 | MapObjectiveRuntime | 0013/0014 | G1.1 骨架、G2.1 内容 | 状态机、锚点、事件 Seed |
| CR-2026-010 | BossPhaseRuntime | 0013/0014 | G1.1 骨架、G2.2 内容 | 阶段、8 组合、清理、奖励 |
| CR-2026-011 | EliteAffixRuntime | 0013/0014 | G1.1 骨架、G1.5 内容 | 合法组合、Spawn 绑定、奖励 |
| CR-2026-012 | MetaCoordinator/Profile | 0013/0015 | G1.1 定义、G2.5 存档 | v2→v3、Loadout、首通幂等 |
| CR-2026-013 | StatCatalog | 0013/0014 | G1.1 | 索引稳定、消费者、Freeze |
| CR-2026-014 | DamageResolution | 0013/0014 | G1.1 | 通道、屏障、冷却、事件 |
| CR-2026-015 | SaveCoordinator | 0015 | 当前不实施 Continue | 检测/提示/清理/禁止结算 |

任何后续实现超出本表契约时，先回到 Change Request；不能在内容包里用 Qinglan ID 分支补洞。

## 2. Content Schema 6 冻结

### 2.1 新定义与最小字段

| kind | 必需字段 | 可选输出/引用 | Owner |
|---|---|---|---|
| `character_mechanic` | Id、ResourceId、Gain/Loss、Tiers | Modifier/Skill/Resource 输出、PresentationId | CharacterMechanic |
| `reward` | Id、Operations[]、RepeatPolicy | Choice/Fallback、UniqueKey | Reward |
| `pickup` | Id、RewardId、Radius、Lifetime | EligibilityTags、PresentationId | Pickup |
| `relic` | Id、MaxLevel、Tags、Outputs | Prerequisites、MutexIds、PresentationId | Build/Reward |
| `map_objective` | Id、AnchorIds、StateGraph、Completion | Rule/Reward 输出、PresentationId | MapObjective |
| `map_event` | Id、TriggerWindow、AnchorRule、StateGraph | Objective/Reward/Rule 输出 | MapObjective |
| `landmark` | Id、AnchorId、Discovery/ClaimRule | Repeat/Unique Reward、Story | MapObjective |
| `boss` | Id、EnemyId、Phases[]、RewardId | AcceptedRuleIds、Resistance、Cleanup | BossPhase |
| `elite_affix` | Id、Required/ExcludedTags | Modifier、Skill、DeathReward、Profile | EliteAffix |
| `meta_node` | Id、Branch/Terminal、Cost、Prerequisites | Trait/Rule/Offer 输出 | Meta |
| `meta_insert` | Id、SlotTags、Cost | Trait/Rule/Offer 输出 | Meta |
| `meta_facility` | Id、UnlockCondition | Page/Updated Rule | Meta/Application |
| `story` | Id、Sequence、UnlockCondition | Localized Keys、Unique Rule | Meta/Application |
| `collectible` | Id、TopicId、AcquireRule | Localized Keys、Fallback Reward | Meta/Application |

所有定义继承稳定 Id、本地化 Key、Tags；Presentation/Audio 只保存稳定 ProfileId。Reward/Rule/Skill/
Status/Enemy/Map/Meta 引用必须在完整 Registry 中按允许 kind 解析，不能“存在即可”。

### 2.2 既有定义的 Schema 6 扩展

- Character：追加 `MechanicIds[]`；旧 Schema 默认为空。
- Skill Module：追加 `ReferenceId0/1`、`Tag0/1`；旧 Schema 默认为 invalid/empty。
- Map：追加 Objective/Event/Landmark ID 集合；旧 Schema 默认为空。
- Encounter：Elite Entry 可引用 Affix Pool，Boss Rule 可引用 BossDefinition；旧 `Elite bool` 兼容映射
  为无命名词缀的历史倍率路径，只供旧 Schema 4/5。
- Effect：追加 Consume/Detonate Status token；旧 EffectOp 数值不变。

Schema 1—5 不读取这些字段；Schema 6 新 Pack 必须重新 Bake。Hash 先写既有字段，再按本节固定表序
追加新字段；数组保留作者顺序，要求集合语义的字段在 Baker 中 canonical 排序/去重。

### 2.3 Wire token 与参数

| token | 参数 |
|---|---|
| `base.condition.status_count_at_least` | Ref0 Status 或 Tag0；I0 最小层数；I1 目标域 |
| `base.condition.target_has_status` | Ref0 Status 或 Tag0；I0 目标域 |
| `base.targeting.trigger_position` | V0 可选半径；I0 最大目标数；0 表示纯位置 |
| `base.targeting.allies_circle` | V0 半径；I0 最大目标数；排除 Owner 并稳定选择非敌对 Actor |
| `base.delivery.outbound_return` | V0 出发速度；V1 回返速度；V2 半径；V3 最远距离；I0 每相位命中数 |
| `base.effect.consume_status` | Ref0 Status 或 Tag0；I0 层数；I1 缺少策略 |
| `base.effect.detonate_status` | Ref0 Status 或 Tag0；V0 每层系数；I0 最大层数；先消费后排 Damage |
| Reward `spawn_enemy` | I0 1—2；V0 子体战斗/奖励倍率；Ref0 可选 Enemy；Cleanup 创建 |

回返 Delivery 固定相位 Outbound→Turn→Return；每相位独立去重，Owner 失效时排队 Cleanup。所有引用
在 Runtime Catalog 构造时绑定，Tick 中不解析字符串。

## 3. 公共 API 变更集

下表是 G1.1 允许的最大新增面；可使用 internal 收窄，但不得扩大语义或删除旧成员。

| Assembly | 允许追加的公共契约 |
|---|---|
| `Game.Core` | 四个 BuiltInStatIds；`DamageChannelId`/内建通道；Reward/Transaction 稳定值类型（如跨层需要） |
| `Game.Content.Runtime` | 14 类 RuntimeDefinition、Schema 6 常量/DTO 所需纯值枚举、模块引用操作数、RewardOp |
| `Game.Simulation` | Demo SystemId/Pipeline、Mechanic/Reward/Map/Boss/Affix Runtime、MovementSource、DamageResolved/策略、只读快照/命令 |
| `Game.Application` | RewardChoice/RunResultDelta/MetaLoadout/CommitResult、Profile 3 字段、按 kind Save 版本查询 |
| `Game.Platform.Abstractions` | 不变；只消费 Application 已提交事件 |

API Freeze 更新顺序固定为：实现＋测试 → 输出规范签名 diff → 人工核对仅含本表 → 完整门禁 → 更新
Hash/签名数和 `PUBLIC_API_FREEZE.md`。不得先改 Hash 再补实现。

## 4. Runtime 所有者与 Pipeline

权威顺序是 ADR 0014 的 24 系统 Pipeline。关键同 Tick 语义：

1. Map/Boss 只消费上一完成 Tick 的外部事件和当前命令，不读取 Scene。
2. Movement 分别解析 PlayerCommand 与外部位移；MechanicAccumulate 只读前者。
3. Skill Effect 只排 Combat/Status/Reward 请求；不直接写生命、状态或永久档案。
4. DamageResolution 后，MechanicReaction 按 Tick＋Target 去重实际 Shield/Health 损失。
5. RewardResolution 执行纯 Run-local 操作；永久输出只进入 `RunResultDelta`。
6. Death 只产生一次死亡/奖励请求；LootAndReward 去重后排结构命令。
7. Cleanup 是唯一 Entity/sidecar 创建删除者；Snapshot 最后构建。

选择请求由 EventFlush 暴露，Application 在下次推进前暂停 SimulationClock。取消页面不等于拒绝事务；
必须显式 Select/Skip/Fallback，Evolution 关键奖励不可被普通 Skip 丢失。

## 5. Damage、状态和角色机制契约

### 5.1 Damage 结果

```text
DamageResolved
├─ Source/Target/Tick/SourceContentId/ChannelId
├─ Requested/Mitigated/BarrierAbsorbed/ShieldDamage/HealthDamage
└─ Outcome: Applied / Immune / ChannelCooldown / Invalid / Zero
```

`DamageApplied` 继续只代表 `ShieldDamage + HealthDamage > 0`。完全屏障吸收只发 DamageResolved，不触发
OnHit/OnDamageTaken/乘风降档。冷却键为 Target＋Channel；默认最大 8 个活动通道，超容量按稳定最早
到期项回收并记录诊断，不能分配字典。

### 5.2 Status 原子事务

查询得到 `{matchedInstances, totalStacks}`；消费先构建固定容量计划，全部校验成功后统一减少/移除。
Detonate 以实际消费层数计算一次伤害请求；消费为 0 时不伤害。结构删除仍延迟到 Cleanup。

### 5.3 Character Mechanic

实例只保存 Mechanic RuntimeIndex、currentValue、tier、previousResolvedPosition、lastDamageTick。Tier
阈值严格递增。一次 Tick 无论多少实际伤害只执行一次 Loss；跨多阈值增长按最终值进入最高合法档，
按顺序发档位事件。输出通过 Modifier/Skill/Resource 通用操作应用，不读取 CharacterId。

## 6. Reward、Map、Boss 与 Elite 契约

- Reward 事务键：`RunId/SourceStableId/Sequence`；同键只提交一次。Reward RNG 只用于非唯一候选。
- Pickup 只保存 Reward RuntimeIndex、Eligibility、SourceTransaction、PresentationId；聚灵吸取按标签排除
  Unique/ObjectiveLocked/Choice。
- Objective 状态为 Hidden→Revealed→Available→Activating→Defending→Completed；Interrupted 只回
  Available，非法定义进入 DisabledWithError。Event/Landmark 使用同一通用状态/事务原语。
- Boss 仍是 Actor/Enemy；BossPhase 只附加阶段。跨阈值转换按阶段顺序，致死优先；阶段危险实体通过
  ExpireOnPhaseExit/FinishCurrentTelegraph/Persist 交 Cleanup。
- Elite Affix 只在 Spawn 时从稳定候选排序后用 Encounter 流选择；Required/Excluded tags、最大数量和
  代数在创建前验证，Tick 只消费已绑定结果。

## 7. Profile Save Schema 3

当前版本按文档分离：Settings 2、Profile 3、RunRecovery 2。Profile 3 字段和 ADR 0015 一致。
v2→v3 只保留旧值并初始化新集合为空；绝不从统计猜测首通。写入前 canonical 排序/去重，读取时保留
缺失稳定 ID 并警告；Loadout 无效则应用安全默认但不改写原文件，直到用户确认保存。

结果提交顺序：Freeze → immutable RunResult → validate delta → merge transaction in memory → atomic
Profile save → clear Recovery → publish RunOutcomeCommitted/platform events → page transition。任何保存失败
停在可重试状态，不能提前显示已保存。

## 8. 实施分片与回滚

G1.1 只实现最小通用骨架、Schema/Codec/Validator、Fixture、API Freeze 和短测；不得在同一包批量创建
Qinglan 正式内容。后续内容包只能使用已冻结契约。回滚顺序：停止新内容引用 → 移除未发布 Pack →
恢复 Composition Root → 保留旧读取器/token/索引 → 恢复 Freeze 前验证下游。Profile 3 不允许静默降写。

## 9. G1.1 最低测试矩阵

| 层 | 必须证据 |
|---|---|
| Schema | 1—5 Golden/Hash；6 round-trip；14 kind 正/负引用；新 Stat 原索引不变 |
| Module | 回返相位/去重；状态查询/原子消费；TriggerPosition；未知 token 拒绝 |
| Pipeline | 24 项顺序；旧 M2—M6 顺序不变；Cleanup 单写入者；选择暂停在 Tick 边界 |
| Damage | 5 通道、冷却隔离、免疫、屏障、Shield/Health 事件、0 伤害 |
| Runtime | Mechanic、Reward 幂等、Objective、Boss、Affix 各至少两个通用 Fixture |
| Save | Settings2/Profile3/Recovery2 round-trip；Profile v1→2→3、v2→3、主备份/未知 ID |
| Freeze | 五程序集签名 diff 只含批准追加项，Validation 在旧 Hash 下先证明漂移、批准后新 Hash PASS |
| 性能 | 54,000 Tick 机制 0 B；目标规模短测与 M10 JSON 对比；无持续容量增长 |

## 10. G0.3 退出门禁

- [x] CR-2026-004—015 均有 ADR/契约/工作包/测试映射。
- [x] Schema 1—5、Save 2 和旧 API 的兼容/迁移/回滚明确。
- [x] Pipeline、所有者、事件、随机流、Cleanup 和选择暂停无循环。
- [x] G1.1 允许的公共 API 最大面明确，Hash 只能实现后更新。
- [x] CR-11 延期边界不被 Profile 3 偷渡为完整恢复。

因此 G0.3 允许进入 G0.4；代码实现仍必须从 G1.1 开始并完成真实门禁。
