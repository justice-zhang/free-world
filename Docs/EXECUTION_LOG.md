# 里程碑执行日志

本文件记录已完成里程碑的可审计集成状态。原始 Unity 日志和构建产物位于本地
忽略目录；对应结果报告保存在 `Docs/Reports`。

## M0：干净工程与工程治理

- 状态：`COMPLETE`
- 日期：2026-07-25
- Unity：`6000.3.20f1`
- 结果报告：`Docs/Reports/2026-07-25-m0-clean-project.md`
- 最终标签：`framework-m0`

### 集成记录

| 项目 | 记录 |
|---|---|
| M0 实现提交 | `57ce2a02ae0e83cac251615a273519c8b4c251fe` |
| M0 实现合并 | PR #1，merge commit `79d01d81b2a62b5c2dfb4a151d0772a2f46c93ad` |
| 构建清理修复 | `36b6d6e03fe14f7a25087f512ba1f35692600786` |
| 修复合并 | PR #2，merge commit `33f78be9bbe59eeff84a591bf42abbab86e01035` |
| 分支收敛 | M0 收尾合并并打标签后，以 `main` 为唯一规范分支；删除三个已合并的临时分支 |

### 最终检查

| 检查 | 结果 | 证据 |
|---|---|---|
| Git diff 与提交图 | PASS | M0 两个实现提交均为 `main` 祖先；收尾工作树只含已说明文件 |
| Bootstrap Scene 静态检查 | PASS | Build Settings 仅启用 `Assets/Scenes/Bootstrap.unity`；场景只有 Main Camera 与唯一 GameBootstrapper |
| CLI 失败路径 | PASS | Unity 错误返回 0 时：测试缺 XML 返回 4，验证缺 PASS 标记返回 5，构建缺新 EXE 返回 5 |
| 编译 | PASS | `TestResults/m0-final-compile.log`：Unity 退出 0，无编译错误 |
| EditMode | PASS | `TestResults/m0-final-tests/editmode.xml`：6/6 |
| PlayMode | PASS | `TestResults/m0-final-tests/playmode.xml`：4/4 |
| 内容验证 | PASS | `TestResults/m0-final-validation.log`：`[M0 Validation] PASS` |
| Windows Development Build | PASS | `TestResults/m0-final-build-rerun.log`：`[M0 Build] PASS`；EXE SHA-256 `5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6` |
| Windows Player 冒烟 | PASS | `TestResults/m0-final-player-smoke.log`：进入 MainMenu，无错误；8 秒后主动终止 |
| 构建后工作树 | PASS | Addressables 临时 `link.xml` 与 `.meta` 均未残留 |
| 性能/Soak | NOT RUN | M0 无正式模拟负载，按后续性能里程碑执行 |

### 下一步

只有在明确指定 M1、重新读取 M1 提示词并完成新的主分支基线后，才能开始内容模型工作。

## M1：核心类型与内容系统

- 状态：`COMPLETE`
- 日期：2026-07-25
- Unity：`6000.3.20f1`
- 实现报告：`Docs/Reports/2026-07-25-m1-content-system.md`
- 审查报告：`Docs/Reports/2026-07-25-m1-review-gate.md`
- 最终标签：`framework-m1`

### 集成记录

| 项目 | 记录 |
|---|---|
| M1 实现提交 | `8edcfadee2f2d3824dee5db0a401146e51e39f22` |
| M1 实现合并 | GitHub PR #4，merge commit `268e3f23535e304dfc4843d85ada5d2a47f642a0` |
| M1 收尾提交 | `586ae087e16bee5752dcff9a65b8dae835122c8c` |
| M1 收尾合并 | GitHub PR #5 |
| 标签目标 | `framework-m1` 指向 M1 收尾 PR 的最终 merge commit |

### 最终检查

