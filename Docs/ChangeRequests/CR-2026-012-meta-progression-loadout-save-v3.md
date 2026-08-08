# Change Request：局外成长、Loadout 与 Profile Save Schema 3

- 编号：CR-2026-012
- 状态：Implemented
- 提交日期：2026-08-04
- 提交人：Codex
- 目标里程碑：G0.3、G2.5、G2.6、G2.7、G3.2
- 关联 ADR：ADR 0013、ADR 0015、ADR 0023；对应 Demo CR-09

## 1. 变更摘要

新增 MetaNode、Insert、Facility、Story、Collectible 定义与应用层 Meta Owner，并把 Profile Save 升至 Schema 3，以稳定 ID 保存 Loadout、首通、唯一领取和幂等事务。

## 2. 触发场景

- 用户或设计需求：Demo 需要青岚养成、装配、设施、图鉴/故事、首通奖励和重启后保持。
- 当前限制：Save Schema 2 只覆盖既有设置/进度字段，缺少可验证 Loadout 与永久事务记录。
- 可复现示例：重复结算胜利可能再次发放唯一奖励，或内容删改后 Loadout 指向无效运行时索引。

## 3. 现有模块为何不足

现有 Save Backend 和 Content Registry 可持久化稳定 ID，但没有这些定义族、聚合验证、幂等键和 v2→v3 迁移。

## 4. 提议方案

- 新增或修改的模块：Meta Progression Service、Loadout Validator、Profile Migration。
- 公共 API：读取/购买/装配/解锁/提交首通事务；Simulation 只接收已验证不可变 Loadout。
- 数据结构：Schema 6 Meta 定义；Profile Schema 3 保存稳定 ContentId、首通、唯一领取和事务键。
- 注册方式：所有定义走 Content Registry；应用层按稳定 ID 解析。
- 编辑器工作流：校验前置、成本、槽位、互斥、默认 Loadout 和本地化。

## 5. 影响分析

| 领域 | 影响 |
|---|---|
| Assembly 依赖 | Application 拥有 Meta；Simulation 不引用 Save Backend |
| Content Schema | Schema 6 新增五类 Meta 定义 |
| Save Schema | Profile 2→3，需原子迁移与回退 |
| Addressables | UI/图鉴资源使用稳定地址 |
| 性能 | 低频事务；启动时一次验证/绑定 |
| 测试 | v2→v3、损坏恢复、幂等、无效 ID、Loadout 注入 |
| 平台 | Windows/Steam backend 保持适配层隔离 |
| 资产与许可 | 用户可见正式资源需 provenance |
| 兼容性 | 必须兼容读取 Schema 2 并保留备份 |

## 6. 备选方案

把所有字段直接加入单一 Profile 类实现快，但无法数据驱动扩展或验证引用，且会保存运行时索引，因此拒绝。

## 7. 迁移与回滚

- 迁移步骤：读取 v2、生成默认 Loadout/空事务集、写临时文件并原子替换，保留备份。
- 旧数据处理：未知稳定 ID 隔离并回退到安全默认，不静默授予奖励。
- 回滚步骤：恢复 v2 备份；已提交的永久事务通过稳定幂等键避免重复。

## 8. 验收标准

- [x] 五类 Meta 定义无核心硬编码内容 ID
- [x] Profile v2→v3 原子迁移可重复
- [x] 自动测试覆盖损坏、未知 ID、幂等和默认 Loadout
- [x] 启动/保存性能满足预算
- [x] ADR、Schema、API Freeze 已更新
- [x] Schema 2 Fixture 兼容通过

## 9. 审批

- 技术负责人：依据用户当前连续 Demo 开发指令批准进入 G0.3 契约设计
- 内容负责人：依据已提交 V2.0 与 G0.1 数量/体验基线
- 制作人：依据用户当前连续 Demo 开发指令
- 结论：Accepted；G1.1 完成 Profile 3 Codec/Migration，G2.5 完成 Meta Owner 与原子结算

## 10. G2.5 实施状态

- 12 个 MetaNode、3 个 Insert、4 个 Facility、3 个 Story、6 个 Collectible 与 15 个通用 Trait 输出：
  `IMPLEMENTED`。
- 6 Branch＋1 Terminal＋2 Insert、前置/互斥、灵砂购买、免费重置和缺失 ID 安全投影：`IMPLEMENTED`。
- Profile 单一 Owner、Victory/Failure 过滤、事务幂等、保存与 Recovery 双失败重试：`IMPLEMENTED`。
- Profile v1→2→3/v2→3 Fixture、内容验证、API Freeze、性能短测和 Windows Development Build：
  `PASS`。
- 实际据点/故事/收藏页面与确认式缺失 ID 修复入口属于 G2.6；正式表现属于 G3，不影响本 CR 的
  数据与持久化验收关闭。
