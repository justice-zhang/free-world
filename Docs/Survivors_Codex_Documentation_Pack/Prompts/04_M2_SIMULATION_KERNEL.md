# M2：固定 Tick 模拟内核

## 目标

建立与 Unity 表现层隔离的固定 Tick 世界、实体句柄、紧凑 Store、系统管线、空间网格和渲染快照。

## 前置条件

- M1 内容系统已验收。

- 测试运行时定义可以被 Simulation 引用。

## 开始前

1\. 阅读 AGENTS.md、Docs/ARCHITECTURE.md、Docs/CODEX_WORKFLOW.md。

2\. 阅读本提示词列出的相关文档。

3\. 检查当前 Git diff，确保没有未说明的改动。

4\. 运行现有测试并记录基线。

5\. 输出不超过 10 条的实现计划，然后开始修改。

## 必须交付

1\. 实现 SimulationClock 和固定 30 Hz Tick Runner，支持累积时间、最大追赶 Tick、暂停和单步调试。

2\. 实现 EntityHandle(Index, Generation)、Free List、Generation 校验和失效句柄检测。

3\. 实现最小 Store：

- ActorStore

- ProjectileStore

- AreaStore

- PickupStore

使用 Dense Array 和 Swap-back Remove。不要写通用 ECS。

4\. 实现 SimulationWorld、ISimulationSystem 和显式系统 Pipeline。M2 只放最小移动、生命周期、清理和快照系统。

5\. 实现 RandomStream，种子、派生流和调用规则可测试。禁止在 Simulation 中使用 UnityEngine.Random。

6\. 实现统一 2D Spatial Grid：插入、更新、删除、半径查询、邻近查询。

7\. 实现命令缓冲和事件缓冲的基础结构，避免在系统遍历时直接改变 Store 结构。

8\. 实现 RenderSnapshot：记录上一 Tick 和当前 Tick 的实体位置、朝向、状态标记。表现层可插值，但本里程碑不做正式 View。

9\. 提供 Headless Simulation Harness，可创建测试 Actor、移动若干 Tick 并导出摘要。

10\. 添加诊断计数：活动实体、创建、删除、无效句柄访问、每 Tick 时间。

## 必须测试

- 固定 Tick 在不同渲染 Delta 下得到相同 Tick 数和结果。

- 暂停和单步正确。

- 删除实体后旧句柄失效。

- Swap-back 后其他有效句柄仍正确。

- Store 扩容和复用正确。

- 系统顺序固定且可断言。

- Spatial Grid 半径查询与暴力结果一致。

- 同一种子产生相同移动结果。

- 快照包含前后状态并可计算插值。

- Headless Harness 不创建 GameObject。

## 验收标准

- Simulation Assembly 不引用 MonoBehaviour、Scene 或表现资源。

- 无逐实体 Update。

- 固定种子测试重复通过。

- 无效句柄不会读取或写入其他实体。

- M1 内容加载测试保持通过。

## 禁止

- 不实现完整伤害、状态、技能、敌人 AI 或地图。

- 不在 M2 过早 Job 化全部逻辑。

- 不使用 UnityEngine.Random。

- 不通过静态全局 World 访问模拟。

## 文档更新

- 更新 ADR 0002 和系统执行顺序。

- 文档化 Store 生命周期、句柄失效和快照格式。

## 完成报告

使用 Templates/CODEX_RESULT_REPORT.md，必须包含：

1\. 修改文件清单。

2\. 关键设计和理由。

3\. 实际执行的命令。

4\. 编译、测试、验证和构建的真实结果。

5\. 未完成事项和已知限制。

6\. 下一里程碑前置条件。
