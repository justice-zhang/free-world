# ADR 0008：M6 局内成长、构筑、候选与进化运行时

- 状态：Accepted
- 日期：2026-07-26
- 决策人：依据当前用户 M6 指令

## 背景

M5 已能在固定 Tick 中生成和驱动敌人，但敌人奖励尚未进入经验拾取，应用层也没有暂停式
升级选择、局内技能/被动库存、构筑联动或进化。M6 需要新增这些通用能力，同时保持稳定
ContentId、专用随机流、Cleanup 结构写入和 Simulation 不依赖 Unity 的既有边界。

## 决策

### Content Schema 5

- 新增 Passive、Trait、UpgradeOffer、Synergy、Evolution 五种纯运行时定义。
- BuildCondition 固定支持 OwnsContent、HasTagCount、SkillLevelAtLeast、StatAtLeast、MapHasTag。
- SynergyOutput 固定支持 AddModifier、UnlockOffer、AddEffectOp、TransformSkill、GrantTrait。
- Evolution 显式保存来源技能、等级、被动要求、附加条件、结果技能和被动消费策略。
- Schema 1–4 继续加载；Schema 5 内容必须重 Bake，存档和跨边界数据仍保存稳定 ContentId。

### 模拟与应用边界

- `BuildRuntimeCatalog` 在 Run 开始前解析内容、Stat 和 Effect 引用；`BuildState` 是库存、标签、
  联动、进化资格、Modifier 与附加 Effect 的唯一真值。
- 敌人死亡只排队 XP Pickup；Pickup 实体只在 Cleanup 创建。Pickup、Experience、
  LevelUpRequest 依次在固定 Tick 中推进。
- `OfferGenerator` 使用由 Run Seed 派生的独立随机流，记录 RootSeed、调用计数、候选与
  Generate/Reroll/Select/Banish/Skip 历史；不使用全局随机数。
- `RunSession` 位于 Application，负责 SimulationClock 的暂停/恢复、升级命令、Run End 与
  不可变 RunResult。候选过滤规则不进入 UI 或 Application。

### 性能边界

- 固定 Tick 的 Pickup 侧车、经验缓冲和 Build Effect 查询使用持久数组；不在高频路径使用
  LINQ、反射或临时托管集合。
- Offer 数组只在暂停式选择阶段创建，不属于每 Tick 热路径。
- M6 执行两次同种子十分钟正确性 Harness；30 分钟 Soak 和目标实体规模性能 JSON 仍在 M10。

## 被拒绝的方案

- 为具体元素流派建立类：会把构筑判断散落到技能并破坏内容扩展规则。
- 在 UI 中过滤候选：无头运行和重放会与 UI 路径产生不同真值。
- 使用全局或共享战斗随机流：Reroll 会受战斗调用顺序影响，无法稳定诊断。
- 死亡系统直接创建拾取物：会在系统遍历期间产生结构变化。

## 后果

新技能、被动、联动和进化可以通过 Schema 5 配置加入，不修改核心程序集。代价是新增显式
DTO/验证、Run 前编译目录和应用层命令协议。M7 只需展示 `UpgradeOfferSet` 并提交命令，
不能复制候选规则。
