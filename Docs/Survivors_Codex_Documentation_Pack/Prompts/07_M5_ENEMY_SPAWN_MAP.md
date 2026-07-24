# M5：敌人、刷怪、遭遇与地图运行时

## 目标

建立可配置敌人、轻量移动与决策、刷怪预算、遭遇时间线、有限地图和无限区块地图边界。

## 前置条件

- M4 技能运行时已验收。

- M2 空间网格和 M3 战斗系统稳定。

## 开始前

1\. 阅读 AGENTS.md、Docs/ARCHITECTURE.md、Docs/CODEX_WORKFLOW.md。

2\. 阅读本提示词列出的相关文档。

3\. 检查当前 Git diff，确保没有未说明的改动。

4\. 运行现有测试并记录基线。

5\. 输出不超过 10 条的实现计划，然后开始修改。

## 必须交付

1\. 扩展 Enemy Definition：基础属性、碰撞半径、移动模式、攻击 SkillId、Tags、奖励、VisualProfileId。

2\. 实现轻量敌人决策：追踪玩家、保持距离、冲锋准备/执行、简单远程攻击。行为通过配置和小型模块组合，不创建每敌人 MonoBehaviour。

3\. 实现 Steering、局部分离和简单障碍规避。普通敌人不使用 NavMeshAgent。

4\. 实现 Spawn Scheduler 和 Spawn Request Buffer。

5\. 实现 Encounter Schedule：阶段、时间、预算曲线、间隔曲线、敌人权重、群组、Elite、Boss 和并发上限。

6\. 实现 Spawn Pattern：Ring、Edge、Cluster、Line、Ambush、Portal、FixedAnchor、OffscreenRandom。

7\. 实现 IMapRuntime，首批：

- FiniteArenaMapRuntime

- ChunkedInfiniteMapRuntime 最小版本

8\. 地图场景只提供视觉和简化障碍输入；刷怪逻辑不写在地图 MonoBehaviour。

9\. 实现 Difficulty Snapshot：生命、伤害、速度、刷怪率、Elite 概率和奖励倍率。

10\. 创建两个测试地图、四种测试敌人和一个测试 Boss。全部使用程序化占位资源。

11\. 实现 Map/Encounter Headless Harness，可在无表现层情况下运行 5 分钟模拟。

## 必须测试

- 各敌人行为状态转换。

- Steering 和分离不会产生 NaN。

- Spawn Budget 与并发上限。

- 各 Spawn Pattern 在地图合法位置生成。

- 同一 Encounter 可用于两个地图。

- Map Runtime 的 Walkable 和 ResolveMovement。

- 固定种子生成相同区块和刷怪序列。

- Boss 只在指定阶段生成一次。

- Headless 5 分钟无未处理异常和实体泄漏。

## 验收标准

- 地图和遭遇配置解耦。

- 新敌人使用已有行为时只需配置。

- 新地图使用已有 MapRuntime 时只需 Definition、Scene 和 Encounter。

- 普通敌人没有逐个 NavMeshAgent 或 Update。

- M4 技能可由敌人和玩家共同使用。

## 禁止

- 不实现最终地图美术或正式敌人。

- 不把刷怪时间轴硬编码进 Scene。

- 不为每种敌人创建完整继承树。

- 不在每个敌人上单独做全局寻路。

## 文档更新

- 更新地图、敌人和 Encounter Schema。

- 文档化无限区块激活/释放策略。

## 完成报告

使用 Templates/CODEX_RESULT_REPORT.md，必须包含：

1\. 修改文件清单。

2\. 关键设计和理由。

3\. 实际执行的命令。

4\. 编译、测试、验证和构建的真实结果。

5\. 未完成事项和已知限制。

6\. 下一里程碑前置条件。
