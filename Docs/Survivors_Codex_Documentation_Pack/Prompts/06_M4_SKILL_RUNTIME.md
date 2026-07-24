# M4：模块化技能运行时

## 目标

实现 Trigger、Targeting、Delivery、Effect 和 LevelPatch 的可注册技能运行时，使普通新技能可通过配置组合。

## 前置条件

- M3 伤害与状态系统已验收。

- M1 内容烘焙可扩展。

## 开始前

1\. 阅读 AGENTS.md、Docs/ARCHITECTURE.md、Docs/CODEX_WORKFLOW.md。

2\. 阅读本提示词列出的相关文档。

3\. 检查当前 Git diff，确保没有未说明的改动。

4\. 运行现有测试并记录基线。

5\. 输出不超过 10 条的实现计划，然后开始修改。

## 必须交付

1\. 扩展 Skill Authoring/Runtime Definition：Trigger、Targeting、Delivery、Effects、LevelPatches、Tags、Cooldown 和资源成本。

2\. 实现显式注册表：

- Trigger Executor

- Targeting Executor

- Delivery Executor

- Effect Executor

- Condition Evaluator

不得使用运行时反射扫描。

3\. 实现烘焙 EffectOp\[\] 和紧凑技能运行时数据。

4\. 初始 Trigger：Timer、OnHit、OnKill、OnDamageTaken、OnPickup、OnStatusApplied。

5\. 初始 Targeting：Self、Nearest、Random、Circle、Cone、Line、Ring、RandomPointAroundPlayer。

6\. 初始 Delivery：Instant、Projectile、Area、Aura、Orbit。

7\. 初始 Effect：Damage、Heal、ApplyStatus、RemoveStatus、Knockback、Pull、ModifyStat、SpawnSecondarySkill、GrantShield、GainResource。

8\. 实现 Skill Instance、等级、冷却、触发上下文、目标结果和执行命令缓冲。

9\. LevelPatch 在烘焙时验证路径和类型，运行时不得通过字符串反射修改对象。

10\. 创建至少四个测试技能：单体投射物、环绕物、地面区域、伤害光环。全部使用程序化占位表现 ID。

11\. 实现技能预览的纯模拟 Harness，输出 DPS、命中数和触发次数，UI 后续实现。

## 必须测试

- 每种初始 Trigger 的基本行为。

- Targeting 在空间网格上得到正确目标。

- Projectile、Area、Aura、Orbit 的生命周期。

- EffectOp 正确调用 M3 伤害/状态系统。

- LevelPatch 各等级结果。

- 同一技能可被两个角色实例复用。

- ProcDepth 在二次技能中传播。

- 缺失模块 ID 在验证阶段失败。

- 四个测试技能固定种子结果稳定。

## 验收标准

- 新测试技能不需要新增 MonoBehaviour。

- 运行时不引用 Authoring ScriptableObject。

- 高频技能执行不使用反射或 LINQ。

- Skill 不直接修改 Health。

- 内容验证能报告无效 LevelPatch 和模块引用。

## 禁止

- 不实现敌人 AI、正式 VFX、正式 UI 或构筑选择。

- 不为四个测试技能各写一个专用控制器。

- 不允许 LevelPatch 在运行时解析任意字符串路径。

## 文档更新

- 更新 Docs/EFFECT_MODULES.md 和 Skill Schema。

- 记录新增模块的稳定 ID 和参数。

## 完成报告

使用 Templates/CODEX_RESULT_REPORT.md，必须包含：

1\. 修改文件清单。

2\. 关键设计和理由。

3\. 实际执行的命令。

4\. 编译、测试、验证和构建的真实结果。

5\. 未完成事项和已知限制。

6\. 下一里程碑前置条件。
