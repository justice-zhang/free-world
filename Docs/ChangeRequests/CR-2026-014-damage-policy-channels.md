# Change Request：伤害通道、屏障与受击冷却策略

- 编号：CR-2026-014
- 状态：Approved
- 提交日期：2026-08-04
- 提交人：Codex
- 目标里程碑：G0.3、G1.6、G2.3、G2.4
- 关联 ADR：ADR 0013、ADR 0014；对应 Demo CR-10B（由 CR-10 拆分）

## 1. 变更摘要

扩展 DamagePacket/Resolution 契约，引入稳定 DamageChannel、按目标/通道受击冷却、免疫/屏障策略和实际伤害事件语义，同时保持 DamageResolution 为唯一生命写入者。

## 2. 触发场景

- 用户或设计需求：接触伤害防止逐 Tick 连续扣血，Boss 预警与屏障按不同通道处理，受伤资源只响应实际伤害。
- 当前限制：现有伤害包无法区分来源通道、屏障吸收和冷却键，也未明确零伤害是否触发受伤事件。
- 可复现示例：免疫命中仍触发受伤资源损失，或两个独立攻击错误共享全局无敌帧。

## 3. 现有模块为何不足

Status 和 Effect 可修改伤害值，但缺少 DamageResolution 内的统一策略与稳定冷却身份；外围拦截会造成多个生命写入者。

## 4. 提议方案

- 新增或修改的模块：Damage Packet、Damage Resolution、Damage Cooldown/Barrier sidecar。
- 公共 API：稳定 DamageChannel、冷却策略、吸收结果和 `ActualDamageApplied` 事件。
- 数据结构：Schema 6 伤害模块参数引用通道/策略；运行时固定容量冷却记录。
- 注册方式：内建通道显式注册，内容引用在加载期绑定。
- 编辑器工作流：验证通道、冷却范围、屏障优先级和免疫组合。

## 5. 影响分析

| 领域 | 影响 |
|---|---|
| Assembly 依赖 | 修改 Simulation 公共战斗契约；依赖方向不变 |
| Content Schema | Schema 6 增加通道和策略引用 |
| Save Schema | 不保存战斗冷却/屏障运行态 |
| Addressables | 无影响 |
| 性能 | 每 Actor 固定容量记录；Resolution 单点写入 |
| 测试 | 通道隔离、冷却、屏障、免疫、零伤害、事件顺序 |
| 平台 | 无影响 |
| 资产与许可 | 无影响 |
| 兼容性 | 旧伤害映射默认通道且结果保持一致 |

## 6. 备选方案

在每个 Enemy/Skill 中保存接触冷却会重复策略并绕过统一伤害审计，因此拒绝。

## 7. 迁移与回滚

- 迁移步骤：旧 DamagePacket 适配到默认通道/无额外冷却；逐步启用新策略。
- 旧数据处理：Schema 1—5 伤害语义通过兼容默认值保持。
- 回滚步骤：停用新策略内容并使用默认适配；先保留追加 token 避免读取失败。

## 8. 验收标准

- [ ] 接触、技能和 Boss 至少三个消费者共享策略
- [ ] DamageResolution 仍是唯一生命写入者
- [ ] 自动测试覆盖免疫/屏障/冷却/零伤害事件
- [ ] 目标敌人数下固定容量与性能门禁通过
- [ ] ADR、Schema、API Freeze 已更新
- [ ] 旧伤害 Golden Fixture 不变

## 9. 审批

- 技术负责人：依据用户当前连续 Demo 开发指令批准进入 G0.3 契约设计
- 内容负责人：依据已提交 V2.0 与 G0.1 数量/体验基线
- 制作人：依据用户当前连续 Demo 开发指令
- 结论：Accepted / Approved for G0.3 design；尚未实现
