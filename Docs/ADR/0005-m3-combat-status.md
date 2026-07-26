# ADR 0005：统一属性、伤害、状态与 Content Schema 2

- 状态：Accepted
- 日期：2026-07-26
- 决策人：依据 M3 当前用户指令

## 背景

M2 提供固定 Tick、Generation-safe Handle、专用 Store、结构命令与 Runner 批次事件，
但没有统一属性、生命、护盾、伤害、状态或死亡真值。M3 还需要把状态作者数据烘焙为
纯运行时定义；M1 的 Schema 1 只认识 Character、Skill、Enemy 和 Map。

## 决策

### 属性

- `StatId` 是可持久化的 canonical 稳定 ID；M3 内置 ID 使用 `base.stat.*` 命名空间。
- `StatIndex` 只在当前 Stat Catalog 内有效，不得写入存档。
- M3 固定注册 14 项：Health、MoveSpeed、Damage、AttackSpeed、Cooldown、Range、
  Duration、ProjectileCount、Pierce、CriticalChance、Armor、PickupRange、Luck、
  Regeneration。
- Modifier 只在加入集合时把 StatId 解析为 StatIndex，并把非空 StackingGroup 映射为
  collection-local 紧凑整数。读取使用数组缓存和整数比较，不做字符串查找或临时集合分配。
- 计算顺序固定为：

```text
Base
→ AddFlat（求和）
→ AddPercent（求和后乘以 1 + sum）
→ Multiply（逐项相乘）
→ ClampMinimum / ClampMaximum
→ Override
→ Stat 域硬边界
```

- 同一非空 StackingGroup 只保留同 Stat、同 Operation 的一个生效项：Priority 高者
  胜出；Priority 相同则后加入者胜出。被压制项仍保留，因此胜出项过期后可恢复。
- 生效项按 Priority 升序、加入序升序执行；这使高 Priority Override 最后生效。

### 战斗数据与伤害

- Actor 的 Health、Shield、Resistance、Stat 和 Status 存在 Handle-slot 侧车中，
  Generation 不匹配时拒绝访问，不放入公开 `SimulationEntityState`。
- 对外只暴露只读 Health/Shield 快照；伤害系统是伤害导致的 Health 唯一写入者。
- `DamagePacket` 使用 `SpatialEntity` 标识来源和目标，并携带来源 ContentId、
  DamageType、位掩码 Tags、BaseValue、暴击资格、ProcCoefficient、Knockback、
  Position 和 ProcDepth。
- 结算顺序固定为：

```text
验证目标与 ProcDepth
→ 规范化基础伤害
→ 来源 Damage 属性
→ 固定随机流暴击
→ Armor 或 Resistance
→ Damage 硬边界
→ Shield
→ Health
→ DamageApplied
→ DeathRequest
```

- Physical 伤害使用 `damage * 100 / (100 + armor)`；元素抗性限制在 `[0, 0.95]`；
  True 伤害跳过二者。
- 默认单包伤害边界为 `[0, 1_000_000]`，暴击倍率为 `2`，最大 ProcDepth 为 `8`。
- Damage 使用由 World Seed 派生并长期持有的独立随机流，避免其他随机调用改变暴击序列。
- `DamageType` 与 `DamageTags` 下沉到无 Unity 依赖的 `Game.Core`，使内容定义和模拟共享
  同一纯领域类型，不增加反向程序集依赖。

### 状态、死亡与事件

- Status 实例只持纯 `RuntimeStatusDefinition`、RuntimeContentIndex 和数值状态；Modifier、
  周期伤害及临时护盾行为属于 `RuntimeStatusDefinition.Behavior`。申请请求不能携带或覆盖
  行为，系统不得按 Burning、Slow、Shielded 等具体 ContentId 分支。
- RefreshDuration 保留单实例并刷新；AddStacks 在单实例上叠加至 MaxStacks；
  ReplaceIfStronger 只接受更强值、相等值只刷新；IndependentInstances 为每次申请
  建独立生命周期并受 MaxStacks 限制。
