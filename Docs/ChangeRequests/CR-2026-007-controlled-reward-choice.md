# Change Request：关键掉落与受控 Evolution 选择

- 编号：CR-2026-007
- 状态：Approved
- 提交日期：2026-08-04
- 提交人：Codex
- 目标里程碑：G0.3、G1.7、G2.4
- 关联 ADR：ADR 0013、ADR 0014；对应 Demo CR-04

## 1. 变更摘要

新增独立于升级 Offer 的 Reward Choice Context，按当前 BuildState 计算 Evolution 资格，并以可重放事务完成受控候选、选择、回退和历史记录。

## 2. 触发场景

- 用户或设计需求：Boss/精英关键掉落只提供当前满足条件的进化选择。
- 当前限制：现有 Level-up Offer 流没有掉落来源、资格快照、唯一领取和奖励事务语义。
- 可复现示例：把进化塞入普通升级会被刷新或跳过，且无法证明掉落只结算一次。

## 3. 现有模块为何不足

Synergy/Evolution 可描述结果，Progression Offer 可提供候选，但两者缺少由奖励触发的独立暂停上下文和资格事务。

## 4. 提议方案

- 新增或修改的模块：Reward Choice Runtime、Evolution Eligibility Adapter。
- 公共 API：奖励来源、BuildState 快照、候选列表、确定性选择/回退和提交结果。
- 数据结构：Reward Context 保存稳定来源 ID、候选稳定 ID、事务号和状态。
- 注册方式：由通用 Reward 操作触发；Evolution 定义仍来自内容注册表。
- 编辑器工作流：验证资格条件、互斥关系、空候选回退和本地化 Key。

## 5. 影响分析

| 领域 | 影响 |
|---|---|
| Assembly 依赖 | Application/UI 读取 Simulation 只读上下文 |
| Content Schema | Schema 6 增加奖励选择规则 |
| Save Schema | 运行中不持久化；永久唯一领取由 CR-2026-012 处理 |
| Addressables | UI 使用现有本地化与程序化占位 |
| 性能 | 仅奖励触发时计算候选；不进入固定 Tick 热路径 |
| 测试 | 资格、暂停、一次提交、空候选回退、确定性 |
| 平台 | 无影响 |
| 资产与许可 | 无外部资产 |
| 兼容性 | Level-up Offer 行为保持不变 |

## 6. 备选方案

复用升级 Offer 可减少 API，但会混淆随机流、刷新规则和奖励幂等边界，因此不采用。

## 7. 迁移与回滚

- 迁移步骤：在 Reward 操作和 Evolution 资格之间增加适配器；先保留原升级流。
- 旧数据处理：旧 Evolution 定义按缺省奖励上下文参与资格计算。
- 回滚步骤：移除掉落触发配置，恢复仅升级选择；不改写旧存档。

## 8. 验收标准

- [ ] 至少 Boss 与精英奖励两个消费者
- [ ] 候选由 BuildState 计算且一次提交
- [ ] 自动测试覆盖无资格、互斥、回退和重放
- [ ] 选择期间 Simulation 暂停且无重复奖励
- [ ] ADR、Schema、API Freeze 已更新
- [ ] 普通升级 Offer 回归通过

## 9. 审批

- 技术负责人：依据用户当前连续 Demo 开发指令批准进入 G0.3 契约设计
- 内容负责人：依据已提交 V2.0 与 G0.1 数量/体验基线
- 制作人：依据用户当前连续 Demo 开发指令
- 结论：Accepted / Approved for G0.3 design；尚未实现
