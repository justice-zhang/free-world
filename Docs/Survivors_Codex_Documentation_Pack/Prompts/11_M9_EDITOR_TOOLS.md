# M9：编辑器工具与内容生产工作流

## 目标

让非程序人员能够安全创建、验证、预览和打包角色、技能、敌人、地图、联动和内容包。

## 前置条件

- M8 内容、流程和保存格式已稳定。

- 所有主要 Authoring Schema 已确定。

## 开始前

1\. 阅读 AGENTS.md、Docs/ARCHITECTURE.md、Docs/CODEX_WORKFLOW.md。

2\. 阅读本提示词列出的相关文档。

3\. 检查当前 Git diff，确保没有未说明的改动。

4\. 运行现有测试并记录基线。

5\. 输出不超过 10 条的实现计划，然后开始修改。

## 必须交付

1\. 实现 Content Creation Wizard，可创建 Pack、Character、Skill、Passive、Trait、Enemy、Status、Evolution、Synergy、Map、Encounter。

2\. 创建时自动：

- 生成符合规则的 ID

- 建立目录

- 建立本地化 Key

- 设置 Addressables 标签

- 创建测试模板

- 建立来源记录占位

3\. 实现 Validator Window 和命令行验证：ID、引用、依赖、循环、等级、概率、冷却、掉落、本地化、VisualProfile、碰撞半径、触发链、Placeholder 和 provenance。

4\. 实现 Wave Timeline Editor：阶段、预算、间隔、敌人权重、理论并发、生命总量、经验产量和 Boss 时间预览。

5\. 实现 Skill Preview Harness UI：选择技能/等级/属性/敌人数，显示范围、命中盒、DPS、触发次数、分配和模拟日志。

6\. 实现 Content Pack Builder：版本、依赖、Catalog、内容 Hash 和构建报告。

7\. 实现 Build Preprocessor：Release 阻止 Placeholder、缺失 provenance、Third Party 未登记、内容验证失败。

8\. 实现资产来源 Hash 检查。

9\. 创建“第二角色、第二技能、第二地图”扩展性测试内容，必须通过向导或内容资产完成。

10\. 编写面向内容制作人员的简明操作文档。

## 必须测试

- 向导生成的每种内容可烘焙。

- 重复 ID、缺失引用和循环依赖可定位。

- Release 构建遇到 Placeholder 会失败。

- provenance 缺失或 Hash 不一致会失败。

- Wave Timeline 计算与运行时抽样一致。

- Skill Preview 与 Headless Harness 结果一致。

- Pack Build 同输入产生同 Hash。

- 第二角色、技能、地图不修改核心程序集。

## 验收标准

- 非程序人员可按文档完成测试内容创建。

- 所有验证可从命令行运行。

- 构建报告列出 Pack、版本、Hash 和资源标签。

- 扩展性测试证明框架主要目标。

## 禁止

- 不创建正式美术或大量正式内容。

- 不让向导生成硬编码 Registry 修改。

- 不允许 Release 构建提供“忽略所有错误”选项。

- 不把验证逻辑只放在 EditorWindow，命令行必须可复用。

## 文档更新

- 更新内容制作手册、验证规则、Pack Builder 和资产来源流程。

## 完成报告

使用 Templates/CODEX_RESULT_REPORT.md，必须包含：

1\. 修改文件清单。

2\. 关键设计和理由。

3\. 实际执行的命令。

4\. 编译、测试、验证和构建的真实结果。

5\. 未完成事项和已知限制。

6\. 下一里程碑前置条件。
