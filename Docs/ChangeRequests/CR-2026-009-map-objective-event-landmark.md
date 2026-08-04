# Change Request：地图目标、事件与地标 Runtime

- 编号：CR-2026-009
- 状态：Approved
- 提交日期：2026-08-04
- 提交人：Codex
- 目标里程碑：G0.3、G2.1、G2.2、G2.5
- 关联 ADR：ADR 0013、ADR 0014；对应 Demo CR-06

## 1. 变更摘要

新增纯模拟 Objective/Event/Landmark 定义和状态机，用稳定锚点、规则 ID 与 MapEvent 随机流表达生存目标、护送/占点、交互事件和地图输出，不把 Scene 当作真相源。

## 2. 触发场景

- 用户或设计需求：Demo 地图需要阶段目标、交互地标、事件和完成奖励。
- 当前限制：Encounter 只负责刷怪节奏，Scene 物体不能作为确定性目标状态所有者。
- 可复现示例：重载场景或无头运行时无法复现地标激活和目标进度。

## 3. 现有模块为何不足

Encounter、Trigger 和 Reward 可以承担输入/输出，但没有通用地图状态机、稳定锚点和目标生命周期。

## 4. 提议方案

- 新增或修改的模块：Map Runtime 中的 Objective、Event、Landmark 子系统。
- 公共 API：目标状态/进度快照、交互命令、规则输出和只读地标状态。
- 数据结构：Schema 6 Objective/Event/Landmark 定义、稳定 anchor ID 和状态转换。
- 注册方式：Content Registry 注册定义；MapEvent 使用独立随机流。
- 编辑器工作流：地图 Bake 校验锚点唯一、状态可达、输出规则和本地化。

## 5. 影响分析

| 领域 | 影响 |
|---|---|
| Assembly 依赖 | Simulation 为所有者；Presentation 仅投影 Scene 表现 |
| Content Schema | Schema 6 新增三个地图定义族 |
| Save Schema | Demo 不保存运行中目标 |
| Addressables | 地标视觉地址与逻辑 anchor 解耦 |
| 性能 | 固定数量状态机；非每敌人 Update |
| 测试 | 状态转换、锚点、事件 RNG、无头/场景一致性 |
| 平台 | 无影响 |
| 资产与许可 | 程序化地标占位 |
| 兼容性 | 旧 Map/Encounter 以空目标集合读取 |

## 6. 备选方案

用 Scene MonoBehaviour 管理每个目标实现较快，但无法无头验证、存档迁移或稳定复现，因此拒绝。

## 7. 迁移与回滚

- 迁移步骤：Schema 6 为旧地图提供空集合；逐个新地图绑定稳定锚点。
- 旧数据处理：旧 Encounter 行为不变。
- 回滚步骤：移除新地图定义和 Presentation 投影；旧场景可继续加载。

## 8. 验收标准

- [ ] 至少两种 Objective 与两种 Event 共享状态机
- [ ] Scene 不是逻辑真相源
- [ ] 自动测试覆盖状态可达、失败、重入和随机隔离
- [ ] 目标规模状态机满足预算
- [ ] ADR、Schema、API Freeze 已更新
- [ ] 旧 Encounter Golden Fixture 不变

## 9. 审批

- 技术负责人：依据用户当前连续 Demo 开发指令批准进入 G0.3 契约设计
- 内容负责人：依据已提交 V2.0 与 G0.1 数量/体验基线
- 制作人：依据用户当前连续 Demo 开发指令
- 结论：Accepted / Approved for G0.3 design；尚未实现
