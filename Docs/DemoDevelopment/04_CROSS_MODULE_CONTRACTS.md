# 04 跨模块契约

- 状态：G0.3 `APPROVED`
- 决策日期：2026-08-04
- 权威 ADR：0013、0014、0015
- 细化契约：[08_G0_3_CONTRACT_FREEZE.md](08_G0_3_CONTRACT_FREEZE.md)

## 1. 数据所有权

| 真值 | Owner | 只读消费者 |
|---|---|---|
| 生命、护盾、状态、死亡 | Simulation Combat | Application、Presentation、UI 投影 |
| 技能实例、冷却、Delivery | SkillRuntime | Snapshot/Preview |
| 敌人行为、Elite/Boss 标记 | EnemyRuntime | Encounter、Presentation |
| 升级候选与 BuildState | ProgressionRuntime | RunSession、UI |
| 地图目标与事件 | MapObjectiveRuntime | Encounter、Boss、Application |
| Boss 阶段 | BossPhaseRuntime | Skill/Map/Presentation |
| 角色机制资源与档位 | CharacterMechanicRuntime | Skill、UI、Presentation |
| Run-local 奖励/选择事务 | RewardRuntime | Progression、Application、UI |
| 局外定义、Loadout 与结果合并 | Meta Coordinator | Run Factory、UI、SaveCoordinator |
| Run 状态与页面转换 | Application | UI、Infrastructure |
| Profile/Settings/Recovery | SaveCoordinator | UI、Run Factory、Platform Router |
| View/VFX/Audio 生命周期 | Presentation | 无反向写入 |

任何模块不能复制其他 Owner 的真值。例如 UI 不根据血量自己推算 Boss 阶段，Boss 也不读取 Scene
GameObject 判断风脉台是否完成。

## 2. 时钟

| 时钟 | 推进内容 | 暂停规则 |
|---|---|---|
| `SimulationClock` | 战斗、技能、敌人、刷怪、目标、Boss | 升级、暂停、剧情选择、结算停止 |
| `UIClock` | 菜单、焦点、过渡、文本 | 不随升级暂停停止 |
| `PresentationClock` | 插值、非关键动画、VFX/Audio | 可在暂停时按 Profile 降速或停止 |

“乘风”按已解析的真实位移积累，只在执行 Simulation Tick 时变化；暂停期间不能由 Transform 位移
或表现插值积累。

## 3. 获批 Demo Pipeline

G0.3 批准以下 G1/G2 目标顺序；旧 M2—M6 Pipeline 构造器继续保留用于兼容回归：

```text
01 InputCommand
02 SpawnScheduler
03 MapObjectiveAndEvent
04 BossPhase
05 EnemyDecision
06 SkillTrigger
07 Movement
08 CharacterMechanicAccumulate
09 SkillDelivery
10 SkillEffectResolution
11 DamageResolution
12 RewardResolution
13 CharacterMechanicReaction
14 StatusTick
15 Regeneration
16 Death
17 LootAndReward
18 Pickup
19 Experience
20 LevelUpRequest
21 Lifetime
22 Cleanup
23 EventFlush
24 SnapshotBuild
```

所有结构创建/删除仍只由 Cleanup 应用；目标、Boss 和奖励系统只写命令/请求缓冲。Map/Boss 消费
上一完成 Tick 的战斗事件；角色反应消费当前 Tick DamageResolution 的实际结果；选择请求只在 Tick
完成后暂停下一 Tick。改变此顺序必须新增 ADR。

## 4. 命令

| 命令 | 生产者 | 消费者 | 约束 |
|---|---|---|---|
| Move | Input Router | Simulation Input | 固定 Tick 采样，记录归一化方向 |
| Select/Reroll/Banish/Skip | UI | RunSession/Progression | UI 不重算候选 |
| ActivateObjective | 玩家接近/交互适配 | MapObjectiveRuntime | 要求距离、状态和防重复验证 |
| ClaimLandmark | Map Runtime | Reward Runtime | 唯一/重复规则由稳定 ID 决定 |
| SelectReward/UseFallback | UI | RunSession/Reward Runtime | UI 不重算资格；同事务只提交一次 |
| EndRun | Boss/失败/调试开发入口 | RunSession | Release 禁用调试入口 |
| EquipNode/Insert | Hub UI | Meta Coordinator | 容量、互斥和解锁在应用真值验证 |

## 5. 领域事件

建议事件只携带纯值、稳定 ID、EntityHandle、Tick 和位置：

```text
CharacterMechanicTierChanged
DamageResolved
MapObjectiveStateChanged
MapEventStarted / MapEventCompleted
BossPhaseChanged
RewardSpawned / RewardCollected
RewardChoiceRequested / RelicSelected / EvolutionSelected
LandmarkDiscovered
RunOutcomeCommitted
MetaLoadoutChanged
```

事件用于表现、UI、低频保存和诊断；不能反向替代 Simulation 真值。Runner 批次必须在下一批 Tick 前
消费，沿用 M2/M3 单生产者批次契约。

## 6. 随机流

| Stream | 用途 | 不得影响 |
|---|---|---|
| Combat | 暴击和战斗确定性 | 候选、事件位置 |
| Encounter | 敌群、精英、出生锚点 | 升级候选 |
| Offer | 升级、Reroll、Banish | 战斗、掉落 |
| MapEvent | 事件/地标锚点选择 | Boss 首通奖励 |
| Reward | 非唯一普通奖励 | 唯一藏品、山河脉印 |
| Presentation | 非真值表现变化 | 任意模拟结果 |

首次 Boss 脉印、唯一剧情物和固定首通奖励不做概率 Roll，幸运不能影响。

## 7. 内容加载和 Run 装配

```text
Load manifests
→ validate dependency/version/hash
→ build ContentRegistry
→ bind runtime catalogs
→ validate selected Character/Map/Encounter
→ validate active Meta loadout and missing content policy
→ construct Map/Enemy/Skill/Progression/Objective/Boss runtimes
→ create player and starting skills
→ emit RunStarted
→ start SimulationClock
```

任何一步失败都进入结构化 `ContentError`，不得带部分 Registry 或半构造 Run 继续。

## 8. 清理与保存事务

Run 结束顺序：冻结时钟 → 得到不可变 RunResult → 计算合法局外增量 → 原子保存 Profile → 删除或
更新 Recovery → 发布 `RunCompleted` → 释放 Scene/Addressables/View/Pool Owner → 进入结算/据点。

若 Profile 写入失败，UI 必须显示可恢复错误，不能展示“已保存”。唯一首通奖励以幂等稳定 ID 写入，
重复提交不得重复发放。

DamageResolution 的固定顺序为：合法性/死亡 → 免疫 → 按 Target＋DamageChannel 冷却 → 暴击/属性/抗性
→ 屏障 → Shield → Health → 事件/冷却。只有 Shield 或 Health 实际减少才发 `DamageApplied`；完全
屏障吸收、免疫、冷却或零伤害只允许发 `DamageResolved` 诊断/表现事件。

## 9. 版本和兼容性

- Content Schema 变化：旧 Schema 1—5 继续可读；新 Pack 重 Bake；
- Save Schema 变化：Settings 2、Profile 3、RunRecovery 2 独立演进；Profile 显式 1→2→3 连续迁移，
  固定 Fixture，主/备份校验；
- 公共 API 变化：ADR、Freeze Hash 更新、完整门禁；
- 表现 Profile 变化：不改变模拟 Hash，但必须更新资产清单和 Addressables Hash。
