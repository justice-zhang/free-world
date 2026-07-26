# Change Request：M5 敌人、地图与 Encounter Schema 4

- 编号：CR-2026-003
- 状态：Implemented
- 提交日期：2026-07-26
- 提交人：Codex
- 目标里程碑：M5
- 关联 ADR：ADR 0007

## 1. 变更摘要

M1 的 Enemy 与 Map 只保存最小展示元数据，Catalog 也没有 Encounter 类型，无法表达 M5
要求的敌人行为、攻击技能、奖励、地图运行时参数、遭遇阶段、刷怪预算和 Boss 规则。
本变更将内容 Schema 从 3 扩展到 4，并保持 Schema 1–3 的读取与确定性 Hash 兼容。

## 2. 触发场景

- 用户或设计需求：按 M5 提示词交付可配置敌人、地图、刷怪和 Encounter。
- 当前限制：Enemy 只有生命/半径，Map 只有 Provider/Scene，Encounter 尚不存在。
- 可复现示例：无法仅通过内容配置表达追踪敌人、有限地图和分阶段 Boss 刷新。

## 3. 现有模块为何不足

M2 Store/SpatialGrid、M3 战斗和 M4 Skill 可直接复用，但它们不拥有地图、敌人决策或
Encounter 作者数据。用 Skill、Status 或场景 MonoBehaviour 组合无法表达可复用的预算、
并发和地图边界，而且会把刷怪时间轴硬编码进 Scene。

## 4. 提议方案

- 新增或修改的模块：Schema 4 Enemy/Map 字段、Encounter Schedule、Map Runtime、Enemy
  Runtime、Spawn Scheduler 与显式 M5 Pipeline。
- 公共 API：纯运行时定义、`IMapRuntime`、Difficulty Snapshot、Headless Harness。
- 数据结构：稳定 ContentId、不可变数组、RuntimeContentIndex 绑定和复用命令缓冲。
- 注册方式：Content DTO 显式 kind；地图 Provider 由显式 Factory 注册，不反射扫描。
- 编辑器工作流：M5 Fixture Setup 生成程序化 Placeholder 并 Bake Schema 4 Catalog。

## 5. 影响分析

| 领域 | 影响 |
|---|---|
| Assembly 依赖 | 不改变；Simulation 仍只依赖 Core/Content.Runtime |
| Content Schema | 最高版本提升至 4；Schema 1–3 保持兼容 |
| Save Schema | 不变；未来仍只保存稳定 ContentId |
| Addressables | 只新增 development-only Placeholder 内容 |
| 性能 | 单线程紧凑数组和复用缓冲；目标规模与 30 分钟 Soak 留到 M10 |
| 测试 | 增加 DTO/Hash/验证、地图、行为、刷怪、确定性和五分钟 Harness |
| 平台 | 无影响 |
| 资产与许可 | 仅程序化 Placeholder，无第三方或正式资产 |
| 兼容性 | 旧 Catalog 不增加 M5 Hash 字段；Schema 4 必须显式完整配置 |

## 6. 备选方案

把 Enemy、Encounter 和刷怪数据写进 Scene MonoBehaviour。该方案较快，但破坏地图/遭遇解耦、
无头测试和内容包复用，因此拒绝。

## 7. 迁移与回滚

- 迁移步骤：需要 M5 数据的 Pack 显式升级到 Schema 4 并重新 Bake。
- 旧数据处理：Schema 1–3 Enemy/Map 继续按最小非 M5 定义加载；不得静默补齐行为。
- 回滚步骤：移除 Schema 4 Pack 并回退运行时/DTO；旧 Catalog 无需迁移。

## 8. 验收标准

- [x] 新机制具有跨内容复用价值
- [x] 不为单个敌人或地图建立一次性系统
- [x] 有自动测试
- [ ] 有性能验证
- [x] 文档和 ADR 已更新
- [x] 不破坏现有内容和存档

## 9. 审批

- 技术负责人：依据当前用户 M5 明确指令
- 内容负责人：M5 严格里程碑审查确认
- 制作人：依据当前用户 M5 明确指令
- 结论：Implemented；五分钟 Headless 正确性与泄漏门禁已完成。30 分钟 Soak 和
  1,500/3,000/5,000 目标实体性能验证仍为 `NOT RUN`，按计划留到 M10。
