# M10：性能、CI、构建与框架冻结

## 目标

建立可重复性能基线、对真实热点进行优化、完善命令行/CI/构建清单，并完成框架冻结验收。

## 前置条件

- M9 编辑器工具和扩展性测试已验收。

- 全部核心功能可运行。

## 开始前

1\. 阅读 AGENTS.md、Docs/ARCHITECTURE.md、Docs/CODEX_WORKFLOW.md。

2\. 阅读本提示词列出的相关文档。

3\. 检查当前 Git diff，确保没有未说明的改动。

4\. 运行现有测试并记录基线。

5\. 输出不超过 10 条的实现计划，然后开始修改。

## 必须交付

1\. 建立固定压力场景：1,500 敌人、3,000 投射物、5,000 拾取物、200 VFX 请求，参数可配置。

2\. 建立 30 分钟 Headless Soak Test 和性能 JSON 输出。

3\. 记录基线：Tick 时间分位数、渲染帧时间、实体峰值、托管/Native 内存、GC、对象池、触发截断、无效句柄和 VFX 丢弃。

4\. 只对 Profiler 证明的热点迁移 Jobs/Burst。每项优化必须有优化前后数据和正确性回归测试。

5\. 完善命令：

- EditMode Test

- PlayMode Test

- Content Validate

- Soak Test

- Windows Development Build

- Windows Release Build

6\. 实现 Build Manifest：游戏版本、Git SHA、Unity 版本、Pack 版本、内容 Hash、构建 UTC、构建类型。

7\. 建立适用于自托管 Windows Runner 的 CI 工作流或等价脚本；不能假设未配置的 Unity 许可证秘密。

8\. 从干净 clone 验证导入、测试、验证和构建。

9\. 运行完整架构审计：程序集、禁止 API、内容扩展、存档、本地化、Placeholder、Third Party、provenance。

10\. 修正文档与代码差异，冻结核心公共 API；未来破坏性变化需要 ADR 和迁移计划。

11\. 执行 Docs/DEFINITION_OF_DONE.md 全部检查并生成签字报告。

## 必须测试

- 固定压力场景和 30 分钟 Soak。

- 优化前后确定性和战斗结果一致。

- 稳态托管分配目标。

- 内存无持续增长。

- 所有 EditMode、PlayMode、验证和构建命令。

- 从干净 clone 的完整流水线。

- 第二角色、第二技能、第二地图扩展性测试。

- Release Build 不含 Placeholder。

- Build Manifest 与实际内容一致。

## 验收标准

- 有可重复性能基线和机器信息。

- 所有门禁通过或明确记录批准的例外。

- Windows Release Build 可启动并完成测试局。

- 文档与代码一致。

- 框架 Definition of Done 签字完成。

## 禁止

- 不为追求数字改变模拟真值。

- 不无依据地把全部系统 Job 化。

- 不关闭安全检查或验证器生成 Release。

- 不在最终阶段引入大量新功能。

- 不把 CI 未运行描述为成功。

## 文档更新

- 更新性能基线、构建方法、CI 方法、已知限制和框架冻结报告。

## 完成报告

使用 Templates/CODEX_RESULT_REPORT.md，必须包含：

1\. 修改文件清单。

2\. 关键设计和理由。

3\. 实际执行的命令。

4\. 编译、测试、验证和构建的真实结果。

5\. 未完成事项和已知限制。

6\. 下一里程碑前置条件。
