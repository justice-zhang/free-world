# Change Request：泛化 Reward、Pickup 与 Relic

- 编号：CR-2026-008
- 状态：Approved
- 提交日期：2026-08-04
- 提交人：Codex
- 目标里程碑：G0.3、G1.7、G2.2、G2.3、G2.4、G2.5
- 关联 ADR：ADR 0013、ADR 0014、ADR 0021；对应 Demo CR-05

## 1. 变更摘要

建立数据驱动 Reward/Pickup/Relic 定义、操作码、独立随机流和幂等结算，使治疗、范围伤害、吸附、货币、遗物、进化、解锁和剧情可由多个来源组合。

## 2. 触发场景

- 用户或设计需求：普通掉落、宝箱、事件、精英和 Boss 产生不同即时或选择型奖励。
- 当前限制：现有 XP/拾取逻辑不能表达唯一奖励、多操作组合、选择上下文和永久输出。
- 可复现示例：同一宝箱事件重放时可能重复发放货币或解锁。

## 3. 现有模块为何不足

Effect 适合战斗效果，Progression 适合升级，但缺少通用奖励所有者、独立随机流、拾取生命周期与幂等事务号。

## 4. 提议方案

- 新增或修改的模块：Reward Runtime、Pickup Runtime、Relic Runtime。
- 公共 API：`Heal`、`ApplyStatus`、`DamageArea`、`CollectEligiblePickups`、`GrantRelicChoice`、`GrantEvolutionChoice`、`AddCurrency`、`UnlockContent`、`GrantUnique`、`TriggerStory` 操作。
- 数据结构：Schema 6 Reward/Pickup/Relic 定义；运行时固定容量 sidecar 与事务记录。
- 注册方式：稳定操作码显式注册，Reward RNG 与其他流隔离。
- 编辑器工作流：验证操作参数、唯一性、来源、回退和本地化。

## 5. 影响分析

| 领域 | 影响 |
|---|---|
| Assembly 依赖 | Simulation 产生命令；Application 处理永久输出 |
| Content Schema | Schema 6 新增三个定义族 |
| Save Schema | 永久输出依赖 Profile v3（CR-2026-012） |
| Addressables | 沿用 Content Pack 和稳定地址 |
| 性能 | 拾取使用批量系统和固定容量数据；结构变更交 Cleanup |
| 测试 | 操作码、随机流隔离、幂等、满容量、Cleanup |
| 平台 | 无影响 |
| 资产与许可 | 仅程序化 Placeholder，正式资源另走 provenance |
| 兼容性 | 旧 XP Pickup 通过兼容适配保持语义 |

## 6. 备选方案

按掉落类型分别写系统会重复生命周期、随机和幂等逻辑，无法长期扩展，因此不采用。

## 7. 迁移与回滚

- 迁移步骤：先增加定义/操作注册，再把旧 XP Pickup 映射到兼容 Reward。
- 旧数据处理：Schema 1—5 仍使用兼容默认定义。
- 回滚步骤：停止新内容引用并恢复旧适配入口；永久事务不回退已授予结果。

## 8. 验收标准

- [ ] 至少三类来源共享 Reward Runtime
- [ ] 所有结构创建/删除经 Cleanup
- [ ] 自动测试覆盖幂等、随机隔离、容量和失败回退
- [ ] 目标规模 Pickup 热路径满足预算
- [ ] ADR、Schema、API Freeze 已更新
- [ ] 旧 XP 拾取回归通过

## 9. 审批

- 技术负责人：依据用户当前连续 Demo 开发指令批准进入 G0.3 契约设计
- 内容负责人：依据已提交 V2.0 与 G0.1 数量/体验基线
- 制作人：依据用户当前连续 Demo 开发指令
- 结论：Accepted / Approved for G0.3 design；尚未实现

## 10. G1.7 分期实施状态

- 复用既有 `RewardRuntime` 的事务容量、提交和重放检查作为受控 Evolution Choice 的幂等所有者：
  `IMPLEMENTED`。
- Reward 独立随机流、空候选 fallback ID 记录和 Application 暂停/投影：`IMPLEMENTED`。
- Reward/Pickup/Relic Definition 与操作码、三类实际来源、fallback 操作执行、永久输出和拾取热路径：
  G2.3 已完成普通敌人/精英/Boss/Map 来源、六 Pickup、六 Relic、显化/fallback、三槽选择、局内永久
  增量和 5,000 Pickup 0 B 专项；Profile v3 原子持久化仍由 G2.5 完成。

## 11. G2.3 分期实施状态

- 三类以上来源共享 RewardRuntime、结构创建/删除经 Cleanup：`IMPLEMENTED`。
- 活动/已提交事务幂等、Reward RNG 与 Offer RNG 隔离、空池/满槽 fallback：`IMPLEMENTED`。
- 六种即时操作、六 Relic 输出、唯一所有权快照和 Application 暂停/恢复：`IMPLEMENTED`。
- 5,000 Reward Pickup 连续扫描 0 B、无容量增长/拒绝：`IMPLEMENTED`。
- Profile v3 原子合并、保存重试和平台事件：`NOT RUN`，目标 G2.5；因此 CR 的持久化验收尚未关闭。
