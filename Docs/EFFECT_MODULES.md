# 技能与效果模块规范

## 1. 设计目的

绝大多数新技能应通过已有模块组合完成。只有无法表达的新机制才增加 C# 模块。

## 2. 烘焙操作码

> public struct EffectOp  
> {  
> public EffectOpCode Code;  
> public float Value0;  
> public float Value1;  
> public float Value2;  
> public int Int0;  
> public int Int1;  
> public RuntimeContentIndex Reference0;  
> public RuntimeContentIndex Reference1;  
> public EffectOpFlags Flags;  
> }

作者数据在构建或内容烘焙时转换为 EffectOp\[\]。高频执行路径不反射 ScriptableObject 类型。

## 3. 注册表

- ITriggerExecutor

- ITargetingExecutor

- IDeliveryExecutor

- IEffectExecutor

- IConditionEvaluator

- IMapModule

注册表必须显式、可测试、无运行时程序集扫描。每种模块拥有稳定模块 ID。

## 4. 初始模块清单

### Trigger

- Timer

- OnHit

- OnKill

- OnDamageTaken

- OnPickup

- OnStatusApplied

### Targeting

- Self

- Nearest

- Random

- Circle

- Cone

- Line

- Ring

- RandomPointAroundPlayer

### Delivery

- Instant

- Projectile

- Area

- Aura

- Orbit

### Effect

- Damage

- Heal

- ApplyStatus

- RemoveStatus

- Knockback

- Pull

- ModifyStat

- SpawnSecondarySkill

- GrantShield

- GainResource

## 5. 调用链保护

每个伤害包和技能触发上下文必须携带 ProcDepth、来源 ContentId 和调用链标记。达到上限后中止二次触发并写入诊断计数，避免无限递归。

## 6. 新模块准入

新增模块前必须回答：

1\. 现有模块组合为何不能表达？

2\. 新模块能否被至少两个未来内容复用？

3\. 是否改变模拟管线顺序？

4\. 是否影响存档格式或内容 Schema？

5\. 是否需要 Burst/Jobs 兼容？

6\. 是否有单元测试和性能测试？

新增模块需附 ADR 或 Change Request。

## 7. M4 Schema 3 数据流

```text
SkillAuthoring
→ Baker（ID、参数、LevelPatch 路径/类型验证）
→ RuntimeSkillDefinition（稳定 ID + EffectOp[]）
→ ContentRegistry（稳定 ID → RuntimeContentIndex）
→ SkillRuntimeCatalog（executor + StatIndex + RuntimeSkillLevel[]）
→ SkillInstance（Owner + Level + Cooldown）
```

Schema 3 Skill 包含 Tags、Cooldown、ResourceCost、Trigger、Condition、Targeting、Delivery、
Effects 和 LevelPatches。固定 Tick 不访问作者对象、不按字符串查找模块、不反射，也不创建
临时查询集合。Registry 构建阶段绑定 RuntimeContentIndex；稳定 ContentId 继续用于 Hash、
验证、错误报告和未来存档。

`SkillModuleRegistry.CreateDefault()` 用直接调用注册五类 executor。重复 ID 立即失败，缺失 ID
由 Baker、ContentValidator 或 Runtime Catalog 构建拒绝。M4 唯一初始 Condition 为
`base.condition.always`。

## 8. Trigger 稳定 ID

TriggerContext 携带事件类型、Source、Subject、Position、Direction、SourceContentId 和
ProcDepth。Timer 由 Skill Instance 冷却驱动；事件 Trigger 消费模拟缓冲。

| 稳定 ID | 行为 |
|---|---|
| `base.trigger.timer` | 冷却与资源允许时周期触发 |
| `base.trigger.on_hit` | Owner 造成实际护盾或生命伤害 |
| `base.trigger.on_kill` | Owner 首次令目标进入死亡流程 |
| `base.trigger.on_damage_taken` | Owner 承受实际伤害 |
| `base.trigger.on_pickup` | 拾取系统提交 OnPickup 上下文；M4 只实现和测试入口 |
| `base.trigger.on_status_applied` | Owner 成功获得状态 |

## 9. Targeting 稳定 ID 与参数

所有 Actor 查询复用 SpatialGrid/结果缓冲并在选择前稳定排序；Random 在稳定候选上使用固定
随机流。`Int0 <= 0` 时 Nearest/Random 默认为 1，其他集合型 Targeting 默认为全部。

| 稳定 ID | Value0 | Value1 | Int0 |
|---|---:|---:|---:|
| `base.targeting.self` | 未使用 | 未使用 | 未使用 |
| `base.targeting.nearest` | 搜索半径 | 未使用 | 最大数量 |
| `base.targeting.random` | 搜索半径 | 未使用 | 最大数量 |
| `base.targeting.circle` | 半径 | 未使用 | 最大数量 |
| `base.targeting.cone` | 长度 | 完整夹角（度） | 最大数量 |
| `base.targeting.line` | 长度 | 半宽 | 最大数量 |
| `base.targeting.ring` | 内半径 | 外半径 | 最大数量 |
| `base.targeting.random_point_around_player` | 最小半径 | 最大半径 | 未使用 |

Self 返回 Owner；RandomPointAroundPlayer 返回无 Actor 的位置目标。方向优先取 TriggerContext，
没有有效方向时使用 Owner 朝向。

## 10. Delivery 稳定 ID 与参数

