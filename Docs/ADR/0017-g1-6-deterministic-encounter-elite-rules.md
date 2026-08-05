# ADR 0017：G1.6 确定性 Encounter 一次性精英规则与停止边界

- 状态：Accepted
- 日期：2026-08-05
- 决策人：依据用户当前连续 Demo 开发指令
- 关联里程碑：G1.6、G2.2
- 关联 CR：CR-2026-016

## 背景

M09 的 12 分钟骨架要求在 3:00 与 7:30 各触发一次精英，并在 12:00 停止普通生成。现有 Entry 的
`Elite` 是每组属性，随机 EliteProbability 是概率属性，均不能表达一次性时点；BossRule 又会污染 Boss
身份。短 Phase 技巧还会受跨 Phase SpawnCooldown 影响，不能作为稳定内容契约。

## 决策

### 一次性 EliteRule

Schema 6 Phase 可选携带 `RuntimeEncounterEliteRule[]`。每条规则保存 EnemyId、绝对 SpawnTime、Pattern、
可选 AnchorId 和 canonical AffixPoolIds；时间必须落在所属 Phase，引用必须解析为可执行 Enemy 与
EliteAffix。旧构造函数和旧 DTO 缺失字段均映射为空。

Scheduler 对当前 Phase 的未触发 EliteRule 与 BossRule 分别保留一次性标记。每 Tick 顺序为固定精英、
Boss、普通组；未触发的一次性规则共同占用保留槽，普通组不能挤掉它们。容量暂满时规则保持未触发并
在所属 Phase 内重试。精英请求 `Elite=true/Boss=false`，复用 ADR 0016 的 Affix 组合和 Encounter RNG。

### 12:00 停止边界

Encounter 最后 Phase 精确结束于 720 秒；结束后不再选择 Phase，`AccumulatedBudget` 与 SpawnCooldown
立即归零，不允许积压预算在后续恢复。结构创建仍只由 Cleanup 执行。

### G1.6 与 G2.2 边界

G1.6 只创建 0—720 秒普通敌人和两条 EliteRule。6:00 折枝、12:00 听风的 Enemy/BossDefinition 与
BossRule 由 G2.2 在同一 Encounter 上追加；G1.6 的报告必须把“两 Boss 一次”列为 `NOT RUN`，不能用
普通敌人或精英伪装 Boss。

## 兼容与影响

- Assembly 方向、Simulation Tick、Save Schema 与稳定 ID 规则不变。
- 只向 `Game.Content.Runtime` 追加类型/构造重载/只读属性；旧成员不删除。
- EliteRules 非空时才追加确定性 Hash；Schema 1—5 DTO 不读取该字段，既有 Fixture Hash 不变。
- Scheduler 只扫描当前 Phase 的固定小数组；不引入 LINQ、反射、字符串查找或 Tick 临时集合。
- 随机调用只发生在规则实际排队时，使用 Encounter 流；其他派生流不受影响。

## 被拒绝的方案

- 短 Phase/权重技巧：不能稳定保证一次性和固定时点。
- 复用 BossRule：错误标记 Boss，破坏后续阶段与奖励所有权。
- ContentId 特判：不可复用且违反内容扩展规则。
- 由 Editor/测试注入：运行时 Player 不会得到相同行为。

## 测试与迁移

1. 先保留旧 API Hash 并导出规范 diff；只允许本 ADR 的 Content Runtime 追加。
2. 覆盖 DTO/Hash/Validation、旧构造回归、两时点一次触发、容量预留和 720 秒清零。
3. 同 Seed 双实例运行 21,600 Tick并比较 Spawn/Death/Elite/世界 Checksum。
4. 运行完整 EditMode、PlayMode、Project Validation、API Freeze 与 G1.6 性能短测。
