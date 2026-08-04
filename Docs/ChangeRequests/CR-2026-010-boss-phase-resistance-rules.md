# Change Request：Boss 阶段、抗性与地图规则

- 编号：CR-2026-010
- 状态：Approved
- 提交日期：2026-08-04
- 提交人：Codex
- 目标里程碑：G0.3、G2.4
- 关联 ADR：ADR 0013、ADR 0014；对应 Demo CR-07

## 1. 变更摘要

在 Enemy/Actor 模型上增加通用 Boss Definition、阶段状态机、抗性、清场策略、地图规则输入和奖励输出，使 Boss 仍由同一伤害与技能管线驱动。

## 2. 触发场景

- 用户或设计需求：Boss 按生命或事件切换技能组、抗性和地图修正，并在结束时产生关键奖励。
- 当前限制：现有 Enemy Archetype 没有阶段进入条件、阶段技能集和阶段清理语义。
- 可复现示例：把阶段逻辑写在 Boss Prefab 会导致无头模拟与场景表现分叉。

## 3. 现有模块为何不足

Enemy、Skill、Status 和 Map Rule 可提供执行原语，但没有统一阶段所有者与确定性转换顺序。复制特殊敌人会破坏内容扩展规则。

## 4. 提议方案

- 新增或修改的模块：Boss Runtime、Enemy Runtime 与 Map Rule Adapter。
- 公共 API：阶段条件/快照、阶段技能集、抗性配置、进入/退出输出和奖励规则。
- 数据结构：Schema 6 `BossDefinition` 与阶段定义，Boss 仍引用通用 Enemy Archetype。
- 注册方式：按稳定 Boss ContentId 注册，技能/状态/规则在加载期绑定。
- 编辑器工作流：校验阶段可达、条件顺序、清理策略、奖励唯一性和预警时长。

## 5. 影响分析

| 领域 | 影响 |
|---|---|
| Assembly 依赖 | Simulation 扩展；UI/Presentation 只读投影 |
| Content Schema | Schema 6 新增 Boss 与 Phase 定义 |
| Save Schema | Demo 不恢复进行中 Boss |
| Addressables | 表现资源通过稳定地址绑定 |
| 性能 | 每 Boss 固定小状态；阶段系统位于 Enemy/Skill 前 |
| 测试 | 阶段顺序、抗性、清场、地图规则、奖励幂等 |
| 平台 | 无影响 |
| 资产与许可 | Placeholder 优先；正式资产需 provenance |
| 兼容性 | 普通 Enemy 路径不变 |

## 6. 备选方案

为每个 Boss 写专用 Controller 较直接，但不能复用阶段、抗性和地图规则，也无法保持无头一致性，因此拒绝。

## 7. 迁移与回滚

- 迁移步骤：新增可选 Boss Definition；旧 Enemy 不绑定即沿用原行为。
- 旧数据处理：Schema 1—5 Enemy 无 Boss 语义。
- 回滚步骤：移除 Boss 内容和阶段系统，普通 Enemy 数据无需迁移。

## 8. 验收标准

- [ ] 两个 Boss Fixture 复用阶段 Runtime
- [ ] Boss 仍使用统一 Damage/Skill/Status 管线
- [ ] 自动测试覆盖同 Tick 条件优先级和奖励一次性
- [ ] 阶段切换不产生热路径分配
- [ ] ADR、Schema、API Freeze 已更新
- [ ] 普通 Enemy 回归通过

## 9. 审批

- 技术负责人：依据用户当前连续 Demo 开发指令批准进入 G0.3 契约设计
- 内容负责人：依据已提交 V2.0 与 G0.1 数量/体验基线
- 制作人：依据用户当前连续 Demo 开发指令
- 结论：Accepted / Approved for G0.3 design；尚未实现