| 检查 | 结果 | 证据 |
|---|---|---|
| Git diff 与范围 | PASS | 相对 `framework-m0` 修改 16、新增 70、删除 0；全部属于 M1 |
| Bootstrap Scene 静态检查 | PASS | Build Settings 仅启用 Bootstrap；场景有 2 个根对象、1 个 GameBootstrapper、1 个有效 baked Catalog 引用 |
| asmdef 与禁用模式 | PASS | 13 asmdef、40 条内部依赖、0 缺失、0 环；禁用 API 和 Runtime Unity 引用均为 0 |
| 编译 | PASS | `TestResults/M1ReviewFinal/compile.log`：Unity 退出 0，无 C# 错误 |
| EditMode | PASS | `TestResults/M1ReviewFinal/editmode.xml`：33/33 |
| PlayMode | PASS | `TestResults/M1ReviewFinal/playmode.xml`：5/5 |
| 内容/治理验证 | PASS | `TestResults/M1ReviewFinal/validation.log`：`[Project Validation] PASS` |
| Windows Development Build | PASS | Build Manifest：`Succeeded`、`StandaloneWindows64`、Development |
| Windows Player 冒烟 | PASS | `TestResults/M1ReviewFinal/player-smoke.log`：`packs=1, entries=4`，无失败标记 |
| Release Build | NOT RUN | M1 已执行适用的 Development Build；Release 门禁随正式发布阶段执行 |
| 性能/Soak | NOT RUN | M1 无模拟 Tick、实体或高频运行负载 |

### 下一步

M2 只能从带 `framework-m1` 标签的最终 `main` 创建独立分支；M1 不再扩张内容 Schema。

## M2：固定 Tick 模拟内核

- 状态：`COMPLETE`
- 日期：2026-07-26
- Unity：`6000.3.20f1`
- 实现报告：`Docs/Reports/2026-07-25-m2-simulation-kernel.md`
- 审查报告：`Docs/Reports/2026-07-25-m2-review-gate.md`
- 最终标签：`framework-m2`

### 集成记录

| 项目 | 记录 |
|---|---|
| M2 实现提交 | `07bfe20d0540a271ab37529cf4a01aacd8c0befe` |
| M2 远程分支 | `codex/m2-simulation-kernel` |
| M2 实现合并 | GitHub PR #6；`framework-m2` 指向该 PR 的最终 merge commit |
| 审查修复 | 同一 PR 内修复追赶 Tick 事件丢失和微小非零速度冻结，并增加回归测试 |

### 最终检查

| 检查 | 结果 | 证据 |
|---|---|---|
| Git diff 与范围 | PASS | 相对 `framework-m1` 修改 5、新增 23、删除 0；全部属于 M2 实现、测试、ADR 或集成记录 |
| Bootstrap Scene 静态检查 | PASS | 工作树与 `HEAD` Blob 均为 `79e997fe895c6a7ba0ee053b38052b7870587580`；Build Settings 只启用 Bootstrap；baked Catalog 引用有效 |
| asmdef 与禁用模式 | PASS | Game.Simulation 仅依赖 Core/Content.Runtime 且 `noEngineReferences=true`；0 环；Simulation 禁用 API 零命中 |
| 编译 | PASS | `TestResults/M2ReviewFinal` Unity 测试和 Development Build 均成功，无 C# 编译错误 |
| EditMode | PASS | `TestResults/M2ReviewFinal/editmode.xml`：50/50 |
| PlayMode | PASS | `TestResults/M2ReviewFinal/playmode.xml`：5/5 |
| 内容/治理验证 | PASS | `TestResults/M2ReviewFinal/validation.log`：`[Project Validation] PASS` |
| Windows Development Build | PASS | Build Manifest：`Succeeded`、`StandaloneWindows64`、Development；EXE SHA-256 `5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6` |
| Windows Player 冒烟 | NOT RUN | M2 未修改 Scene、输入、UI 或 Bootstrap 生命周期；实际 Development Build 已生成 |
| 性能/Soak | NOT RUN | M2 先交付正确的单线程内核；目标规模基准和 30 分钟 Soak 在性能里程碑执行 |

### 下一步

M3 只能从带 `framework-m2` 标签的最终 `main` 创建独立分支；必须复用 M2 的
Generation Handle、Command/Event Buffer 和固定 Pipeline，不得绕过 Cleanup
进行系统遍历期结构变化。

## M3：属性、伤害、护盾与状态系统

