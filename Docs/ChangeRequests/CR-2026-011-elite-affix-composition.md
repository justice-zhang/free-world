# Change Request：精英词缀组合

- 编号：CR-2026-011
- 状态：Approved
- 提交日期：2026-08-04
- 提交人：Codex
- 目标里程碑：G0.3、G1.5、G2.3
- 关联 ADR：ADR 0013、ADR 0014、ADR 0016；对应 Demo CR-08

## 1. 变更摘要

新增 Elite Affix 定义、兼容标签和确定性组合器，通过属性修正、附加技能、死亡输出、奖励倍率与表现 Profile 组合精英，而不复制 Enemy 定义。

## 2. 触发场景

- 用户或设计需求：普通敌人可获得多种精英词缀并产生合法组合与额外奖励。
- 当前限制：现有 Enemy Archetype 没有可组合词缀、冲突约束和组合随机选择。
- 可复现示例：复制三份敌人只为表现不同词缀会造成平衡数据漂移。

## 3. 现有模块为何不足

Stat Modifier、Skill 和 Reward 可表达词缀输出，但缺少把它们绑定到敌人的通用定义、兼容规则和选择所有者。

## 4. 提议方案

- 新增或修改的模块：Elite Affix Registry 与 Composition Runtime。
- 公共 API：兼容标签查询、合法组合结果、附加技能/输出和只读表现 Profile。
- 数据结构：Schema 6 `EliteAffixDefinition`，含 required/excluded tags、修正、技能、死亡输出与奖励倍率。
- 注册方式：稳定 ContentId 注册；由 Encounter RNG 确定性选择合法组合。
- 编辑器工作流：验证冲突、循环引用、最大组合数、技能引用和本地化。

## 5. 影响分析

| 领域 | 影响 |
|---|---|
| Assembly 依赖 | Simulation 组合现有 Enemy/Skill/Reward 能力 |
| Content Schema | Schema 6 新增 Elite Affix 定义 |
| Save Schema | 无影响 |
| Addressables | 表现 Profile 使用稳定地址 |
| 性能 | Spawn 时组合，Tick 时使用绑定结果 |
| 测试 | 合法组合、冲突、随机确定性、奖励与死亡输出 |
| 平台 | 无影响 |
| 资产与许可 | 程序化轮廓/颜色占位 |
| 兼容性 | 无词缀 Enemy 路径不变 |

## 6. 备选方案

复制 Enemy Archetype 不需要新 Schema，但组合爆炸、维护成本高且违反内容扩展规则，因此拒绝。

## 7. 迁移与回滚

- 迁移步骤：新增定义族；Encounter 配置可选词缀池。
- 旧数据处理：旧 Encounter 默认空词缀池。
- 回滚步骤：清空词缀池后移除定义与组合器。

## 8. 验收标准

- [ ] 至少三种 Enemy 与多种词缀共享组合器
- [ ] 不复制 Enemy 定义表达词缀
- [ ] 自动测试覆盖冲突、无解、确定性和奖励
- [ ] Spawn/目标规模性能满足预算
- [ ] ADR、Schema、API Freeze 已更新
- [ ] 旧 Encounter 回归通过

## 9. 审批

- 技术负责人：依据用户当前连续 Demo 开发指令批准进入 G0.3 契约设计
- 内容负责人：依据已提交 V2.0 与 G0.1 数量/体验基线
- 制作人：依据用户当前连续 Demo 开发指令
- 结论：Accepted / Approved；G1.5 已实现组合/绑定/有限分裂，G2.3 继续实现完整奖励选择
