# Change Request：M6 构筑与升级 Schema 5

- 编号：CR-2026-001
- 状态：Implemented
- 提交日期：2026-07-26
- 提交人：Codex
- 目标里程碑：M6
- 关联 ADR：ADR 0008

## 1. 变更摘要

增加通用 Passive、Trait、Offer、Synergy 和 Evolution 内容定义及对应运行时，使 M5 奖励能
形成可配置、可复现的局内成长，而不为某个角色、技能或地图硬编码构筑。

## 2. 触发场景

- 用户或设计需求：经验升级、库存、加权候选、联动、进化、暂停选择和局内结果。
- 当前限制：Schema 4 只能表达技能、敌人、地图和 Encounter，不能表达构筑条件与输出。
- 可复现示例：同种子自动玩家需要在十分钟运行中得到相同候选历史和最终统计。

## 3. 现有模块为何不足

M4 Trigger/Targeting/Delivery/Effect 能执行技能，M5 Map/Encounter 能生成敌人，但都没有
跨内容库存、候选资格、一次性联动输出和技能转化语义。把这些判断组合在单个技能或地图中
会使候选规则分散且无法由无头应用层复用。

## 4. 提议方案

- 新增或修改的模块：Schema 5 定义、DTO、Baker/Validator、BuildRuntimeCatalog、BuildState、
  OfferGenerator、ProgressionRuntime、RunSession。
- 公共 API：库存获取/替换、Offer Generate/Reroll/Banish/Skip/Select、RunSession 命令与结果。
- 数据结构：紧凑库存、标签计数、激活联动、进化资格、XP Pickup 侧车和候选历史。
- 注册方式：继续使用显式 SkillModuleRegistry；新内容由 ContentRegistry 和运行前目录编译。
- 编辑器工作流：M6 ScriptableObject、测试内容生成命令和 baked JSON。

## 5. 影响分析

| 领域 | 影响 |
|---|---|
| Assembly 依赖 | 不改变现有方向或 asmdef 引用 |
| Content Schema | 支持版本提升到 5；新增五种 kind 与显式 DTO |
| Save Schema | 不变；未来保存仍使用稳定 ContentId，不保存 RuntimeContentIndex |
| Addressables | 仅增加 Placeholder 测试 Pack，不改变加载策略 |
| 性能 | 固定 Tick 使用持久缓冲；Offer 分配仅发生在暂停选择阶段 |
| 测试 | 增加逻辑、内容 round-trip、拾取、暂停与十分钟确定性测试 |
| 平台 | 无平台差异 |
| 资产与许可 | 仅程序化 Placeholder 和元数据，无第三方资源 |
| 兼容性 | Schema 1–4 保持兼容；Schema 5 Pack 需重新 Bake |

## 6. 备选方案

只在 Application 中维护升级列表较简单，但会复制 Simulation 的库存和资格真值，导致 UI、
无头测试和重放结果分叉，因此未采用。

## 7. 迁移与回滚

- 迁移步骤：需要 M6 内容的 Pack 声明 Schema 5 并重新 Bake。
- 旧数据处理：Schema 1–4 Catalog 原样加载，旧 Hash 字段顺序不变。
- 回滚步骤：移除 Schema 5 Pack 即可；旧 Pack 与存档稳定 ID 不需转换。

## 8. 验收标准

- [x] 新机制具有跨内容复用价值
- [x] 不为单个角色或技能建立一次性系统
- [x] 有自动测试
- [x] 有十分钟正确性与确定性验证；目标规模性能验证按 M10 执行
- [x] 文档和 ADR 已更新
- [x] 不破坏现有内容和存档

## 9. 审批

- 技术负责人：由当前用户 M6 指令授权实施
- 内容负责人：测试内容仅为 Placeholder
- 制作人：由当前用户 M6 指令授权实施
- 结论：Accepted / Implemented
