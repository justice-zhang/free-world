# ADR 0011：M9 内容生产工具与纯模拟预览复用

- 状态：Accepted
- 日期：2026-07-26
- 决策人：依据当前用户 M9 指令

## 背景

M8 已稳定主要 Authoring Schema、双语表和构建边界，但内容人员仍需手工创建目录、ID、语言 Key、
Addressables 标签、测试记录和来源记录。Wave 与 Skill 的编辑器预览若各自复制运行时公式，会产生
“编辑器看起来正确、实际运行不同”的长期漂移风险。

## 决策

### 工具服务与界面

- Content Creation、Validation、Wave Timeline、Skill Preview 和 Content Pack Builder 的规则放在
  可复用服务中；EditorWindow 只负责输入和展示。验证与打包同时提供命令行入口。
- 向导只生成 Schema 5、程序化 Placeholder 和 `development-only` 内容；自动维护稳定 ID、目录、
  Pack 引用、双语 Key、Addressables 标签、测试模板、来源占位和 baked Catalog。
- Content Pack Builder 使用现有 canonical Baker 的 Content Hash，并对实际 Catalog 文件另算
  SHA-256；报告显式列出版本、依赖、Catalog、标签和两个 Hash。

### 模拟复用与 Assembly 方向

- `EncounterTimelineSampler` 位于纯 `Game.Simulation`，运行时 Scheduler 和 Editor Timeline 共用
  同一个曲线采样实现。
- M4 的纯 `SkillPreviewHarness` 扩展为等级、属性、目标数感知的详细报告；M9 Editor UI 只 Bake、
  解析并调用该 Headless API。
- 因此 `Game.Editor` 新增对 `Game.Simulation` 的单向引用。Simulation 仍为
  `noEngineReferences: true`，不引用 Editor、Unity Object、Scene 或表现资源，程序集无循环。

### 发布门禁

- 普通 Project Validation 继续验证内容、依赖、本地化、Visual/Presentation Profile、触发链、
  Third Party 和 provenance。
- 非 Development Build 额外执行不可绕过的 Release 门禁：任一 Placeholder 路径/标签、缺失或
  不合格 provenance、Hash 不一致、Third Party 未登记或内容验证失败都会终止构建。
- M9 不改变 Content Schema 5 或 Save Schema 2。

## 被拒绝的方案

- 在每个 EditorWindow 内实现独立校验：命令行和 Build 无法复用，容易出现绕过路径。
- 在 Editor 中重写 Encounter 插值或 Skill 伤害公式：会与真实 Headless Runtime 漂移。
- 让 Simulation 引用 Editor 资产以提供预览：破坏纯模拟和 Server/测试边界。
- 为 Release 提供“忽略全部错误”：与来源和 Placeholder 硬门禁冲突。

## 后果

内容人员可在不修改核心程序集的情况下创建并检查主要内容类型，预览结果与真实纯模拟共享实现。
代价是 `Game.Editor` 成为对 Simulation 的外层消费者；后续任何预览功能必须保持该依赖单向，并以
自动测试证明 Editor 与 Headless 结果一致。
