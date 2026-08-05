# M03 Demo 属性、伤害与状态

## 1. 复用边界

生命、护盾、伤害、状态、死亡和事件完整复用 M3 管线。技能、灵物、Boss 和地图目标只能提交请求，
不能直接写 Health/Shield/Status Store。结构删除仍由 Cleanup 执行。

## 2. 属性映射

| 产品属性 | 现有 Stat | 处理 |
|---|---|---|
| 最大生命 | `base.stat.health` | 直接使用 |
| 生命恢复 | `base.stat.regeneration` | 需确认生产 Pipeline 已实际消费；否则 CR-10 |
| 护甲 | `base.stat.armor` | 直接使用 |
| 移动速度 | `base.stat.move_speed` | 直接使用 |
| 威力 | `base.stat.damage` | 直接使用 |
| 攻击间隔 | `base.stat.attack_speed` / `base.stat.cooldown` | 明确技能公式，避免双乘 |
| 作用范围 | `base.stat.range` | 直接使用 |
| 持续时间 | `base.stat.duration` | 直接使用 |
| 投射物数量 | `base.stat.projectile_count` | 直接使用 |
| 穿透 | `base.stat.pierce` | 直接使用 |
| 暴击率 | `base.stat.critical_chance` | 直接使用 |
| 拾取范围 | `base.stat.pickup_range` | 直接使用 |
| 幸运 | `base.stat.luck` | 只作用非唯一随机奖励 |
| 投射物速度 | `base.stat.projectile_speed` | G1.1 已追加并由移动 Delivery 消费 |
| 暴击伤害 | `base.stat.critical_multiplier` | G1.1 已追加并由 DamageResolution 消费 |
| 击退抗性 | `base.stat.knockback_resistance` | G1.1 已追加并由击退 Effect 消费 |
| 经验获取 | `base.stat.experience_gain` | G1.1 已追加并由 Experience 消费 |

G1 不能用伤害/冷却等不等价 Stat 冒充缺失属性。若 Demo 内容不消费某缺失项，可将其从 Demo UI
隐藏并在 G0 明确 Deferred；玩家文案不得宣称存在。

## 3. 状态配置

| 状态 | 策略 | 行为 | Boss 规则 |
|---|---|---|---|
| 灼烧 | AddStacks | Fire 周期伤害 | 可叠层，伤害有效 |
| 中毒 | AddStacks/Independent | Poison 周期伤害 | 可叠层，设总实例上限 |
| 减速 | ReplaceIfStronger | MoveSpeed Modifier | 抗性/递减，不能降为 0 |
| 定身 | RefreshDuration | MoveSpeed Override/Clamp | Boss 转为重度减速或短硬直，禁止永久冻结 |
| 破甲 | ReplaceIfStronger | Armor Modifier | 可生效，有下限 |
| 标记 | 待 CR-03 | 计数、消费、引爆/增伤 | 需最大层数与触发间隔 |
| 免伤 | RefreshDuration | `base.damage_policy.immune.*` 标签 | 按 DamageChannel 可细分；完全免疫不发 DamageApplied |

状态 Definition 只表达通用行为，不能在系统中判断 `qinglan.status.*`。

## 4. 接触伤害

普通敌人可通过随身 Aura/Area 攻击 Skill 产生接触伤害，统一进入 DamageRequest。玩家侧锁定 0.6 秒
（18 Tick）接触伤害保护窗口；该保护使用 Target＋`base.damage_channel.contact` 冷却，不由 View 碰撞
或角色 ID 控制。

Boss 高危技能使用独立 `base.damage_channel.boss_hazard`，不共享普通接触伤害保护。CR-2026-014 已在
G1.1 实现固定容量通道、屏障和免疫；G1.2 增加状态驱动的通用免疫标签消费者。

## 5. 伤害规则

沿用：来源 Damage → 暴击 → Armor/Resistance → 单包边界 → Shield → Health → Event → Death。

- 物理：`damage * 100 / (100 + armor)`；
- 元素抗性最大 95%；True 跳过减免；
- 单包最大 1,000,000；ProcDepth 最大 8；
- Boss 首通奖励不从 Damage/Death 随机流决定；
- 破甲不能将公式推入分母非正或非有限值；
- 所有伤害预警必须能映射到实际伤害包来源 ID。

## 6. 状态与控制递减（CR-07/10 输入）

建议 Boss 保持以下纯数值快照：SlowResistance、RootConversion、KnockbackResistance、
ControlDiminishingWindow。控制累计只按稳定类别和 Tick 更新，不按具体 Boss ID 分支。

普通敌人也要限制无限连锁：同来源控制重触发需有最小间隔；状态实例/层数存在硬上限；达到上限时
记录诊断而不是继续分配。

## 7. 事件契约

M02 消费 DamageApplied 判定乘风降档；M13 消费 Damage/Shield/Status/Death 产生表现；M06/M07
消费 Death 产生通用奖励请求；M10 根据 Health 阈值推进阶段但不得由 UI 推算。

## 8. 测试与验收

| 类型 | 必须覆盖 |
|---|---|
| Content Validation | 状态参数、非有限值、叠层、周期、Modifier、标签 |
| EditMode | 七种状态、Boss 递减、接触保护、多段伤害、ProcDepth |
| EditMode | 缺失 Stat 不被错误映射；属性公式不双乘 |
| PlayMode | 状态图标、Boss 抗性提示、危险预警与实际命中一致 |
| Performance | 状态高水位无持续分配；截断计数可审计 |

退出条件：所有 Demo 伤害与状态只走集中管线，Boss 不可永久冻结，产品属性与实际公式一一对应。
