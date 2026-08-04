# ADR 0014：Qinglan Demo 固定 Tick、所有者、随机流与公共战斗契约

- 状态：Accepted
- 日期：2026-08-04
- 决策人：依据用户当前连续 Demo 开发指令
- 关联里程碑：G0.3、G1.1—G2.4
- 取代：无

## 背景

CR-2026-004—011、013、014 会增加角色机制、回返轨迹、状态事务、Reward、地图目标、Boss、
精英词缀和伤害策略。它们存在明确的 Tick 顺序、数据所有者和随机隔离依赖。若各工作包自行插入
系统或直接写其他模块状态，会产生同 Tick 竞态、重复奖励、Scene 真值和不可复现随机结果。

## 决策

### G1/G2 目标 Pipeline

在保持现有 M2—M6 构造器用于回归的同时，新增显式 Demo Pipeline：

```text
01 InputCommand                 13 CharacterMechanicReaction
02 SpawnScheduler              14 StatusTick
03 MapObjectiveAndEvent        15 Regeneration
04 BossPhase                   16 Death
05 EnemyDecision               17 LootAndReward
06 SkillTrigger                18 Pickup
07 Movement                    19 Experience
08 CharacterMechanicAccumulate 20 LevelUpRequest
09 SkillDelivery               21 Lifetime
10 SkillEffectResolution       22 Cleanup
11 DamageResolution            23 EventFlush
12 RewardResolution            24 SnapshotBuild
```

Map/Boss 消费上一已完成 Tick 的战斗事件；Damage 之后的角色反应消费当前 Tick 实际伤害。结构创建和
删除仍只由 Cleanup 应用。跨越多个 Boss 阈值时按阶段顺序执行转换，致命伤害优先进入 Death，不能
靠阶段转换复活。选择请求在 Tick 完成后由 Application 暂停下一 Tick。

### 所有者和事务

- Combat 独占生命、护盾、状态、屏障和死亡；DamageResolution 是唯一伤害生命写入者。
- CharacterMechanic 独占角色资源/档位；只消费解析后命令位移与实际伤害结果。
- Reward 独占 Run-local 奖励事务/选择；永久增量只输出不可变 `RunResultDelta` 给 Application。
- MapObjective、BossPhase、Enemy、Progression 各自独占状态；UI/Scene 只读快照。
- Reward/死亡/目标输出用 `RunId + SourceStableId + Sequence` 形成稳定事务键；同键重复提交无效果。

### 位移、状态与伤害

Movement 记录类型化 `MovementSource` 和分来源解析位移；角色机制只累计 `PlayerCommand` 经地图解析
后的有限位移，排除 Teleport、Correction、Knockback、Pull 和 Scripted。

状态查询在加载期把 ContentId/Tag 绑定为紧凑操作数；消费与引爆在一次 Effect Resolution 事务中
先验证再提交，失败不做部分消费。`TriggerPosition` 来自纯值 Trigger Context。

新增 `DamageChannelId` 与内建 `direct`、`contact`、`periodic`、`hazard`、`boss_hazard`。每 Actor
使用固定容量的按通道冷却/屏障 sidecar。顺序为：合法性/死亡 → 免疫 → 通道冷却 → 暴击/属性/抗性
→ 屏障 → 既有 Shield → Health → 事件/冷却。只有 Shield 或 Health 实际减少才发
`DamageApplied`；完全免疫、冷却拒绝、零值或仅屏障吸收不触发 OnHit/OnDamageTaken/角色降档。
另发只读 `DamageResolved` 供屏障表现和诊断。

### 随机流与容量

固定派生流为 Combat、Skill、Encounter、Offer、MapEvent、Reward；Presentation 不进入 Simulation。
新增/移除某一流的调用不得改变其他流。首次 Boss、唯一奖励、故事和脉印不 Roll。

所有 sidecar/缓冲使用构造期容量、可审计增长或稳定拒绝策略；Tick 热路径禁止 LINQ、反射、字符串
格式化和临时集合。是否迁移 Jobs/Burst 仍只由实际基准决定。

## 备选方案

### 方案 A：各 Runtime 直接互调并立即创建实体

- 优点：代码路径短。
- 缺点：遍历中结构变化、重入和顺序依赖不可验证。
- 未采用原因：破坏 Cleanup 单写入者和确定性。

### 方案 B：所有新系统统一放在 Tick 末尾

- 优点：插入简单。
- 缺点：位移、伤害、死亡、奖励会多一 Tick 或读到错误状态。
- 未采用原因：不能满足体验与事务语义。

## 影响

### 正面影响

- G1/G2 可按同一顺序独立实现并用系统序列断言。
- 实际伤害、奖励幂等和选择暂停有唯一语义。
- Scene、UI、Platform 不会成为模拟真值。

### 负面影响与成本

- `Game.Core`、`Game.Simulation` 和 `Game.Application` 公开 API 均需追加契约。
- 需要为旧 Pipeline 构造器保留回归并扩展性能 Harness。

### 对兼容性的影响

- Content Schema：使用 ADR 0013 的 Schema 6 token。
- Save Schema：运行态不持久化；永久结果使用 ADR 0015。
- API：追加 SystemId、Channel/Movement/Reward/Map/Boss/Mechanic 类型；不删除旧成员。
- 性能：增加固定遍历和 sidecar；G1.1 必须与 M10 基线对比。
- 构建：Development/Release 都必须运行 Project Validation 和目标 Pipeline Smoke。
- 资产：无 Unity Object 进入契约。

## 实施与迁移

1. G1.1 先实现公共纯值、Registry/Runtime 骨架、Demo Pipeline 和最小 Fixture。
2. 旧 `CreateM2Default`—`CreateM6Default` 保留；新增 Demo 构造器，不静默改变旧测试语义。
3. 各工作包只填充已批准 Owner，所有新结构操作接入 Cleanup。
4. 更新 API Freeze Hash 前运行完整测试、Validation、性能短测和 Development Build。

## 测试与验收证据

- 测试：24 系统顺序、同 Tick 伤害/阶段/奖励、随机流隔离、Cleanup、状态原子消费和 ID 无分支扫描。
- 构建：G1.1 Development 探针，G1.7 完整 Development，G3.6 Release。
- 性能：1 玩家机制 54,000 Tick 0 B；1,500/3,000/5,000 基线不退化超预算；新 sidecar 容量/拒绝计数可见。
- 日志或产物位置：实施后写入 `TestResults/QinglanDemo/G1.1/` 与性能 JSON。

## 回滚方案

新内容尚未发布时，可停用 Demo Composition Root 并恢复旧 M6 Pipeline；旧构造器和类型不删除。
若已发布 token，保留读取/注册兼容并仅停止新内容引用，禁止重用枚举值、Stat 索引或 Channel ID。
