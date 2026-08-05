# M04 六把武器与技能运行时

## 1. 通用规则

- 普通武器建议 8 级；等级 2—8 使用连续、显式 `LevelPatch`。
- 每个技能只用稳定模块 ID、数值、标签和表现 ID；不创建技能专用 MonoBehaviour。
- 多阶段行为优先拆成隐藏 Secondary Skill；ProcDepth、来源和命中去重必须保留。
- 非 Instant Delivery 必须有 PresentationId；Development 可 fallback，Release 不允许缺失。
- 预览输出 DPS、命中、触发、范围、实体峰值和截断，不作为正式性能结论。

## 2. 游风剑

```text
Timer → Nearest → ReturningProjectile → Damage
ReturnComplete + RidingWind tier 3 → SpawnSecondarySkill(wind_blade)
```

阶段：放出追踪 → 穿刺 → 最大航程/命中后转向 → 返回 Owner → 回收。出/回程可分别命中一次；同一
阶段不可重复击中同一目标。等级路线：追踪、伤害、间隔、数量、穿透、飞行、回返再命中、综合强化。

现有 Projectile 不支持返回，依赖 CR-02。若 CR 未获批，游风剑不能以单向投射物标记完成。

## 3. 镇邪黄符

```text
Timer → Nearest/Random → Projectile → Damage + ApplyStatus(marked)
Mark threshold / expiry / explicit detonate
→ Circle at target → consume marks → scaled Damage
```

每目标保存层数、来源、到期 Tick；同一来源有最大层数。引爆必须原子读取并消费，不能先消费后因
目标失效丢失。显化后单发转符阵，阵内合法标记同时引爆。依赖 CR-03。

## 4. 离火飞轮

```text
Timer → Nearest → ReturningProjectile → Damage(outbound + return)
Evolution → projectile count + return explosion + bounded chain
```

回返命中与游风剑复用 CR-02；连锁爆裂可用 Secondary Skill＋Circle，但必须限制每次回返触发数和
ProcDepth。等级优先数量、伤害、速度、穿透/回程和冷却，不与游风剑形成同质成长。

## 5. 听潮珠

```text
Timer → Self → Aura/Orbit
Phase A RisingTide: Pull + ApplyStatus
Phase B FallingTide: Knockback + Damage
```

相位由通用周期资源或两个有确定偏移的技能实例驱动，不能依赖动画事件。若现有模块无法保证交替，
纳入 CR-01 的通用相位资源或独立 CR。显化扩大相位差异并在切换时爆发；Boss 只受抗性后的位移。

## 6. 震岳印

```text
Timer → Self/nearest point → Area → Damage + Knockback
SecondarySkill → defensive Area/Aura → Armor/Shield modifier
```

短时安全领域不能等于无条件无敌。反震优先用 OnDamageTaken Secondary Skill，但同一内容同时需要
主动 Timer 时，应由显化组合/附属技能装配，不把两个 Trigger 塞入现有单 Trigger 定义。

## 7. 灵藤种

```text
OnKill → TriggerPosition → Area(seed)
Area ticks → Damage/Poison
Evolution → bounded neighbor propagation
```

现有 Targeting 无 TriggerPosition，依赖通用 ContextPosition Targeting（纳入 CR-03 或独立小 CR）。
藤丛连接只保留数值邻接关系，区域实体有硬上限、最小间距、传播代数和生命周期；达到上限时刷新
最近/最旧合格节点，不持续扩容。

## 8. 等级与标签

| 武器 | 核心标签 | 不应成为主成长 |
|---|---|---|
| 游风剑 | `weapon.sword`, `delivery.projectile`, `mechanic.return`, `mechanic.riding_wind_affinity` | 纯范围铺场 |
| 黄符 | `weapon.talisman`, `mechanic.mark`, `damage.burst` | 无条件高频伤害 |
| 飞轮 | `weapon.mechanism`, `mechanic.return`, `mechanic.chain` | 长时领域 |
| 听潮珠 | `weapon.artifact`, `delivery.aura`, `mechanic.cycle`, `control` | 单体直射 |
| 震岳印 | `weapon.artifact`, `delivery.area`, `control`, `defense` | 远程追踪 |
| 灵藤种 | `weapon.plant`, `delivery.area`, `damage.dot`, `mechanic.growth` | 即时单体爆发 |

## 9. 平衡预算

每把武器定义：单体/群体 DPS 目标、覆盖率、控制占比、实体峰值、每秒 EffectOp、VFX 请求和最坏
ProcDepth。显化提升应主要改变行为并形成闭环，不只是统一乘伤。正式数值在 G3 固定。

## 10. 测试与验收

| 类型 | 必须覆盖 |
|---|---|
| Validation | 等级连续、模块存在、引用类型、显化可达、PresentationId |
| EditMode | 出/回程去重、标记原子消费、潮汐交替、藤丛上限、反震间隔 |
| Preview | 六武器 1/4/8 级固定 Seed Golden；显化前后差异 |
| Integration | 角色、心诀、奇物只通过标签/操作组合 |
| Performance | 高等级实体峰值、0 B 热路径、ProcDepth/丢弃计数 |

退出条件：六武器行为与文案一致，三条目标构筑可形成，任何一把都没有 ContentId 特判或专用 Update。

## 11. G1.3 实施锁定

G1.3 已在 `qinglan.pack.demo` 0.2.0 / Schema 6 中实现六把 8 级主武器和十个隐藏技能。主武器等级
2—8 均至少有一个连续显式 Patch；陆青野 Starting Skill 已回填为游风剑。隐藏技能不进入普通候选，
只通过 load-local 引用和 `SpawnSecondarySkill`/`OutboundReturn` 通用边界执行。

| 武器 | 已锁定基础闭环 | G1.4 组合边界 |
|---|---|---|
| 游风剑 | 出/回程分阶段命中去重；回收到 Owner 后按当前乘风三阶输出触发风刃 | 显化再扩大剑影闭环 |
| 镇邪黄符 | 命中叠 `marked`；隐藏引爆按目标原子读取/消费并按实际层数伤害 | 显化扩展符阵范围/频率 |
| 离火飞轮 | 火焰往返与分阶段命中 | 回程爆裂和有限连锁由显化装配 |
| 听潮珠 | `ActivationSequence` 确定性交替涨潮/退潮；辅助技能继承主技能等级 | 显化扩大相位差异 |
| 震岳印 | 主动范围伤害与击退 | 护域/反震隐藏技能由显化装配 |
| 灵藤种 | `OnKill` 触发位置生成有生命周期的毒藤 Area | 生长/相邻传播隐藏技能由显化装配 |

回程命中额度耗尽后只停止继续命中，不提前删除投射物；投射物仍须返回 Owner，之后才触发
`ReturnComplete` 或清理。预览器对非 Timer 技能生成匹配其声明的合成事件，因此灵藤种也有非零、可重复
的 1/4/8 级 Golden。所有运行时扩展均为通用模块逻辑，Simulation 中禁止出现 `qinglan.*` 常量。
