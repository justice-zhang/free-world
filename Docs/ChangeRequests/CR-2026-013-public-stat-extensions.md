# Change Request：公共 Stat 扩展

- 编号：CR-2026-013
- 状态：Approved
- 提交日期：2026-08-04
- 提交人：Codex
- 目标里程碑：G0.3、G1.2、G1.3、G1.5、G2.3
- 关联 ADR：待 G0.3；对应 Demo CR-10A（由 CR-10 拆分）

## 1. 变更摘要

向冻结公共 StatCatalog 增加经消费者证明的 ProjectileSpeed、CriticalMultiplier、ExperienceGain 和 KnockbackResistance，使技能、成长和敌人修正共享同一属性管线。

## 2. 触发场景

- 用户或设计需求：技能投射速度、暴击倍率、经验增益和抗击退需要被角色、被动、词缀与难度统一修改。
- 当前限制：现有公共属性集合缺少这些维度，临时参数无法参与统一 Modifier 叠加。
- 可复现示例：同一投射速度加成分别写进两个 Delivery 后叠加顺序不一致。

## 3. 现有模块为何不足

Stat Modifier 管线足够，但 StatId/StatCatalog 没有稳定条目；在具体模块中添加旁路字段会形成互不兼容的属性系统。

## 4. 提议方案

- 新增或修改的模块：`Game.Core` StatId/StatCatalog 和对应绑定表。
- 公共 API：四个稳定 Stat 条目、默认值、范围和叠加顺序。
- 数据结构：Schema 6 接受这些 wire token；既有 Modifier 结构不变。
- 注册方式：内建稳定索引只追加，不重排现有条目。
- 编辑器工作流：Stat 引用校验与消费者覆盖报告。

## 5. 影响分析

| 领域 | 影响 |
|---|---|
| Assembly 依赖 | 修改冻结 `Game.Core` 公共 API，不改变依赖方向 |
| Content Schema | Schema 6 增加合法 Stat token |
| Save Schema | 不保存 Stat 运行值 |
| Addressables | 无影响 |
| 性能 | 只扩展固定数组；不得引入字典热查找 |
| 测试 | 索引稳定、默认值、叠加、每个消费者、旧 Hash |
| 平台 | 无影响 |
| 资产与许可 | 无影响 |
| 兼容性 | 旧 Stat 索引和语义绝不重排 |

## 6. 备选方案

使用通用浮点参数可避免公共 API 变化，但无法被 Trait/Status/Meta 统一修正和审计，因此拒绝。

## 7. 迁移与回滚

- 迁移步骤：ADR 批准后只追加条目，更新 API Freeze Hash 和 Schema 文档。
- 旧数据处理：旧 Modifier/Pack 无新 token 时结果逐位不变。
- 回滚步骤：先移除全部新 token 引用，再撤回追加条目；不得重用其索引。

## 8. 验收标准

- [ ] 每个 Stat 至少两个独立消费者或明确跨模块消费者
- [ ] 现有索引和 wire token 不变
- [ ] 自动测试覆盖默认值、边界和叠加顺序
- [ ] 固定数组扩展无热路径分配
- [ ] ADR、Schema、API Freeze 已更新
- [ ] 旧 Modifier Golden Fixture 不变

## 9. 审批

- 技术负责人：依据用户当前连续 Demo 开发指令批准进入 G0.3 契约设计
- 内容负责人：依据已提交 V2.0 与 G0.1 数量/体验基线
- 制作人：依据用户当前连续 Demo 开发指令
- 结论：Accepted / Approved for G0.3 design；尚未实现
