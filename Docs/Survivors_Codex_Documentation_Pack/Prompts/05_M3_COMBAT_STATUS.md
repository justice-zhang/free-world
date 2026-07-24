# M3：属性、伤害、护盾与状态系统

## 目标

在模拟内核上实现统一属性修正、伤害管线、生命/护盾、状态效果、死亡和高频事件缓冲。

## 前置条件

- M2 模拟内核已验收。

- Store、命令缓冲、事件缓冲和固定 Tick 稳定。

## 开始前

1\. 阅读 AGENTS.md、Docs/ARCHITECTURE.md、Docs/CODEX_WORKFLOW.md。

2\. 阅读本提示词列出的相关文档。

3\. 检查当前 Git diff，确保没有未说明的改动。

4\. 运行现有测试并记录基线。

5\. 输出不超过 10 条的实现计划，然后开始修改。

## 必须交付

1\. 实现稳定 StatId 和运行时 StatIndex。初始支持生命、移动速度、伤害、攻击速度、冷却、范围、持续、弹数、穿透、暴击、护甲、拾取范围、幸运、回复。

2\. 实现 ModifierCollection，统一顺序：Base -\> AddFlat -\> AddPercent -\> Multiply -\> Clamp -\> Override。Modifier 包含 SourceId、StatId、Operation、Value、Priority、StackingGroup、Duration。

3\. 实现 DamagePacket 和 DamageContext，包含来源实体、目标实体、来源 ContentId、伤害类型、标签、基础值、暴击资格、ProcCoefficient、击退、位置和 ProcDepth。

4\. 实现伤害结算顺序并集中在 DamageResolutionSystem。不得让技能直接修改生命值。

5\. 实现 Health、Shield、Armor/Resistance 的最小模型。

6\. 实现状态系统：RefreshDuration、AddStacks、ReplaceIfStronger、IndependentInstances、MaxStacks、TickInterval、DispelTags、ImmunityTags。

7\. 实现死亡请求与死亡事件，确保同一实体只死亡一次。

8\. 实现 Tick 内结构体事件缓冲：DamageApplied、StatusApplied、EntityDied、ShieldChanged。

9\. 实现 ProcDepth 和触发链上限，并记录被截断次数。

10\. 创建测试状态：Burning、Slow、Shielded；仅使用占位定义。

## 必须测试

- 所有 Modifier 运算顺序和优先级。

- 同 StackingGroup 的规则。

- 暴击、护甲、护盾、生命结算。

- 伤害不会低于/高于已定义边界。

- 状态四种叠层策略。

- 状态 Tick、过期、驱散和免疫。

- ProcDepth 超限被截断。

- 死亡事件只触发一次。

- 无效目标伤害安全失败。

- 固定种子结果可复现。

## 验收标准

- 技能或测试命令不能直接写 Health。

- 状态定义来自运行时内容，不持有 Unity Object。

- 事件缓冲在 Tick 结束后正确清空。

- 旧测试全部通过。

## 禁止

- 不实现完整技能选择和投射物行为。

- 不把具体角色技能写进 Damage System。

- 不在每次属性读取时分配集合。

- 不使用字符串比较作为高频属性查找。

## 文档更新

- 更新伤害顺序、Modifier 规则和状态 Schema。

- 若改变 M1 Runtime Definition，记录兼容性影响。

## 完成报告

使用 Templates/CODEX_RESULT_REPORT.md，必须包含：

1\. 修改文件清单。

2\. 关键设计和理由。

3\. 实际执行的命令。

4\. 编译、测试、验证和构建的真实结果。

5\. 未完成事项和已知限制。

6\. 下一里程碑前置条件。
