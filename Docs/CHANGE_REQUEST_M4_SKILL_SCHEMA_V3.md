# Change Request：M4 模块化技能运行时与 Content Schema 3

- 编号：CR-2026-002
- 状态：Implemented
- 提交日期：2026-07-26
- 提交人：Codex（依据当前 M4 用户指令）
- 目标里程碑：M4
- 关联 ADR：ADR 0006

## 1. 变更摘要

M1 的 `RuntimeSkillDefinition` 只保存冷却元数据，无法表达 M4 指定的 Trigger、Targeting、
Delivery、Effect、资源成本或等级补丁。M4 将 baked catalog 扩展到 Schema 3，并新增显式
注册的纯模拟技能运行时，使单体投射物、环绕物、地面区域和伤害光环由同一套模块配置完成。

## 2. 触发场景

- 用户或设计需求：普通新技能必须由五类模块组合，不为每个技能增加控制器。
- 当前限制：Schema 1/2 无模块、EffectOp、LevelPatch 或表现 ID 字段。
- 可复现示例：M1 Skill 只能表达 cooldown，无法让 M3 Damage/Status 系统执行一次命中。

## 3. 现有模块为何不足

M3 负责效果真值，但没有触发、选目标、交付生命周期或等级成长。把这些规则直接写进具体
技能会破坏内容扩展边界。M2 的 Projectile/Area Store 和 SpatialGrid 可以复用，但需要一个
通用、显式注册且不依赖 UnityEngine 的技能编排层。

## 4. 提议方案

- 新增或修改的模块：Schema 3 Skill、五类 executor 注册表、Skill Instance、Delivery
  生命周期、Effect 命令解析、Level 编译和预览 Harness。
- 公共 API：`RuntimeSkillDefinition`、`SkillModuleRegistry`、`SkillRuntimeCatalog`、
  `SkillRuntime`、`SkillTriggerContext` 和 `SkillPreviewHarness`。
- 数据结构：稳定模块 ContentId、紧凑 `EffectOp[]`、typed `SkillLevelPatch[]`、
  RuntimeContentIndex 引用、可复用目标/命令缓冲。
- 注册方式：Composition Root 直接调用 `Register*`；禁止反射扫描。
- 编辑器工作流：M4 菜单创建四个 Placeholder SkillAuthoring 资产并使用现有 Baker 输出
  Schema 3 JSON。

## 5. 影响分析

| 领域 | 影响 |
|---|---|
| Assembly 依赖 | 不改变 asmdef 方向；Simulation 继续依赖 Core 和 Content.Runtime |
| Content Schema | 新增 Schema 3；Schema 1/2 旧技能保持只读兼容 |
| Save Schema | 无影响；M8 尚未实现，RuntimeContentIndex 仍禁止持久化 |
| Addressables | 只记录稳定 PresentationId；M4 不加载表现资源 |
| 性能 | Tick 使用已解析 executor/索引和复用缓冲；未引入 Jobs/Burst |
| 测试 | 增加全部初始 Trigger/Targeting/Delivery/Effect、LevelPatch、复用、ProcDepth 和预览覆盖 |
| 平台 | 无影响 |
| 资产与许可 | 只新增程序化 Placeholder 作者数据，无第三方或 AI 资产 |
| 兼容性 | 旧 Schema 1/2 Catalog 可加载；可执行技能 Pack 必须升级 Schema 3 并重 Bake |

## 6. 备选方案

备选方案是在 M3 测试中直接调用 Damage/Status API，并把具体技能生命周期留到后续实现。
该方案不改变 Schema，但不能证明技能内容无需核心代码即可扩展，也不满足 M4 的 Delivery、
LevelPatch 和预览验收，因此不采用。

## 7. 迁移与回滚

- 迁移步骤：需要执行逻辑的 SkillAuthoring 配置模块与 Effect，Pack 升到 Schema 3 后重 Bake。
- 旧数据处理：Schema 1/2 技能保留为非可执行元数据；不会静默推断模块。
- 回滚步骤：移除 Schema 3 Pack 和 M4 Composition Root 装配；既有 M1/M3 Pack 不需改写。

## 8. 验收标准

- [x] 新机制具有跨内容复用价值
- [x] 不为单个角色或技能建立一次性系统
- [x] 有自动测试
- [x] 固定种子预览回归；完整压力/Soak 仍按 M10 门禁执行
- [x] 文档和 ADR 已更新
- [x] 不破坏现有内容；存档尚未实现且格式未改变

## 9. 审批

- 技术负责人：由当前 M4 用户指令授权实施
- 内容负责人：由当前 M4 用户指令授权实施
- 制作人：待项目负责人复核里程碑结果
- 结论：Implemented；最终编译、测试、验证与构建证据记录于 M4 结果报告。
