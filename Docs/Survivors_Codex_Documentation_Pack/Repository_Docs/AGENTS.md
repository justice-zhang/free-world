# AGENTS.md

本文件定义 Codex 及其他自动化开发代理在本仓库中的强制工作规则。规则适用于所有目录，除非更深层目录存在更严格的 `AGENTS.md`。

## 1. 项目性质

这是一个从零创建的、可商业化的 Unity 类幸存者游戏框架。

- 不是任何开源游戏的 fork。
- 不得导入、复制或改造参考项目的美术、角色、场景、音效、音乐、字体、Prefab、Scene、动画、材质、Shader、Logo、品牌或演示资源。
- 默认不复制参考项目代码。确需引入第三方代码时，必须先提交 Change Request，并登记来源、许可证、版本、文件范围和改动。
- 框架阶段只使用程序化 Placeholder。

## 2. 开始任务前的读取顺序

每次任务开始前依次阅读：

1. `AGENTS.md`
2. `Docs/MASTER_PLAN.md`
3. `Docs/ARCHITECTURE.md`
4. `Docs/CONTENT_SCHEMA.md`
5. `Docs/CODEX_WORKFLOW.md`
6. `Docs/EXECUTION_ORDER.md`
7. 当前里程碑提示词
8. 与当前任务相关的 ADR、测试计划和性能预算

如果文档之间冲突，优先级为：

```text
用户当前明确指令
→ AGENTS.md
→ 已接受 ADR
→ ARCHITECTURE.md / CONTENT_SCHEMA.md
→ 当前里程碑提示词
→ 其他说明文档
```

发现冲突时必须停止相关实现并报告，不得自行选择更方便的解释。

## 3. 技术与架构硬约束

- Unity 精确版本以 `ProjectSettings/ProjectVersion.txt` 为准。
- Unity 6 LTS、URP、C#。
- Windows x64 / Steam 为首发目标。
- 使用 Input System、Addressables、Localization 和 Unity Test Framework。
- 模拟层不得依赖 `GameObject`、`MonoBehaviour`、Scene、Prefab、Sprite、AudioClip、Animator 或 Steam。
- `Game.Core` 不得引用 `UnityEngine`。
- 高频模拟不得采用逐敌人 `MonoBehaviour.Update`。
- 高频路径不得使用 LINQ、反射、字符串格式化或临时托管集合。
- 不得使用 `Resources.Load`、`GameObject.Find`、`FindObjectOfType` 或全局 Service Locator 解决依赖。
- 不得引入未经批准的第三方运行时包。
- 不使用 DOTween、Odin 或第三方 DI 框架。
- 存档保存稳定 `ContentId`，不得保存运行时索引或 Unity Object 引用。
- 用户可见文本必须使用本地化 Key。
- 正式资源必须通过 provenance 和许可证验证。

## 4. 内容扩展规则

在使用已有模块时，新增以下内容不应修改核心程序集：

- 角色
- 技能
- 被动与 Trait
- 状态效果组合
- 敌人和 Boss
- Synergy 和 Evolution
- 地图和 Encounter
- 解锁条件和局外升级

无法通过现有模块表达的新机制，应先提交 `Templates/CHANGE_REQUEST_TEMPLATE.md`，明确：

- 现有模块为何不足
- 是否能通过组合解决
- 新模块的通用价值
- 对 Schema、存档、性能和测试的影响
- 迁移与回滚方案

禁止把新机制硬编码进某个具体角色、技能或地图。

## 5. 单里程碑纪律

- 每次只实现一个被明确指定的里程碑或修复任务。
- 不提前实现后续里程碑。
- 不进行无关重构。
- 不批量创建正式内容。
- 计划不超过 10 条，并说明将执行的测试。
- 里程碑未通过审查门禁，不得开始下一里程碑。

## 6. 测试与真实性

任何“完成”“通过”“可构建”声明都必须有实际执行证据。

审查结果只能使用：

- `PASS`
- `FAIL`
- `NOT RUN`

不得把 `NOT RUN` 表述为通过，不得伪造命令、日志、性能数据或构建产物。

最低要求：

- 修改纯逻辑时运行相关 EditMode 测试。
- 修改场景、输入、UI 或生命周期时运行相关 PlayMode 测试。
- 修改内容 Schema 时运行完整内容验证和迁移测试。
- 修改性能热点时运行基线和对比测试。
- 修改构建门禁时生成实际 Development 或 Release Build。

## 7. 文档与 ADR

以下变化必须新增或更新 ADR：

- Unity 版本升级
- Assembly 依赖方向改变
- 模拟 Tick 频率改变
- Content Schema 或稳定 ID 规则改变
- 存档格式或迁移策略改变
- 新增第三方包
- 替换资源加载、模拟、渲染或平台后端

代码、文档和测试必须在同一任务中保持一致。

## 8. 资产规则

- `Assets/GameAssets/Placeholder` 只放程序化占位资源。
- `Assets/GameAssets/AI` 中的正式资源必须有对应 provenance。
- `Assets/ThirdParty` 中的内容必须登记到 `THIRD_PARTY_NOTICES.md`。
- `release` 标签不得包含 `placeholder` 或 `development-only`。
- 禁止把参考开源项目资源作为正式素材或 AI 输入。
- 文件来源不明、商业权利不清或 Hash 不一致时，必须阻止 Release 构建。

## 9. 完成报告

每次任务结束必须使用 `Templates/CODEX_RESULT_REPORT.md` 的结构报告：

1. 实现范围
2. 新增和修改文件
3. 关键决定
4. 实际执行的命令
5. 测试和构建结果
6. 未执行项目及原因
7. 已知限制和风险
8. 下一步前置条件

测试失败、验证失败或构建失败时，不得宣称任务完成。
