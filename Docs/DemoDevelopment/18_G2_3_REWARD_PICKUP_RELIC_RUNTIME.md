# 18 G2.3 奖励、灵物与战斗奇物运行时

## 1. 工作包目标

G2.3 交付 M06 的完整非 XP 奖励真值：六种即时灵物、六种战斗奇物、精英异相灵核三选一、折枝显化
宝匣、听风固定首通、普通敌人概率掉落、独立 Reward RNG、三槽库存、幂等事务和局内永久结果增量。
Pack 从 0.7.0 / 121 项升级到 0.8.0 / 150 项。

本包不写 Profile、RunResult 胜负合并、叙事转形、实际奖励 UI、正式 VFX/音频或 Windows Build；它们由
G2.4—G2.8 和 G3 关闭。

## 2. 来源与事务

| 来源 | Reward | 事务 Source / Sequence | 交付 |
|---|---|---|---|
| 普通敌人 | 六种即时灵物之一 | Enemy ContentId / Actor 代际序号 | 概率地面 Pickup |
| 精英 Affix | `elite.afflicted_core` 或 splitting | Affix ID / Actor＋Affix 序号 | 地面 Relic Choice |
| 折枝 | `reward.manifestation_chest` | BossDefinition ID / 0 | 地面 Evolution Choice |
| 听风 | `reward.first_clear.tingfeng` | BossDefinition ID / 0 | 固定直接结果 |
| Map Objective | 任意 RewardDefinition | Objective 输出事务 | 直接排队 |

同一事务在待处理、地面存活、选择暂停和已提交状态都不可重复生成。RewardResolution 是唯一消费者；
Death、Boss、Affix 和 Map Runtime 只产生稳定请求。

## 3. 六种即时灵物

| Pickup ID | 效果 | 领取边界 |
|---|---|---|
| `qinglan.pickup.greenwood_dew` | 30 固定＋15% 最大生命治疗 | 满血且无可用过量护盾时不消费 |
| `qinglan.pickup.boundary_talisman` | 6m 敌对范围定身 | Boss 时长继续走抗性倍率 |
| `qinglan.pickup.thunder_jade` | 6.5m、90 雷伤及击退 | 只命中敌对 Actor |
| `qinglan.pickup.spirit_gourd` | 强制吸附普通地面灵物 | 排除选择、唯一、目标锁定 |
| `qinglan.pickup.heart_guard_jade` | 伤害免疫状态 | 复用状态生命周期 |
| `qinglan.pickup.riding_wind_feather` | 4s、移速 ×1.45 | 仍受 Map 硬边界约束 |

所有 Pickup 生命周期为 90 秒并由 Cleanup 结构变更；吸附速度 15，半径 0.65—0.75。OnPickup Trigger
继续进入 M4 Skill Runtime，XP Pickup 保持 M6 原路径。

## 4. 六种战斗奇物

| Relic ID | 输出 | 锁定规则 |
|---|---|---|
| `qinglan.relic.broken_sword_tassel` | 2.8s 有界次级投射剑气 | 不递归触发自身 |
| `qinglan.relic.wind_vein_copper` | 移速 +10%、拾取范围 +20% | Trait 输出 |
| `qinglan.relic.herb_garden_seed_pod` | 生命 +10%、回复 +0.75；溢出治疗转 20% 上限护盾 | Trait＋通用标签 |
| `qinglan.relic.listening_wind_core` | 2.2s 最多 3 目标稳定回响 | 隐藏 Skill 输出 |
| `qinglan.relic.old_court_bell` | 6s、7m 周期减速脉冲 | 控制走 Boss 抗性 |
| `qinglan.relic.blank_sword_trial_token` | Boss 伤害 +25%、承伤 +15%、护甲 ×0.85 | 风险只按通用标签生效 |

库存固定三槽，全部内容 MaximumLevel=1，因此不重复、不覆盖。每次精英选择从当前合格六项无放回抽取
最多三个；槽满、前置/互斥导致空池时固定发 5 灵砂。

## 5. 显化与首通

折枝显化宝匣引用六个已锁定 Evolution，复用 G1.7 RewardChoiceRuntime：当前构筑最多展示三个合格项，
选择时再次验证；空池不调用随机流并执行固定灵砂 fallback。听风首通不随机，固定输出
`qinglan.progress.region_mark.qinglan` 和 25 灵砂；若 Profile 快照已拥有唯一脉印，则唯一项回退为 5 灵砂，
固定 25 灵砂仍发放。

Currency/Unique/Unlock/Story 仅保存为 `RewardResultEntry`，G2.5 才把它们与 Profile v3 原子合并。

## 6. Pipeline 与 Application

```text
Death / Boss / Affix / Map output
→ RewardResolution（解析 Definition 或生成地面请求）
→ PickupSystem（吸附、领取、排队直接奖励）
→ Cleanup（创建/删除 Pickup）
→ Tick 边界 PauseRequested
→ RunSession 投影 RewardChoice
→ SelectReward 再验证、提交并恢复 Clock
```

Relic Choice 优先于同 Tick 普通 Evolution Choice；同一时刻最多一个受控奖励页面。Application 只看到稳定
ContentId 候选，不读取 RuntimeIndex 或 Unity Object。

## 7. 容量、性能与诊断

- Demo 默认：4,096 整局事务、512 同帧执行/活动结构、三 Relic 槽。
- 选择和 Definition 绑定是低频路径；Pickup Tick 使用稠密 Store、固定 sidecar 和 SpatialGrid。
- 诊断：`RejectedCapacity`、`PickupCapacityGrowthCount`、`RandomCalls`、活动 Pickup 和结果条目数。
- 目标压力：5,000 Reward Pickup 预建后连续 120 次扫描 0 B，容量增长和拒绝均为 0。

## 8. API 与兼容

- Game.Core：168，不变；
- Game.Content.Runtime：940，不变；
- Game.Simulation：1396 / `4d5bfc3d...c6410c32`，批准追加 65 条、删除 0；
- Game.Application：355，不变；
- Game.Platform.Abstractions：73，不变。

旧 `RewardRuntime(int = 128)`、XP Pickup、旧 Pipeline 和 Save Schema 均保留。新增公开面只包含纯值结果、
Relic/Pickup 快照、只读诊断和选择提交；详见 ADR 0021。

## 9. 冻结内容值与退出条件

- Pack：0.8.0 / Schema 6 / 150 definitions；
- Content Hash：`5f233508384d0f9b4b5babc98571ccd45e0d35319c776f4de217ae99e3107c9d`；
- API diff：Simulation 添加 65、删除 0，其他四个程序集 0/0；
- Focused EditMode 20/20、全量 EditMode 261/261、全量 PlayMode 11/11；
- Project Validation、API Freeze、12 分钟 Headless、Pack 双构建均 PASS；Headless Checksum
  `049cb8bdc48092eb`；
- 性能短测复测 Tick p99 4.8810 ms、Render p99 0.6311 ms、0 B、GC 0/0/0；首轮后台 GC FAIL
  证据保留；
- 两次 Pack CLI 各构建 7 Pack，Catalog SHA-256 均为
  `b274fc24afa07194682968eb2a290e0b3e12c631a621dd98f2799f0925702236` 且字节一致。

Windows Development Build 和实际奖励 UI/输入为 `NOT RUN`，路线固定由 G2.8/G2.6 关闭；不能用
Simulation 测试冒充表现验收。
