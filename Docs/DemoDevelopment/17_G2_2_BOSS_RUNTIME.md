# 17 G2.2 折枝与听风 Boss 运行时

## 1. 工作包目标

G2.2 交付 M10 的两 Boss 战斗真值：0.7.0 Pack 中的折枝/听风 Enemy、三阶段 BossDefinition、10 个
阶段技能、Encounter 一次性生成、三风脉台 8 组合修正、控制抗性、危险实体清理和幂等死亡事务。
Pack 从 0.6.0 / 107 项升级到 0.7.0 / 121 项。

本工作包不创建正式 RewardDefinition、宝箱/灵物/奇物消费、RunResult/Profile 合并、叙事转形、HUD
预警或正式资产；它们依次由 G2.3—G2.8 和 G3 关闭。

## 2. 内容和遭遇接入

| BossDefinition | Enemy Actor | 出现时点/锚点 | 生命 | 控制时长倍率 |
|---|---|---|---:|---:|
| `qinglan.boss.zhezhi` | `qinglan.enemy.boss.zhezhi` | 360.0 s / central | 1,200 | 0.35 |
| `qinglan.boss.tingfeng` | `qinglan.enemy.boss.tingfeng` | 719.9 s / north | 2,600 | 0.25 |

两条 BossRule 是固定时点一次性规则，优先于普通 Spawn Budget。Encounter Scheduler 使用整数 Tick
作为时间真值，避免 12 分钟累计 float 漂移；30 Hz 推进 21,600 Tick 时两 Boss 均精确生成一次。

## 3. 阶段和技能集合

折枝阈值为 65% / 30% / 0%，各阶段启用横枝试剑、落木剑影、演武木桩。听风阈值为
70% / 35% / 0%，阶段技能集合为：

| 阶段 | 技能集合 | CleanupPolicy |
|---|---|---|
| 守门 | 剑气、定向冲锋 | ExpireOnPhaseExit |
| 听风 | 遮蔽风场、假剑鸣、残剑 | FinishCurrentTelegraph |
| 旧誓不散 | 交叉风痕、残剑、旧誓 | Persist（只允许无害残留） |

Boss Spawn 时低频预加载所有唯一技能，当前阶段之外的实例由 `SkillRuntime` 抑制。BossPhaseSystem 在
SkillTrigger 前同步阶段，因此跨阈值的当前 Tick 不会继续启动旧阶段技能。离开阶段时旧阶段 Projectile/
Area Delivery 先从伤害记录解绑，再由集中命令缓冲清理。

## 4. 阈值、致命伤与奖励顺序

阶段只由 Content 阈值解析，不重置生命。一次伤害可按顺序跨多个阶段；同 Tick 致命伤优先进入阶段
终点，随后只产生一次死亡完成标记。Boss 奖励事务键为：

```text
RewardTransactionId(RunId, BossDefinitionId, 0)
```

重复 Finalize 返回 false，不增加 CompletedCount。G2.2 的正式 Boss RewardId 有意保持空值，避免在
G2.3 Reward/Pickup 内容建立前伪造消费者；运行时对合法 RewardId 的幂等路径已有自动测试。

## 5. 三风脉台 8 组合

听风在 Phase 2/3 接受引风、听风、止衡三个 Objective 稳定 ID。完成状态投影为三位 Mask：

| 位 | 目标 | 参数修正 |
|---:|---|---|
| 0 | 引风台 | 空间负载 ×0.70 |
| 1 | 听风台 | 欺骗/假预警 ×0.65 |
| 2 | 止衡台 | 技能间隔 ×1.25 |

三位同时开启时额外输出 BonusOutputEligible。任何组合都不修改 Phase、不直接扣血；Golden 覆盖
0—7 全组合，并断言空间/欺骗参数不低于 0.70/0.65。

## 6. 清理和抗性

- `ExpireOnPhaseExit`：禁伤、解绑并排队移除。
- `FinishCurrentTelegraph`：已开始预警可完成，但状态立即变为 TelegraphOnly 且 DamageEnabled=false。
- `Persist`：阶段切换可保留无害效果；Boss 死亡仍全部过期。
- Owner 移除前统一清理其 Projectile/Area Delivery，之后再释放技能实例和 Actor。
- `status.control` 时长乘 Boss 抗性倍率并限制最短 0.1 s；DOT、标记等非控制状态保持原时长。

## 7. 容量和确定性

默认 4 个活动 Boss、128 个 Boss-owned Effect；每 Boss 最多绑定 3 个目标规则和 64 个唯一阶段技能。
所有容量在 Run 装配时创建，阶段解析/切换不创建临时托管集合。无参构造仍映射到默认容量，保持旧
调用者二进制签名。

## 8. API 与兼容

- Game.Core：168 / `25766747...d7e176`，不变；
- Game.Content.Runtime：940 / `cd72d779...e35b00`，不变；
- Game.Simulation：1331 / `e41c43a1...4895a`，批准追加 58 条、删除 0；
- Game.Application：355 / `f57fe00c...f8a6`，不变；
- Game.Platform.Abstractions：73 / `8eb5f2cc...a51738`，不变。

详见 ADR 0020。新增 API 是 Boss 快照、规则修正、阶段转换、Owned Effect 和容量构造；没有删除旧
类型或成员，也没有改变程序集依赖方向。

## 9. 冻结内容值与退出条件

- Content Hash：`a654cca5b99f355d9d5122fe106fa4bdba73aebcd745ddbbf136446b5214895a`；
- Pack：0.7.0 / Schema 6 / 121 definitions；
- Focused：G1.6 Encounter 7 项＋G2.2 Boss 8 项，共 15/15；
- 阶段解析：54,000 次固定循环 0 B；
- API diff：Simulation 添加 58、删除 0，其余程序集 0/0。
- 12 分钟 Headless：双实例各 21,600 Tick、2,584 Spawn、2,572 Death、2 Elite、2 Boss、Peak 16、
  0 InvalidHandle，Checksum `049cb8bdc48092eb`；
- 全量 EditMode 253/253、PlayMode 10/10、Project Validation 与 API Freeze PASS；
- 性能短测：600/1,200/2,000/100、900 Tick＋300 预热，Tick p99 `5.2451 ms`、Render p99
  `0.8541 ms`、0 B、GC 0/0/0；
- 两次全 Pack Build 各 7 Pack，Qinglan Catalog SHA-256 均为
  `b2f0a3aca2544619159ca7a1b55b7535c7d79153701e33fcd57c14211a188270` 且字节一致。

Windows Development Build 为 `NOT RUN`，按路线由 G2.8 对完整垂直切片执行。正式视觉/音频 Telegraph
与可访问性对照同样为 `NOT RUN`，由 G2.6/G2.8 关闭，不能用纯模拟清理测试冒充表现验收。
