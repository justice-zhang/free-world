# Change Request：回返与多阶段轨迹 Delivery

- 编号：CR-2026-005
- 状态：Approved
- 提交日期：2026-08-04
- 提交人：Codex
- 目标里程碑：G0.3、G1.3
- 关联 ADR：待 G0.3；对应 Demo CR-02

## 1. 变更摘要

增加可注册的出发—转向—回返轨迹 Delivery，使不同技能可复用阶段速度、阶段效果与单阶段命中去重，同时保持投射物生命周期由 Simulation/Cleanup 管理。

## 2. 触发场景

- 用户或设计需求：回旋镖类技能往返飞行并在不同阶段命中目标。
- 当前限制：现有直线/瞬时 Delivery 没有阶段状态、回返所有者和阶段命中集合。
- 可复现示例：同一目标在同一阶段的多个 Tick 被重复命中，或所有者失效后投射物悬空。

## 3. 现有模块为何不足

现有 Delivery 可组合伤害 Effect，却无法表达相位转换和阶段去重；在具体技能中维护状态会形成一次性系统。

## 4. 提议方案

- 新增或修改的模块：注册 `base.delivery.outbound_return`，扩展 Projectile sidecar。
- 公共 API：轨迹相位、相位转换条件、每相位命中策略和所有者失效策略。
- 数据结构：Delivery 参数描述出发距离/时长、转向、回返速度及各阶段输出。
- 注册方式：沿用 Skill Module Registry；稳定 wire token 显式登记。
- 编辑器工作流：预览各阶段路径并验证速度、时长和去重容量。

## 5. 影响分析

| 领域 | 影响 |
|---|---|
| Assembly 依赖 | 仅扩展 `Game.Simulation` Skill Runtime |
| Content Schema | Schema 6 模块参数；不新增顶级技能 kind |
| Save Schema | 不保存飞行中投射物 |
| Addressables | 无变化 |
| 性能 | 固定容量紧凑 sidecar；无每 Tick 集合分配 |
| 测试 | 相位转换、去重、所有者失效、Cleanup、确定性 |
| 平台 | 无影响 |
| 资产与许可 | 程序化轨迹占位 |
| 兼容性 | 原 Delivery token 与语义不变 |

## 6. 备选方案

拆为两个独立技能会丢失同一投射物身份和统一去重语义，且难以处理提前回收，因此不采用。

## 7. 迁移与回滚

- 迁移步骤：注册新 token 并更新 API Freeze；旧技能无需迁移。
- 旧数据处理：旧 Schema 不认识该 token 时仍按既有验证规则拒绝非法引用。
- 回滚步骤：删除使用该 Delivery 的新内容后移除注册项。

## 8. 验收标准

- [ ] 至少两个技能 Fixture 共用 Delivery
- [ ] 每相位命中语义和所有者失效行为明确
- [ ] EditMode/PlayMode 覆盖完整往返生命周期
- [ ] 目标规模下无新增热路径 GC
- [ ] ADR、Schema、API Freeze 已更新
- [ ] 原有 Delivery 回归通过

## 9. 审批

- 技术负责人：依据用户当前连续 Demo 开发指令批准进入 G0.3 契约设计
- 内容负责人：依据已提交 V2.0 与 G0.1 数量/体验基线
- 制作人：依据用户当前连续 Demo 开发指令
- 结论：Accepted / Approved for G0.3 design；尚未实现