- 新状态在申请 Tick 的状态阶段末加入，不在同一 Tick 立即扣除持续时间或产生周期 Tick。
- 状态周期伤害只排入 DamageRequest，下一 Tick 才由 DamageResolution 结算。
- 周期累计只使用 `min(TickDelta, RemainingDuration)`；`DeathPending` / `Dead` Actor 不推进
  状态。周期触发继承并递增 ProcDepth，超过上限时记录截断且不排入伤害。
- ShieldCapacity 是临时实例贡献，按 Strength 和 stacks 缩放；刷新不重复扩容，替换、
  过期或驱散回收对应容量。ShieldChanged 同时携带当前值和最大值的前后状态，最大值单独
  变化时也必须发出；有限贡献聚合为非有限容量时原子拒绝该状态申请。
- DeathResolution 首次致死时标记 DeathPending；DeathSystem 只发一次 EntityDied 并
  排队 Remove，Cleanup 仍是唯一结构删除点。
- M3 事件使用两级结构体缓冲：系统写 per-tick pending，EventFlush 把它追加到公开
  Runner batch 后清空 pending。一次 catch-up 的多个 Tick 累积；0-Tick Advance
  保留公开批次；下一次实际执行 Tick 的 Advance/Step 清空旧批次。
- World 在每次 Pipeline 返回后执行一次幂等 Flush；显式自定义 Pipeline 即使省略
  EventFlushSystem 也不会静默丢失事件。
- M3 默认 Pipeline 为：

```text
Movement
→ DamageResolution
→ StatusTick
→ Death
→ Lifetime
→ Cleanup
→ EventFlush
→ SnapshotBuild
```

M2 的 `CreateM2Default()` 保持原四系统顺序，供契约测试和兼容装配使用。

### Content Schema

- Schema 2 新增显式 `status` kind、四种稳定 wire token、Duration、MaxStacks、
  TickInterval、DispelTags、ImmunityTags，以及 Modifier、PeriodicDamage、ShieldCapacity
  三类通用 Behavior 字段。
- Runtime 同时加载 Schema 1 和 Schema 2；Schema 1 内容包不得包含 status。
- 既有 M1 Schema 1 测试包、ContentId 和 Content Hash 不改变。需要状态的包升级到
  Schema 2 并重新 Bake。
- Registry 仍使用现有多态定义路径，无反射、无程序集方向变化；Save Schema 不变。
- Actor 战斗记录、Stat 数组、Modifier 数组和 Status 数组按 Handle slot 保留并复用；
  只有并发高水位增长时创建新记录。零最大生命或零当前生命的 Actor 初始化被原子拒绝。

## 被拒绝的方案

- 把 Health 放入公开 `SimulationEntityState`：现有公开 TryWrite/SetStateAt 会绕过伤害管线。
- 在 DamageSystem 中按技能或状态 ID 分支：破坏内容扩展边界。
- 每次属性读取扫描字符串 ID 或创建集合：违反高频路径约束。
- 每 Tick 清空唯一公开事件缓冲：会丢失同一次 catch-up 中较早 Tick 的事件。
- 静默把 status 写入 Schema 1：破坏格式版本和迁移审计。

## 后果

优点：伤害和状态具有单一真值入口、固定种子可复现、状态内容可扩展、M2 结构删除和
事件批次契约继续成立。代价：Actor 需要维护战斗侧车与状态实例数组；状态标签匹配仍是
正确性优先的线性比较，写入阶段的 StackingGroup 映射维护一个复用 Dictionary；只有
后续基准证明状态标签是热点时才允许更换后端。

兼容性影响：旧 Schema 1 内容可直接加载且原 M1 Catalog Hash 不变；Schema 2 状态行为
字段进入 Catalog Hash，包含状态的 Pack 必须重新 Bake。`DamageType`/`DamageTags` 的公开
命名空间从 Simulation 调整为 Core；该 API 尚未发布。M8 尚未实现存档，因此本 ADR
不引入存档迁移。