- 状态：`COMPLETE`
- 日期：2026-07-26
- Unity：`6000.3.20f1`
- 实现报告：`Docs/Reports/2026-07-26-m3-combat-status.md`
- 审查报告：`Docs/Reports/2026-07-26-m3-strict-review.md`
- 最终标签：`framework-m3`

### 集成记录

| 项目 | 记录 |
|---|---|
| M3 实现提交 | `719a40e7b3afba7a98307df2113e811551755e7b` |
| M3 远程分支 | `codex/m3-combat-status` |
| M3 实现合并 | GitHub PR #7；`framework-m3` 指向该 PR 的最终 merge commit |
| 审查修复 | 同一 PR 内修复临时护盾耗尽后过期不发容量变化事件，以及有限护盾容量聚合溢出为正无穷；增加回归测试 |

### 最终检查

| 检查 | 结果 | 证据 |
|---|---|---|
| Git diff 与范围 | PASS | 相对 `framework-m2` 修改 15、新增 43、删除 0；全部属于 M3 实现、Placeholder Fixture、测试或文档 |
| Bootstrap Scene 静态检查 | PASS | 工作树与 M2 `HEAD` Blob 均为 `79e997fe895c6a7ba0ee053b38052b7870587580`；Build Settings 只启用 Bootstrap；引用有效 |
| asmdef 与禁用模式 | PASS | 无 asmdef 改动、无循环；Simulation 禁用 API、UnityEngine 引用和 View 直写零命中 |
| Unity 编译 | PASS | 最终 EditMode、PlayMode、验证和 Build 均完成脚本编译，无 C# error/warning |
| EditMode | PASS | `TestResults/M3StrictReviewFinal/editmode.xml`：97/97 |
| PlayMode | PASS | `TestResults/M3StrictReviewFinal/playmode.xml`：5/5 |
| 内容/工程验证 | PASS | `TestResults/M3StrictReviewFinal/validation.log`：`[Project Validation] PASS` |
| Windows Development Build | PASS | Manifest：`Succeeded`、`StandaloneWindows64`、Development；EXE SHA-256 `5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6` |
| Release Build | NOT RUN | M3 适用门禁为 Windows Development Build；Release 留待正式发布阶段 |
| 性能/Soak | NOT RUN | 30 分钟 Soak 和目标实体规模压力 JSON 固定在 M10 执行 |

### 下一步

M4 只能从带 `framework-m3` 标签的最终 `main` 创建独立分支；必须复用 M3
的 Stat、Damage、Status、ProcDepth 和事件契约，不得让技能绕过
`DamageResolutionSystem` 直接写入 Health。

## M4：模块化技能运行时

- 状态：`COMPLETE`
- 日期：2026-07-26
- Unity：`6000.3.20f1`
- 实现报告：`Docs/Reports/2026-07-26-m4-skill-runtime.md`
- 审查报告：`Docs/Reports/2026-07-26-m4-strict-review.md`
- 最终标签：`framework-m4`

### 集成记录

| 项目 | 记录 |
|---|---|
| M4 实现提交 | `0f5df90c5f3224052c2ca3711b22fcd1e5d56f6f` |
| M4 远程分支 | `codex/m4-skill-runtime` |
| M4 实现合并 | GitHub PR #8；`framework-m4` 指向该 PR 的最终 merge commit |
| 严格审查修复 | 同一 PR 内修复 LevelPatch 累积结果验证、Secondary Skill 可执行性与传递注册、实例清理/代际句柄，以及 Heal 的 ActorStore 边界；增加回归测试 |

### 最终检查

