# Codex 工作规范

## 读取顺序

每次任务开始前阅读：

1\. AGENTS.md

2\. Docs/MASTER_PLAN.md

3\. Docs/ARCHITECTURE.md

4\. Docs/CONTENT_SCHEMA.md

5\. Docs/CODEX_WORKFLOW.md

6\. Docs/AGENT_COLLABORATION.md

7\. Docs/EXECUTION_ORDER.md

8\. 当前里程碑提示词指定的文档

9\. 当前代码与测试

## 实施流程

1\. 运行当前基线测试。

2\. 输出不超过 10 条的实现计划。

3\. 只实现当前里程碑。

4\. 增加或更新测试。

5\. 运行编译、测试、验证和适用的构建。

6\. 更新受影响文档和 ADR。

7\. 输出结果报告。

## 行为限制

- 不提前实现后续里程碑。

- 不进行与当前目标无关的格式化或重构。

- 不引入未批准依赖。

- 不删除失败测试或降低断言强度。

- 不把手工检查描述为自动测试通过。

- G0—G2 框架阶段不生成正式美术、Logo、角色或世界观素材；G3 仅允许按已批准生产清单、
  provenance、Addressables 与审查门禁逐批生成/导入，禁止清单外扩量。

- 不导入参考开源项目资源。

- 不修改已发布 ContentId。

## 结果报告模板

使用 Templates/CODEX_RESULT_REPORT.md。

## 双 Agent 协作

两个 Agent 协作时必须遵守 `Docs/AGENT_COLLABORATION.md`：

- 当前任务只有一个 Owner 可以写实现分支；
- 默认只有当前 Owner 在工作，另一个 Agent 等待干净交接；
- 所有权转移使用 `Templates/AGENT_HANDOFF_TEMPLATE.md`；
- 接管者必须独立核验 Git、Unity、权限和当前环境基线；
- 当前里程碑未 merge/tag 前，任何 Agent 都不得开始下一里程碑。

## 变更升级

出现以下情况时暂停并提交 Change Request：

- 需要改变程序集依赖方向

- 需要改变内容 Schema

- 需要改变存档格式

- 需要增加第三方包

- 需要调整模拟 Tick 率

- 需要新增不可复用的一次性系统

- 需要绕过构建验证器
