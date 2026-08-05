# ADR 0016：G1.5 精英词缀执行、友军目标与有限死亡生成

- 状态：Accepted
- 日期：2026-08-05
- 决策人：依据用户当前连续 Demo 开发指令
- 关联里程碑：G1.5、G1.6、G2.3
- 关联 CR：CR-2026-011

## 背景

G0.3 已接受 `EliteAffixDefinition`、Encounter Affix Pool、修正、附加技能、死亡输出、奖励倍率和
表现 Profile，但 G1.1 只交付了定义与资格查询骨架。G1.5 实现六类敌人时发现两个执行缺口：现有
Targeting 只能选自己或敌对 Actor，无法配置鸣风铃的友军支援；Reward 操作没有可复用的有限敌人
生成操作，无法表达同一 Affix 作用于不同 Enemy Archetype 的一代分裂。

按 EnemyId 编写支援/分裂分支或复制 EnemyDefinition 均违反内容扩展规则，因此在获批 CR-2026-011
范围内补齐通用执行契约。

## 决策

### 友军圆形目标

新增稳定模块 `base.targeting.allies_circle`：V0 为半径、I0 为最大目标数；排除 Owner、死亡中 Actor
和敌对阵营，按距离及稳定 Handle 顺序截断。模块在加载期显式注册，固定 Tick 不做反射、字符串查找
或临时集合分配。鸣风铃用该模块向最多六名友军施加 RefreshDuration 护盾状态；同一状态不叠加，
因此多个铃灵不能无限叠盾。

### Elite Affix Spawn 绑定

Encounter RNG 在稳定、已 canonical 的 Affix Pool 上选至多两个合法词缀。Required/Excluded Tag 同时
检查 Enemy Tags 与已选 Affix Tags；Boss 不进入普通词缀组合。SpawnRequest 内部携带固定两槽绑定，
创建时一次安装 Trait/Passive/Synergy 修正、附加 Skill、DeathReward、RewardMultiplier 和 Profile ID；
Tick 只消费已绑定结果。公共 `SpawnRequest` 旧构造函数保持不变，空池继续走旧 Elite 1.5 倍兼容路径。

### Reward `SpawnEnemy`

Schema 6 Reward 新增操作码 `SpawnEnemy = 11`：`IntegerValue` 为 1—2 个子体，`Value` 为
`(0,1]` 的生命/伤害/奖励倍率，`ReferenceId` 为空表示沿用死亡 Enemy Archetype，否则必须引用可执行
Schema 4 Enemy。结构创建仍由 Cleanup 完成。

`EliteAffixDefinition` 补齐已冻结设计中的 `MaximumGeneration` 与 `RewardMultiplier`。旧构造函数保留，
默认值为 0 代与 1 倍；Schema 6 DTO 的旧空值按同一默认读取。分裂词缀配置最大一代、两个 0.35 倍
子体；子体不是 Elite、不继承 Affix，因而不能再次分裂。非 `SpawnEnemy` 奖励操作继续由 G2.3 的
RewardResolution 执行；G1.5 只绑定并验证异相灵核 Reward，不提前实现三选一页面。

## 兼容与影响

- Assembly 依赖方向不变；`Game.Core`、Simulation 公共面、Application、Platform 不变。
- `Game.Content.Runtime` 追加一个 Targeting ID、一个 Reward enum 值、Affix 新构造函数与两个只读属性；
  旧成员与旧构造函数不删除。
- Content Schema 仍为 6；这是 Demo 首发前的向后兼容追加。Schema 1—5 Codec/Hash 不变，Schema 6
  Affix 必须重新 Bake，Pack 从 0.3.0 升至 0.4.0。
- Save Schema 不变；存档仍只保存稳定 ContentId，不保存 Affix RuntimeIndex 或 Spawn 绑定。
- 随机性只使用 Encounter 流；Combat/Offer 流不受影响。
- 热路径使用固定 Affix 两槽与复用空间查询缓冲；600—1200 敌人门禁要求热决策 0 B。

## 被拒绝的方案

- 按鸣风铃或分裂词缀 ID 在 Simulation 分支：不可复用，违反内容扩展规则。
- 复制每个“敌人×词缀”定义：造成组合爆炸与数值漂移。
- 让友军技能继续使用 hostile Circle 但只改文案：行为与设计承诺不符。
- 在 DeathSystem 直接创建 Actor：破坏 Cleanup 单写入者契约。
- 让分裂子体继承父 Affix：形成指数增长和奖励刷取。

## 测试与迁移

1. 旧 Hash 下输出规范 API diff，仅允许本 ADR 的 `Game.Content.Runtime` 追加。
2. 覆盖六 Enemy、四 Affix、合法/排除/Boss、确定性、修正/附加 Skill、友军上限与一代分裂。
3. 运行完整 EditMode、PlayMode、内容验证、API Freeze 与性能短测；通过后更新 Freeze。
4. 回滚时先清空 Encounter Affix Pool，再移除新内容；保留旧 enum/token 读取，避免已发布 Catalog 失效。