| 检查 | 结果 | 证据 |
|---|---|---|
| Git diff、日志与范围 | PASS | 相对 `framework-m3` 修改 19、新增 47、删除 0；全部属于 M4 实现、Placeholder 测试夹具、测试、治理或集成记录；分支从 `framework-m3` 演进 |
| Bootstrap Scene 静态检查 | PASS | M4 未修改 `.unity` 或 EditorBuildSettings；Build Settings 仅启用 `Assets/Scenes/Bootstrap.unity`；场景无缺失脚本或冲突标记，GameBootstrapper 引用有效 |
| asmdef 与禁用模式 | PASS | asmdef 依赖无缺失、无循环；`Game.Core`/`Game.Simulation` 不引用 UnityEngine；禁用查找、Resources、运行时反射/高频 LINQ、Service Locator、高频 Instantiate/Destroy 和 View 直写均为零命中 |
| Unity 编译 | PASS | 最终 EditMode、PlayMode、验证和 Development Build 均完成脚本编译，无 C# 编译失败 |
| EditMode | PASS | `TestResults/M4StrictReviewFinal2EditMode/editmode.xml`：125/125，0 failed，0 skipped |
| PlayMode | PASS | `TestResults/M4StrictReviewFinal2PlayMode/playmode.xml`：5/5，0 failed，0 skipped |
| 内容/工程验证 | PASS | `TestResults/M4StrictReviewFinal2Validation/validation.log`：`[Project Validation] PASS` |
| Windows Development Build | PASS | Manifest：`Succeeded`、`StandaloneWindows64`、Development；EXE SHA-256 `5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6` |
| Release Build | NOT RUN | M4 适用门禁为 Windows Development Build；Release 留待正式发布阶段 |
| 性能/Soak | NOT RUN | 30 分钟 Soak 和目标实体规模压力测试按计划在 M10 执行 |

### 已知问题

`Docs/KNOWN_ISSUES.md` 已登记 M4-KI-001 至 M4-KI-008；其中四项严格审查失败已解决，
其余为已接受限制或 M10 计划项。当前没有阻止 M5 开始的 OPEN 问题。

### 下一步

M5 只能从带 `framework-m4` 标签的最终 `main` 创建独立分支；必须复用 M4 的显式模块
注册、RuntimeContentIndex、稳定 ContentId、类型化 LevelPatch 和 ProcDepth 契约，不得为
普通新技能增加专用控制器。

## M5：敌人、刷怪、遭遇与地图运行时

- 状态：`COMPLETE`
- 日期：2026-07-26
- Unity：`6000.3.20f1`
- 实现报告：`Docs/Reports/2026-07-26-m5-enemy-spawn-map.md`
- 审查报告：`Docs/Reports/2026-07-26-m5-strict-review.md`
- 最终标签：`framework-m5`

### 集成记录

| 项目 | 记录 |
|---|---|
| M5 实现提交 | `cc307b1dee4b9d45bf82aa8b0caf63685c7508fd` |
| M5 远程分支 | `codex/m5-enemy-spawn-map` |
| M5 实现合并 | GitHub PR #9；`framework-m5` 指向该 PR 的最终 merge commit |
| 严格审查 | 无运行时代码 FAIL；补齐 M5 测试计划和作者工作流，Unity 自动行尾噪声未纳入 diff |

### 最终检查

| 检查 | 结果 | 证据 |
|---|---|---|
| Git diff、日志与范围 | PASS | 相对 `framework-m4` 修改/新增 75、删除 0；全部属于 M5 Schema、运行时、Placeholder、测试或文档 |
| 两张 M5 Scene 静态检查 | PASS | 每张 Scene 各一个纯占位根对象，仅 Transform；无 MonoBehaviour/Prefab/缺失脚本；Build Settings 仍只启用 Bootstrap |
| asmdef 与禁用模式 | PASS | asmdef 无循环；Core/Simulation 无 UnityEngine；全局 Find、Resources、NavMeshAgent、高频 LINQ/反射、逐敌人 Update 均零命中 |
| Unity 编译 | PASS | 最终 EditMode、PlayMode、验证和 Development Build 均完成脚本编译，无 C# 编译失败 |
| EditMode | PASS | `TestResults/M5Review/editmode.xml`：144/144，0 failed，0 skipped |
| PlayMode | PASS | `TestResults/M5Review/playmode.xml`：5/5，0 failed，0 skipped |
| 内容/工程验证 | PASS | `TestResults/M5Review/validation.log`：`[Project Validation] PASS` |
| Windows Development Build | PASS | Manifest：`Succeeded`、`StandaloneWindows64`、Development；EXE SHA-256 `5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6` |
| Release Build | NOT RUN | M5 适用门禁为 Windows Development Build；Release 留待正式发布阶段 |
| 性能/Soak | NOT RUN | 30 分钟 Soak 和 1,500/3,000/5,000 目标实体压力 JSON 固定在 M10 执行 |

