# Change Request：Encounter 一次性精英规则

- 编号：CR-2026-016
- 状态：Approved
- 提交日期：2026-08-05
- 提交人：Codex
- 目标里程碑：G1.6
- 关联 ADR：ADR 0007、0013、0014、0016、0017

## 1. 变更摘要

为 Schema 6 Encounter Phase 增加可选的一次性 `EliteRules[]`，在固定时间用 Encounter RNG 绑定合法
Affix Pool，并为尚未触发的精英预留并发槽。旧 Schema 1—5 和没有 EliteRule 的 Encounter 行为不变。

## 2. 触发场景

- M09 要求 3:00 与 7:30 各生成一次精英，固定 Seed 下不得重复或漏触发。
- 当前 `EncounterEnemyEntry.Elite` 会把该条目的每个普通组都变成 Elite；Difficulty EliteProbability
  是随机概率，两者都不能表达“固定时点、一次性”。

## 3. 现有模块为何不足

用极短 Phase 模拟一次性精英仍会继承上一 Phase 的 SpawnCooldown，并受普通预算、权重和并发占用影响；
结果可能漏触发或重复，且把业务事件隐含在曲线技巧中。BossRule 会把 Actor 标为 Boss，也不能复用。

## 4. 提议方案

- 新增 `RuntimeEncounterEliteRule`：EnemyId、SpawnTime、Pattern、AnchorId、AffixPoolIds。
- `RuntimeEncounterPhase` 追加可选 EliteRules；旧构造函数保留并映射为空。
- Scheduler 为未触发 Elite/Boss 预留槽，先排一次性规则、再排普通组；精英不是 Boss。
- Rule 始终走 G1.5 的通用 Affix 组合器和 Encounter RNG；不按青岚 ContentId 分支。
- 12:00 后 Scheduler 清空普通预算和冷却，不消费积压预算。

## 5. 影响分析

| 领域 | 影响 |
|---|---|
| Assembly 依赖 | 不变 |
| Content Schema | Schema 6 Encounter 可选字段追加；Schema 1—5 忽略 |
| Save Schema | 无影响 |
| 公共 API | Content Runtime 仅追加 EliteRule 类型、Phase 重载和只读属性 |
| 性能 | 每 Phase 固定小数组；每 Tick 只扫描当前 Phase 的一次性规则 |
| 随机 | 只使用 Encounter 流，不改变 Combat/Skill/Offer/Reward |
| 测试 | 3:00/7:30 精确一次、并发预留、停止生成、旧 Hash/回归 |

## 6. 备选方案

- 极短 Phase：不能保证 SpawnCooldown/容量条件，拒绝。
- BossRule 伪装精英：污染 Boss 计数、标签和后续 Boss Runtime，拒绝。
- 在测试 Harness 直接插入精英：不验证实际内容和 Scheduler，拒绝。

## 7. 迁移与回滚

- Schema 6 DTO 缺失 `elites` 时读取为空；只在数组非空时追加 Hash token，旧 Catalog Hash 不变。
- 回滚先清空 Encounter EliteRules；保留已发布 DTO 字段和公共 token，不复用语义。

## 8. 验收标准

- [x] 两个固定时点精英各触发一次且使用合法 Affix Pool
- [x] 相同 Seed 双实例 21,600 Tick Checksum 一致
- [x] 未触发精英获得并发槽，普通组不能挤占
- [x] 12:00 后普通 Spawn 为 0，积压预算为 0
- [x] 旧 Encounter 构造、Schema 1—5 Hash 与测试不变
- [x] API Freeze、Validation、性能短测通过

## 9. 审批

- 技术负责人：依据用户授权 Codex 自主决定并持续完成 Demo 的明确指令
- 内容负责人：依据 M09 固定精英时间点与 M07 通用 Affix 约束
- 制作人：依据用户当前连续开发指令
- 结论：Accepted / Approved；仅授权通用一次性 EliteRule，不提前实现 G2.2 Boss 内容
