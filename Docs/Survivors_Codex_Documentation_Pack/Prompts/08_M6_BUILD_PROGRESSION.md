# M6：局内成长、构筑、联动与进化

## 目标

实现经验、升级选择、技能/被动库存、构筑标签、Synergy、Evolution、局内结果和可复现候选池。

## 前置条件

- M5 地图和敌人已验收。

- 技能、战斗和内容定义稳定。

## 开始前

1\. 阅读 AGENTS.md、Docs/ARCHITECTURE.md、Docs/CODEX_WORKFLOW.md。

2\. 阅读本提示词列出的相关文档。

3\. 检查当前 Git diff，确保没有未说明的改动。

4\. 运行现有测试并记录基线。

5\. 输出不超过 10 条的实现计划，然后开始修改。

## 必须交付

1\. 实现 XP、等级曲线、经验拾取和 LevelUp Request。

2\. 实现 Skill Inventory 和 Passive Inventory：槽位、等级、最大等级、重复获取、替换策略。

3\. 实现 Offer Generator：候选池、权重、前置条件、互斥、已满槽、最大等级、Reroll、Banish、Skip 接口。

4\. 候选生成使用 Run Random Stream 的专用派生流，并可记录种子和选择历史。

5\. 实现 BuildState：Owned Skills、Passives、Traits、Tags、Active Synergies、Evolution Eligibility。

6\. 实现 Condition Evaluator：OwnsContent、HasTagCount、SkillLevelAtLeast、StatAtLeast、MapHasTag。

7\. 实现 Synergy Outputs：AddModifier、UnlockOffer、AddEffectOp、TransformSkill、GrantTrait。

8\. 实现 Evolution Definition 和 Consume Policy。

9\. 实现 Run State、暂停升级选择、Run End、Run Result 和基础统计。

10\. 创建两个测试 Synergy 和一个测试 Evolution，不创建正式构筑。

11\. 实现自动玩家 Harness：自动移动、拾取和选择升级，完成 10 分钟测试局。

## 必须测试

- XP 曲线和多次连续升级。

- Offer 权重、互斥、前置、满级和槽位规则。

- 固定种子候选结果可复现。

- Reroll 使用可预测但不同的序列。

- Banish 不再出现被移除内容。

- Synergy 条件与输出。

- Evolution 条件、转化和消费策略。

- 构筑标签随内容变化更新。

- 10 分钟自动局可完成并产生一致统计。

## 验收标准

- 不存在硬编码 FireBuild、CritBuild 等类。

- 新 Synergy 和 Evolution 只需配置。

- 升级 UI 尚未实现时，应用层可通过命令选择。

- 暂停升级时 SimulationClock 停止，测试时钟继续可控。

## 禁止

- 不实现正式数值平衡和局外商店。

- 不把构筑判断散落到技能类。

- 不使用全局随机数生成候选。

- 不在 UI 中实现候选规则。

## 文档更新

- 更新构筑、Synergy、Evolution 和 Offer Schema。

- 文档化随机流和重放诊断数据。

## 完成报告

使用 Templates/CODEX_RESULT_REPORT.md，必须包含：

1\. 修改文件清单。

2\. 关键设计和理由。

3\. 实际执行的命令。

4\. 编译、测试、验证和构建的真实结果。

5\. 未完成事项和已知限制。

6\. 下一里程碑前置条件。
