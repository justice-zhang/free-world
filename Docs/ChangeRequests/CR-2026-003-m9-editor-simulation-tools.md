# Change Request：M9 Editor 复用纯模拟预览服务

- 编号：CR-2026-003
- 状态：Implemented
- 提交日期：2026-07-26
- 提交人：Codex
- 目标里程碑：M9
- 关联 ADR：ADR 0011

## 1. 变更摘要

为 Wave Timeline 和 Skill Preview 建立可复用的纯模拟查询 API，并允许外层 `Game.Editor` 单向引用
`Game.Simulation`。这消除编辑器复制运行时公式的漂移，同时不改变任何内容或存档 Wire Schema。

## 2. 触发场景

- 用户或设计需求：M9 要求 Wave Timeline 与运行时抽样一致，Skill Preview 与 Headless Harness
  结果一致。
- 当前限制：M8 的 Editor 不引用 Simulation；Encounter 曲线只存在于 Scheduler 私有实现，M4
  Preview 只接受固定的等级/属性配置。
- 可复现示例：若 Editor 自行实现插值、等级 Patch 或伤害属性，任一运行时改动都可能让预览静默
  失真。

## 3. 现有模块为何不足

现有 Trigger、Targeting、Delivery 和 Effect 模块足以表达测试技能，不需要新内容模块；缺少的是
外层工具调用真实运行逻辑的稳定只读接口。因此不增加一次性角色、技能或地图分支。

## 4. 提议方案

- 新增或修改的模块：共享 `EncounterTimelineSampler`；扩展 `SkillPreviewHarness` 详细报告。
- 公共 API：纯值 `EncounterTimelineSample`、`SkillPreviewRequest/Geometry/Report`。
- 数据结构：只包含 ContentId、数值和只读日志，不包含 Unity Object。
- 注册方式：沿用 Content Registry 和现有模块注册表，无新硬编码内容 Registry。
- 编辑器工作流：Editor Bake 全部 Pack、按稳定 ID 解析，再调用纯 API。

## 5. 影响分析

| 领域 | 影响 |
|---|---|
| Assembly 依赖 | `Game.Editor → Game.Simulation`；保持单向且无环 |
| Content Schema | 不变，继续为 Schema 5 |
| Save Schema | 不变，继续为 Schema 2 |
| Addressables | 工具只通过现有 Authoring/Bake 与稳定地址工作 |
| 性能 | Preview 为显式低频 Editor 操作；Scheduler 复用无额外每 Tick 集合 |
| 测试 | Editor/Headless 一致性、Timeline/Runtime 采样一致性、Assembly 治理 |
| 平台 | 无影响 |
| 资产与许可 | 只生成程序化 Placeholder，不导入外部资产 |
| 兼容性 | 既有 Harness API 保留并委托详细 API |

## 6. 备选方案

在 Editor 内复制公式改动较少，但无法提供可靠的一致性保证，因此拒绝。

## 7. 迁移与回滚

- 迁移步骤：Scheduler 改用共享采样器；旧 Preview API 继续兼容。
- 旧数据处理：无 Schema 或资产迁移。
- 回滚步骤：移除 Editor 对 Simulation 引用并撤销工具 UI；既有 baked 内容和存档不受影响。

## 8. 验收标准

- [x] 新机制具有跨内容复用价值
- [x] 不为单个角色或技能建立一次性系统
- [x] 有自动测试
- [x] Preview 分配被报告；目标规模性能门禁明确留在 M10
- [x] 文档和 ADR 已更新
- [x] 不破坏现有内容和存档

## 9. 审批

- 技术负责人：由当前用户 M9 指令授权实施
- 内容负责人：只生成 Placeholder Fixture
- 制作人：由当前用户 M9 指令授权实施
- 结论：Accepted / Implemented
