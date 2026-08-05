# M07 六类敌人与四种精英词缀

## 1. 通用配置

EnemyDefinition 继续使用基础生命、半径、移速、伤害、攻击范围、AttackSkill、奖励、行为和
VisualProfile。所有敌人在单次稠密 `EnemyDecisionSystem` 中推进，不创建 Enemy Controller 或
NavMeshAgent。

## 2. 六类普通敌人

| 敌人 | MovementMode | 攻击技能 | 压力功能 | 主要克制方式 |
|---|---|---|---|---|
| 草灵 | Chase | 接触 Aura | 群聚/占位 | 范围、穿透 |
| 纸鹤符灵 | Charge | 侧后俯冲 | 追袭/打断路线 | 转向、预判、击退 |
| 木制剑傀 | Chase | 重接触/近域斩 | 重甲/抗击退 | 破甲、持续伤害 |
| 石灯守卫 | Ranged | 蓄力石火弹 | 远程封路 | 移动、优先击杀 |
| 鸣风铃灵 | KeepDistance | 加速/护盾 Aura | 支援 | 集火、穿透 |
| 爆裂种囊 | Chase/Idle | 死亡或延迟爆裂 Area | 环境/危险区 | 提前击杀、路线避让 |

现有 M5 只支持四种移动模式和单一 AttackSkill。蓄力、死亡爆裂、支援目标等优先由 Skill/状态组合；
若需新条件必须纳入通用 CR，不按 EnemyId 分支。

## 3. 数值角色

每个敌人记录相对草灵的 Health、Speed、Damage、XP、BudgetCost、CollisionRadius 和出场分钟倍率。
数值在 G1 Timeline Analyzer 中共同平衡，目标是组合压力而非无限加血。

- 木制剑傀的抗击退通过通用 KnockbackResistance，不把碰撞做成不可穿墙；
- 纸鹤 Charge 有清晰 Windup、冲刺方向锁定和 Recover；
- 石灯弹有最大存活、扫掠碰撞和屏外回收；
- 鸣风铃 Aura 不能互相无限叠盾/加速；
- 种囊危险区有最大并发、持续时间和危险轮廓。

## 4. 精英词缀（CR-08）

| 词缀 | 输出 | 互斥/限制 | 表现 |
|---|---|---|---|
| 狂奔 | MoveSpeed/Charge 冷却 Modifier | 与超高速基础纸鹤可互斥或封顶 | 连续风纹、急促音 |
| 结界 | 周期护盾或防护 Aura | 支援敌人组合需护盾总量上限 | 清晰罩体、破盾音 |
| 分裂 | 死亡生成较弱同类/子体 | Boss 禁用；代数 1；不继承分裂 | 裂纹预告、两枚子体 |
| 震地 | 周期/受击后 Area 冲击 | 与自爆种囊高危区密度互斥 | 地面预警、低频重音 |

EliteAffixDefinition 应包含兼容/互斥标签、Modifier、附加 Skill、死亡输出、奖励倍率和 Profile；
Encounter Entry 只选择合法 Affix 组合。当前 Elite bool 的 1.5 倍倍率不能代表四种词缀完成。

## 5. 生成与保护规则

- 生成位置由 IMapRuntime 采样且满足 min/max 距离、Walkable 和硬边界；
- 普通敌人不能在玩家视野中心/不可逃区域突然生成；
- Boss 保留并发槽，精英触发不得挤掉固定 Boss；
- 同屏高危远程/震地/种囊数量设独立预算；
- Director 不因玩家高 DPS 动态惩罚，只按固定时间轴和选择的难度快照。

## 6. 掉落与死亡

普通敌人死亡排队 XP；Elite 额外排队一个异相灵核。分裂子体是否给 XP 必须在 Affix Definition
明确，默认不重复提供完整奖励，防止刷取。Boss 走 M10 固定 Reward Rule。

## 7. 测试与验收

| 类型 | 必须覆盖 |
|---|---|
| Validation | AttackSkill、Profile、行为数值、Affix 兼容/互斥 |
| EditMode | 六种行为、有限坐标、支援上限、死亡爆裂、分裂代数 |
| Headless | 固定 Seed 12 分钟 Spawn/Death/Elite Checksum |
| PlayMode | 轮廓、预警、音效、玩家可穿过普通敌人 |
| Performance | 600—1200 正常敌人、支援/危险区上限、0 高频分配 |

退出条件：六类敌人功能可辨、四词缀可组合且无复制定义爆炸，所有行为集中推进并通过公平生成保护。

## 8. G1.5 实施冻结

G1.5 已在 `qinglan.pack.demo` 0.4.0 创建六 Enemy、九 Skill、三 Status、四 Affix、一个 Trait 与两个
Reward。词缀由 Encounter RNG 在 Spawn 前选择至多两个合法组合；Spawn 时绑定 Modifier/Skill/
DeathReward/RewardMultiplier，固定 Tick 不解析字符串。鸣风铃使用 ADR 0016 的
`base.targeting.allies_circle`，最多六名友军且排除自身；分裂使用 `spawn_enemy` Reward 操作，限制一代、
两个 0.35 倍子体且不继承 Elite/Affix。

G1.5 自动化已证明六定义/攻击引用、兼容/排除/Boss、固定 Seed、实际 Modifier/Shield、友军上限、
分裂代数与 600 敌人集中决策 0 B。12 分钟 Timeline/Headless 由 G1.6，异相灵核选择由 G2.3，正式
轮廓/预警/音频 PlayMode 由 G2.6/G3；这些未执行项目不得计入 G1.5 PASS。
