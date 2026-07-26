# Change Request：M3 状态运行时定义与 Content Schema 2

- 编号：CR-2026-001
- 状态：Implemented
- 提交日期：2026-07-26
- 提交人：Codex（依据当前 M3 用户指令）
- 目标里程碑：M3
- 关联 ADR：ADR 0005

## 1. 变更摘要

M1 只实现 Character、Skill、Enemy 和 Map 四类最小运行时定义，无法把 M3 要求的
状态叠层、持续时间、周期 Tick、驱散和免疫规则写入内容包。M3 新增可复用的
`RuntimeStatusDefinition` 与 `StatusEffectAuthoring`，并把 baked catalog 格式扩展为
Schema 2；现有 Schema 1 内容包继续可加载。

## 2. 触发场景

- 用户或设计需求：M3 必须实现四种状态叠层策略并创建 Burning、Slow、Shielded
  程序化 Placeholder 定义。
- 当前限制：M1 Runtime Definition、DTO Codec 与 Baker 没有 `status` 内容类型。
- 可复现示例：Schema 1 内容包无法保存 `MaxStacks`、`TickInterval`、
  `DispelTags` 或 `ImmunityTags`。

## 3. 现有模块为何不足

现有 Character、Skill、Enemy、Map 定义都不表达状态生命周期。把状态字段硬编码进
技能或伤害系统会破坏内容扩展边界，并导致每个具体状态修改核心模拟代码。M4 的
EffectOp 尚未实现，也不应被 M3 提前完整实现。

## 4. 提议方案

- 新增或修改的模块：纯运行时 Status Definition、作者 Status ScriptableObject、
  Schema 2 DTO Codec、验证器和独立 M3 Placeholder 内容包。
- 公共 API：`RuntimeStatusDefinition` 与 `StatusStackingPolicy`。
- 数据结构：稳定 ContentId、四种策略、Duration、MaxStacks、TickInterval、
  DispelTags、ImmunityTags，以及不可由申请覆盖的 Modifier、PeriodicDamage、
  ShieldCapacity 通用行为。
- 注册方式：继续使用 M1 `ContentRegistry` 的多态定义注册，无反射或类型扫描。
- 编辑器工作流：由 M3 测试内容配置命令创建作者资产并执行现有 Bake 流程。

## 5. 影响分析

| 领域 | 影响 |
|---|---|
| Assembly 依赖 | 不变；Simulation 继续只依赖 Core 与 Content.Runtime |
| Content Schema | 新增 Schema 2；Schema 1 保持可加载但不得包含 status |
| Save Schema | 无影响；M8 尚未实现存档，RuntimeContentIndex 仍不得持久化 |
| Addressables | 无影响；测试状态只含纯数据 |
| 性能 | 高频状态实例使用 RuntimeContentIndex；不在 Tick 中解析作者对象；Actor 战斗数组按 slot 复用 |
| 测试 | 增加 v1/v2、DTO round-trip、状态验证、模拟策略与生命周期覆盖 |
| 平台 | 无影响 |
| 资产与许可 | 只新增程序化 Placeholder 数据，不含外部资源 |
| 兼容性 | 既有 Schema 1 M1 测试包及 Hash 保持不变；新状态包要求 Schema 2 |

## 6. 备选方案

备选方案是在测试或技能代码中直接构造状态并按具体 ContentId 分支。该方案不需要
改变磁盘 Schema，但无法证明作者数据到运行时内容的完整路径，也会把具体状态耦合到
核心系统，因此不采用。

## 7. 迁移与回滚

- 迁移步骤：需要状态定义的内容包把 manifest 升到 Schema 2，并从作者资产重新 Bake。
- 旧数据处理：Schema 1 内容包继续按原格式加载；不做静默字段推断。
- 回滚步骤：移除 Schema 2 状态内容包和对应 Codec 分支即可；既有 Schema 1 包无需改动。

## 8. 验收标准

- [x] 新机制具有跨内容复用价值
- [x] 不为单个角色或技能建立一次性系统
- [x] 有自动测试
- [x] 高频 IL 与数组复用回归测试；完整压力/Soak 按 M10 门禁执行
- [x] 文档和 ADR 已更新
- [x] 不破坏现有内容和存档

## 9. 审批

- 技术负责人：由当前 M3 用户指令授权实施
- 内容负责人：由当前 M3 用户指令授权实施
- 制作人：待项目负责人复核里程碑结果
- 结论：Implemented；Unity EditMode、PlayMode、内容验证和 Windows Development Build
  已在 M3 结果报告中记录。完整性能压力 JSON 仍遵循既定 M10 门禁。
