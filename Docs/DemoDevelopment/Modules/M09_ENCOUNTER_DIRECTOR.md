# M09 刷怪导演与十二分钟时间轴

## 1. 目标

复用 Schema 4 Encounter 的连续 Phase、预算、间隔、权重、群组、Elite、BossRule 和并发上限，形成
可复现的 12 分钟骨架。地图事件和 Boss 阶段通过独立模块协作，不把时间轴写入 Scene。

## 2. 时间轴

| Phase | 时间 | 敌人池 | 固定事件 |
|---|---|---|---|
| P0 | 0:00—1:30 | 草灵 | 移动/攻击/XP 教学 |
| P1 | 1:30—3:00 | 草灵、纸鹤 | 标记首座风脉台 |
| P2 | 3:00—4:30 | 上述＋木制剑傀 | 3:00 第一精英/灵核 |
| P3 | 4:30—6:00 | 上述＋石灯守卫 | 中段压力上升 |
| P4 | 6:00—6:30 | Boss 专用/最小杂兵 | 6:00 折枝；宝匣 |
| P5 | 6:30—7:30 | 加鸣风铃灵 | 风脉暴动触发窗 |
| P6 | 7:30—9:00 | 加爆裂种囊 | 第二精英 |
| P7 | 9:00—10:30 | 全池复合 | 提示未完成风脉台 |
| P8 | 10:30—12:00 | 高压全池 | 事件/旧庭记忆显现 |
| P9 | 12:00+ | 停止普通刷怪 | 听风登场 |

Phase 必须从 0 连续，无空洞/重叠。12:00 的普通生成预算和剩余生成请求必须清空或进入明确的
Boss Cleanup 策略，不能在停止后继续从积压预算刷怪。

## 3. 预算设计

每 Phase 锁定：BudgetPerSecond start/end、SpawnInterval start/end、阶段并发上限、SpawnPattern、
EnemyEntry 权重/成本/群组和 BossRule。具体数值由 Timeline Analyzer 迭代，但必须满足：

- 普通并发上限始终低于全局上限并为 Boss/精英保留槽；
- 支援、远程、自爆/危险区各有子预算；
- 预算增长来自组合复杂度，非统一 Health 膨胀；
- 精英时间点固定，词缀用独立稳定随机流选择；
- 玩家输出不改变预算曲线，只影响场上清理速度。

## 4. SpawnPattern

| 阶段 | 主要 Pattern | 保护规则 |
|---|---|---|
| 教学 | Ring/Edge | 不在移动方向正前方封死 |
| 纸鹤 | Ambush/Edge | 侧后有前摇和最小视距 |
| 重甲 | Line/Cluster | 保留可穿路线 |
| 远程 | Ring/FixedAnchor | 不能全部在屏外同时开火 |
| 支援 | Cluster | 至少与可支援目标同组 |
| 高压 | 混合 | 高危单位子上限 |

Portal/FixedAnchor 必须引用地图已验证 Anchor。采样失败应使用确定性 fallback，并记录诊断；不得生成
在障碍、硬边界外或玩家重叠位置。

## 5. Boss 与普通敌群

- 6:00 BossRule 生成折枝并置 `Boss=true`；
- 折枝存活时普通预算降到批准值，避免技能教学不可读；
- 12:00 先关闭普通调度和清理/撤离规则，再生成听风；
- Boss 死亡不由 Scheduler 直接结算奖励，只发 Death 供 Reward/Boss Runtime；
- Boss 一次性标记固定 Seed 可复现，Catch-up 不重复生成。

## 6. 难度与重玩

Demo 初始只开放基础难度。隐藏开发配置可测 Health/Damage/Speed/SpawnRate/Elite/Reward 快照，但不进入
玩家正式 UI。重复通关可改变事件/地标位置，不改变固定 Boss 时间和首局教学骨架。

## 7. 测试与验收

| 类型 | 必须覆盖 |
|---|---|
| Validation | Phase 连续、曲线有限、Entry/Boss 引用、Anchor、并发上限 |
| Timeline | 各分钟理论预算、权重、并发、生命、XP、Boss 时间报告 |
| Headless | 21,600 Tick（12 分钟）双实例 Checksum 一致 |
| Headless | 两精英、两 Boss 一次；12:00 后普通 Spawn=0 |
| PlayMode | 生成公平、阶段压力可读、Boss 过渡无残留高危弹幕 |
| Performance | 正常 600—1200 敌人内容场景，Tick/GC/内存趋势 |

退出条件：时间轴严格对应总纲，固定 Seed 可复现，保护规则不会动态惩罚高输出玩家。