### 已知问题

`Docs/KNOWN_ISSUES.md` 已登记 M5-KI-001 至 M5-KI-004，均为已接受限制或 M10 计划项；
当前没有阻止 M6 开始的 `OPEN` 问题。

### 下一步

M6 只能从带 `framework-m5` 标签的最终 `main` 创建独立分支；必须复用 M5 的 Content 驱动
Enemy/Map/Encounter、`IMapRuntime`、Spawn Request Buffer、Difficulty Snapshot 和集中式敌人
系统，不得把刷怪时间线或逐敌人逻辑写入 Scene MonoBehaviour。

## M6：局内成长、构筑、联动与进化

- 状态：`COMPLETE`
- 日期：2026-07-26
- Unity：`6000.3.20f1`
- 实现报告：`Docs/Reports/2026-07-26-m6-build-progression.md`
- 审查报告：`Docs/Reports/2026-07-26-m6-strict-review.md`
- 最终标签：合并后创建 `framework-m6`

### 集成记录

| 项目 | 记录 |
|---|---|
| M6 实现提交 | `fc66a1d47036bbcd29698a2b3b251154f55cfd66` |
| M6 远程分支 | `codex/m6-build-progression` |
| M6 实现合并 | 待本分支 GitHub PR 创建后补录；`framework-m6` 将指向最终 merge commit |
| 严格审查修复 | 修复 Unity JsonUtility 空嵌套 DTO、具体 ScriptableObject 文件名绑定、Synergy 热路径 RuntimeContentIndex 比较，并补齐 AddEffectOp 行为证据 |

### 最终检查

| 检查 | 结果 | 证据 |
|---|---|---|
| Git diff、日志与范围 | PASS | 相对 M6 基线新增 73、修改 19、删除 0，共 92 个实现文件；全部属于 M6 Schema、运行时、Placeholder、测试或同步文档 |
| Scene / asmdef / Packages / ProjectSettings | PASS | 均无变更；Build Settings 继续使用已验收 Bootstrap |
| asmdef 与禁用模式 | PASS | Core/Simulation 无 UnityEngine；Find、Resources、全局随机、Service Locator、高频 LINQ/反射、逐敌人 Update 均零命中 |
| Unity 编译 | PASS | 最终 EditMode、PlayMode、验证和 Development Build 均完成脚本编译，无 C# 编译失败 |
| EditMode | PASS | `TestResults/M6Final/editmode.xml`：154/154，0 failed，0 skipped |
| PlayMode | PASS | `TestResults/M6Final/playmode.xml`：5/5，0 failed，0 skipped |
| 内容/工程验证 | PASS | `TestResults/M6Final/validation.log`：`[Project Validation] PASS` |
| Windows Development Build | PASS | Manifest：`Succeeded`、`StandaloneWindows64`、Development；EXE SHA-256 `5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6` |
| 10 分钟自动局 | PASS | 同一 Seed 两次各 18,000 Tick，统计/Checksum 一致、显式清理无泄漏、无效 Handle 为 0 |
| Release Build | NOT RUN | M6 适用门禁为 Windows Development Build；Release 留待正式发布阶段 |
| 性能/Soak | NOT RUN | 30 分钟和 1,500/3,000/5,000 目标实体压力 JSON 固定在 M10 执行 |

### 已知问题

`Docs/KNOWN_ISSUES.md` 已登记 M6-KI-001 至 M6-KI-006；两项审查问题已解决，其余为已接受
边界或 M10 计划项。当前没有阻止 M7 开始的 `OPEN` 问题。

### 下一步

先通过 GitHub PR 合并 M6、创建并推送 `framework-m6`，再从最终 `main`/`framework-m6`
创建 M7 独立分支。M7 只消费 `RunSession`、`UpgradeOfferSet` 和 `RenderSnapshot`，不得把
候选生成、构筑资格或模拟真值复制到 View/UI。