| 稳定 ID | Value0 | Value1 | Value2 | Value3 | Int0 |
|---|---:|---:|---:|---:|---:|
| `base.delivery.instant` | 未使用 | 未使用 | 未使用 | 未使用 | 未使用 |
| `base.delivery.projectile` | 速度 | 碰撞半径 | 生命周期秒 | 未使用 | 可命中次数，最小 1 |
| `base.delivery.area` | 半径 | 生命周期秒 | Tick 间隔秒 | 未使用 | 未使用 |
| `base.delivery.aura` | 半径 | 生命周期秒 | Tick 间隔秒 | 未使用 | 未使用 |
| `base.delivery.orbit` | 轨道半径 | 碰撞半径 | 生命周期秒 | 弧度/秒 | 命中间隔 Tick，最小 1 |

所有非 Instant Delivery 必须配置稳定 PresentationId。该 ID 是模拟到表现的契约，M4 不加载
Prefab/VFX。结构创建在 Cleanup 应用；Area 锚定目标点，Aura/Orbit 跟随 Owner；Projectile
使用移动线段扫掠碰撞。

## 11. Effect 稳定 ID 与参数

| 稳定 ID | 参数契约 |
|---|---|
| `base.effect.damage` | V0 基础伤害；V1 ProcCoefficient `[0,1]`；V2 击退；I0 DamageType；I1 DamageTags；Flags 可含 CanCritical |
| `base.effect.heal` | V0 治疗量 |
| `base.effect.apply_status` | Ref0 为 Status ContentId/Index；V0 Strength |
| `base.effect.remove_status` | Tag0 为驱散标签 |
| `base.effect.knockback` | V0 沿来源到目标方向增加的速度 |
| `base.effect.pull` | V0 沿反方向增加的速度 |
| `base.effect.modify_stat` | Stat0；V0 数值；V1 持续秒，`<=0` 为无限；I0 ModifierOperation；I1 Priority |
| `base.effect.spawn_secondary_skill` | Ref0 为可执行 Skill ContentId/Index；ProcDepth + 1；引用链递归预注册并按 ContentId 去重防环 |
| `base.effect.grant_shield` | V0 同时增加当前与最大护盾 |
| `base.effect.gain_resource` | V0 增加技能资源 |

Effect executor 只写 `SkillExecutionCommand`。`SkillEffectResolutionSystem` 统一解析：Damage、
ApplyStatus 和 RemoveStatus 必须进入 M3 请求缓冲；Heal 只调用 ActorStore 的受控内部治疗
边界；Skill Instance、executor 和 resolver 均不直接写 Health 字段。

## 12. LevelPatch Schema

作者边界只允许下列显式路径，烘焙后只保留 enum target、下标和数值类型：

- Float：`cooldown`、`resource_cost`、`trigger.value0`、`trigger.value1`、
  `targeting.value0`、`targeting.value1`、`delivery.value0..value3`、
  `effects[n].value0..value2`
- Integer：`trigger.int0`、`targeting.int0`、`delivery.int0`、
  `effects[n].int0`、`effects[n].int1`
- Operation：`add`、`multiply`、`override`

等级从 2 开始连续；同等级按作者顺序应用，后续等级继承前级结果。路径、Effect 下标、类型、
有限值、32 位整数算术、Effect 参数契约和非负 Cooldown/ResourceCost 均按等级累积结果在进入
运行前验证。运行时不保存路径字符串。

## 13. M4 测试内容

| Skill ContentId | 组合 | PresentationId |
|---|---|---|
| `test.skill.single_projectile` | Timer + Nearest + Projectile + Damage | `placeholder.presentation.single_projectile` |
| `test.skill.orbit` | Timer + Self + Orbit + Damage | `placeholder.presentation.orbit` |
| `test.skill.ground_area` | Timer + RandomPointAroundPlayer + Area + Damage | `placeholder.presentation.ground_area` |
| `test.skill.damage_aura` | Timer + Self + Aura + Damage | `placeholder.presentation.damage_aura` |

这些 Fixture 位于 `Assets/GameAssets/Placeholder/TestSkillContent`，不带 `release` 标签，不含
正式或第三方资产，也没有专用控制器。

## 14. Qinglan Demo Schema 6 模块追加

ADR 0013/0014 接受以下通用模块；G1.1 实现前仍不可在内容中引用：

| 类别 | 稳定 ID | 参数契约 |
|---|---|---|
| Condition | `base.condition.status_count_at_least` | Ref0 Status 或 Tag0；I0 最小层数；I1 目标域 |
| Condition | `base.condition.target_has_status` | Ref0 Status 或 Tag0；I0 目标域 |
| Targeting | `base.targeting.trigger_position` | V0 可选半径；I0 最大 Actor 数；0 可为纯位置 |
| Delivery | `base.delivery.outbound_return` | V0/V1 出发/回返速度；V2 半径；V3 距离；I0 每相位命中数 |
| Effect | `base.effect.consume_status` | Ref0 Status 或 Tag0；I0 消费层数；I1 不足策略 |
| Effect | `base.effect.detonate_status` | Ref0 Status 或 Tag0；V0 每层系数；I0 最大层数 |

Schema 6 `SkillModuleDefinition` 作者/磁盘边界追加 ReferenceId0/1、Tag0/1，Runtime Catalog 构造时
绑定为紧凑操作数。Consume/Detonate 必须先完整验证再原子消费；Detonate 只按实际消费层数排一次
Damage 请求。OutboundReturn 固定 Outbound→Turn→Return，相位内去重，Owner 失效只排 Cleanup。

完整字段、Pipeline 和测试矩阵见 `DemoDevelopment/08_G0_3_CONTRACT_FREEZE.md`。以后增加模块仍需
新的 Change Request；不得按具体技能、状态或角色 ContentId 分支。
