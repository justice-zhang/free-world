# ADR 0007：M5 敌人、刷怪、Encounter 与地图运行时

- 状态：Accepted
- 日期：2026-07-26
- 决策人：依据当前用户 M5 指令

## 背景

M2–M4 已提供固定 Tick、Generation Handle、空间网格、战斗状态和模块化技能，但尚无
可配置敌人决策、刷怪预算、Encounter 或地图运行时。M5 需要在不引入逐敌人
MonoBehaviour、NavMeshAgent、场景时间轴或运行时反射的前提下补齐这些边界。

## 决策

### Content Schema 4

- Schema 4 Enemy 增加基础移动/伤害、攻击 SkillId、奖励、VisualProfileId 和通用行为参数。
- Schema 4 Map 增加 Bounds、EncounterId、程序化运行时参数、简化障碍和稳定 Anchor。
- 新增 Encounter kind，包含 Phase、线性预算/间隔、加权 Enemy Entry、并发上限和 Boss Rule。
- Schema 1–3 继续加载；旧 Enemy/Map 不会被静默视为可运行的 M5 定义，也不会改变旧 Hash。
- Catalog/存档保存稳定 ContentId；EnemyRuntimeCatalog 在 Run 开始前解析 RuntimeContentIndex。

### 模拟与结构变化

- EnemyRuntime 是 ActorStore 的紧凑侧车，保存定义索引、行为状态和计时器，不开发通用 ECS。
- Spawn Scheduler 只写复用 SpawnRequestBuffer；Enemy Actor 只在 Cleanup 应用阶段创建。
- M5 Pipeline 为：SpawnScheduler → EnemyDecision → SkillTrigger → Movement → SkillDelivery →
  SkillEffectResolution → DamageResolution → StatusTick → Death → Lifetime → Cleanup →
  EventFlush → SnapshotBuild。
- 敌人攻击复用 M4 Skill Instance；敌人与玩家通过 EnemyRuntime 的阵营判定互为合法目标。

### 地图运行时

- `IMapRuntime` 只使用数值、稳定 ID 和 RandomStream；不持有 Scene 或 Unity Object。
- FiniteArena 使用边界、矩形障碍和 Anchor；ChunkedInfinite 用 Root Seed 与 Chunk 坐标生成
  稳定签名，并只维护焦点周围固定半径的逻辑活动窗口。
- Chunk 激活/释放只改变可查询环境窗口，不改变既有实体身份或随机序列。
- 地图 Scene 只保存 Placeholder 视觉根，不包含刷怪逻辑。

### 性能边界

- 决策、分离、刷怪和地图查询使用持久数组/查询缓冲；固定 Tick 不使用 LINQ、反射或字符串格式化。
- M5 运行五分钟确定性 Headless Harness；1,500/3,000/5,000 压力与 30 分钟 Soak 仍由 M10 门禁。

## 被拒绝的方案

- 每敌人 MonoBehaviour/NavMeshAgent：破坏批处理、确定性和性能预算。
- Scene 内波次脚本：地图与 Encounter 无法复用，也不能可靠无头测试。
- 一开始引入 Jobs/Burst 或通用 ECS：没有 M10 测量证据，超出当前里程碑。
- 为四种 Fixture 敌人创建继承树：把内容差异硬编码为类型。

## 后果

新敌人可复用同一行为模块和 Skill，新地图可复用同一 Encounter。代价是 Schema 4 Pack 必须
重 Bake，并维护显式 DTO、验证、地图 Provider 和确定性测试。长期压力优化仍需要 M10 数据。
