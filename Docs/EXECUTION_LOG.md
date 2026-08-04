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
- 最终标签：`framework-m6`

### 集成记录

| 项目 | 记录 |
|---|---|
| M6 实现提交 | `fc66a1d47036bbcd29698a2b3b251154f55cfd66` |
| M6 远程分支 | `codex/m6-build-progression` |
| M6 实现合并 | GitHub PR #12；`framework-m6` 指向该 PR 的最终 merge commit |
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

## M7：表现层、输入与完整 UI 流程

- 状态：`COMPLETE`
- 日期：2026-07-26
- Unity：`6000.3.20f1`
- 实现报告：`Docs/Reports/2026-07-26-m7-presentation-ui-input.md`
- 审查报告：`Docs/Reports/2026-07-26-m7-strict-review.md`
- 最终标签：`framework-m7`

### 集成记录

| 项目 | 记录 |
|---|---|
| M7 实现提交 | `abdd15969023d3c3f9ba968063aae99a800d5264` |
| M7 远程分支 | `codex/m7-presentation-ui-input` |
| M7 实现合并 | GitHub PR #13；`framework-m7` 指向该 PR 的最终 merge commit |
| 严格审查修复 | 修复同批次 Removed 事件与 Snapshot 同步的重复释放风险、Input TestFixture 销毁顺序，并清理 Input Asset 行尾空格 |

### 最终检查

| 检查 | 结果 | 证据 |
|---|---|---|
| Git diff、日志与范围 | PASS | 相对 `framework-m6` 新增 35、修改 18、删除 0，共 53 个实现文件；全部属于 M7 实现、Placeholder、测试或同步文档 |
| Scene / asmdef / ProjectSettings | PASS | Bootstrap 显式组合 M7；UI 不引用 Simulation；项目 Input Action 指向 M7 资产；Project Validation PASS |
| asmdef 与禁用模式 | PASS | Core/Simulation 无 UnityEngine；Find、Resources.Load、Service Locator、高频 LINQ/反射和逐实体 Update 均零命中 |
| Unity 编译 | PASS | 最终 EditMode、PlayMode、验证和 Development Build 均完成脚本编译，无 C# 编译失败 |
| EditMode | PASS | `TestResults/M7Final/editmode.xml`：163/163，0 failed，0 skipped |
| PlayMode | PASS | `TestResults/M7Final/playmode.xml`：8/8，0 failed，0 skipped |
| 完整输入与 UI 流程 | PASS | 键鼠和虚拟手柄完成菜单、真实 Run、升级、暂停、结算和返回主菜单；暂停 Tick 停止、销毁无池/输入 owner 泄漏 |
| 内容/工程验证 | PASS | `TestResults/M7Final/validation.log`：`[Project Validation] PASS` |
| Windows Development Build | PASS | Manifest：`Succeeded`、`StandaloneWindows64`、Development；EXE SHA-256 `5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6` |
| 正式本地化与伪本地化 | NOT RUN | M8 计划项；M7 只保证 Presenter 使用 Localization Key |
| 性能/Soak | NOT RUN | 30 分钟和 1,500/3,000/5,000 目标实体压力 JSON 固定在 M10 执行 |

### 已知问题

`Docs/KNOWN_ISSUES.md` 已登记 M7-KI-001 至 M7-KI-004，均为已接受边界或后续计划项；
当前没有阻止 M8 开始的 `OPEN` 问题。

### 下一步

先通过 GitHub PR 合并 M7、创建并推送 `framework-m7`，再从最终 `main`/`framework-m7`
创建 M8 独立分支。M8 接入 Localization Table、伪本地化、字体覆盖和内容工具时，必须继续
复用 M7 Presenter/ViewModel 边界，不把正式文本或内容规则硬编码到 UI。

## M8：版本化存档、本地化与平台边界

- 状态：`COMPLETE`
- 日期：2026-07-26
- Unity：`6000.3.20f1`
- 实现报告：`Docs/Reports/2026-07-26-m8-save-localization-platform.md`
- 审查报告：`Docs/Reports/2026-07-26-m8-strict-review.md`
- 最终标签：`framework-m8`

### 集成记录

| 项目 | 记录 |
|---|---|
| M8 实现提交 | `baddd6914c07a173cc4a6091886f1580c9a1f29d` |
| M8 远程分支 | `codex/m8-save-localization-platform` |
| M8 实现合并 | GitHub PR #14；`framework-m8` 指向该 PR 的最终 merge commit |
| 严格审查修复 | 修复 Unity Context 异步文件死锁、Pseudo 运行时回退、Localization Editor 验证源、异步平台路由，并补齐 103 Key 正式门禁 |

### 最终检查

| 检查 | 结果 | 证据 |
|---|---|---|
| Git diff、日志与范围 | PASS | 相对 `framework-m7` 新增/修改 100 个实现文件、删除 0；全部属于 M8 代码、Unity Localization 资产、测试或同步文档 |
| Scene / ProjectSettings | PASS | Scene 无变更；Build Settings 只登记 Localization Settings，Bootstrap 仍为唯一启用 Scene |
| asmdef 与禁用模式 | PASS | Assembly 治理测试通过；Core/Simulation 无 Unity/平台污染；禁用查找、Resources.Load、Service Locator、BinaryFormatter 零命中 |
| Unity 编译 | PASS | 最终测试、验证和 Development Build 均无 C# 编译失败 |
| EditMode | PASS | `TestResults/M8Final/editmode.xml`：172/172，0 failed，0 skipped |
| PlayMode | PASS | `TestResults/M8Final/playmode.xml`：9/9，0 failed，0 skipped |
| 内容/工程验证 | PASS | `TestResults/M8Final/validation.log`：`[Project Validation] PASS`；103 个双语 Key |
| Windows Development Build | PASS | Manifest：`Succeeded`、`StandaloneWindows64`、Development；EXE SHA-256 `5D7EEB...C9C6` |
| Release Build | NOT RUN | M8 适用门禁为 Windows Development Build；Release 留待正式发布阶段 |
| 性能/Soak | NOT RUN | 30 分钟和 1,500/3,000/5,000 目标规模压力 JSON 固定在 M10 |

### 已知问题

`Docs/KNOWN_ISSUES.md` 已登记 M8-KI-001 至 M8-KI-004，均为已接受限制或 M10 计划项；当前没有
阻止 M9 开始的 `OPEN` 问题。

### 下一步

M9 只能从带 `framework-m8` 标签的最终 `main` 创建独立分支。真实平台实现必须替换现有 Facade
子服务并消费 Application Event/Cloud Conflict 边界，不得让 SDK 进入 Simulation；若扩展完整续局
格式，必须新增连续存档迁移和 ADR/Change Request。

## M9：编辑器工具与内容生产工作流

- 状态：`COMPLETE`
- 日期：2026-07-26
- Unity：`6000.3.20f1`
- 实现报告：`Docs/Reports/2026-07-26-m9-editor-tools.md`
- 审查报告：`Docs/Reports/2026-07-26-m9-strict-review.md`
- 最终标签：`framework-m9`（PR 合并后创建）

### 集成记录

| 项目 | 记录 |
|---|---|
| M9 实现提交 | `f29dcabc6b5cbafb6ae70531b8853fb1c36aefbb` |
| M9 远程分支 | `codex/m9-editor-tools` |
| M9 实现合并 | GitHub PR #15；`framework-m9` 指向该 PR 的最终 merge commit |
| 严格审查修复 | 收敛 Placeholder 诊断、补全 provenance 权利/来源字段、真实 Release 负向 Build 与公共 API 文档 |

### 最终检查

| 检查 | 结果 | 证据 |
|---|---|---|
| Git diff、日志与范围 | PASS | 相对 `framework-m8` 修改 19、新增 96、删除 0；均为 M9 工具、Fixture、测试或文档 |
| Scene / Package / ProjectSettings | PASS | 最终均无差异；Unity 生成的临时 Resources/link/preloaded 修改已清理 |
| asmdef 与禁用模式 | PASS | ADR 0011 接受 Editor→Simulation；无环；Simulation 热路径禁用模式零命中 |
| Unity 编译 | PASS | 最终完整测试、验证、Pack CLI 与两个 Build 门禁均完成编译 |
| EditMode | PASS | `TestResults/M9Final/editmode.xml`：181/181，0 failed，0 skipped |
| PlayMode | PASS | `TestResults/M9Final/playmode.xml`：9/9，0 failed，0 skipped |
| 内容/工程验证 | PASS | `TestResults/M9Final/validation.log`：`[Project Validation] PASS` |
| Content Pack Builder | PASS | 6 Pack；M9 Content/Catalog Hash 为 `2ad333...c527` / `49e15b...8655` |
| Release Placeholder 门禁 | PASS | 真实非 Development Build Failed，命中 `M9-RELEASE-PLACEHOLDER`，未生成 EXE |
| Windows Development Build | PASS | Manifest `Succeeded`、`StandaloneWindows64`、Development；EXE SHA-256 `5D7EEB...C9C6` |
| 成功 Release Build | NOT RUN | Placeholder 按设计必须阻止 Release |
| 性能/Soak | NOT RUN | 30 分钟和目标规模压力 JSON 固定在 M10 |

基线 EditMode 首次 Unity Crash 且无 XML，按 FAIL 记录；未改代码重试 172/172 PASS。实现中的首次
编译探针失败也已保留日志，修复后全部最终门禁通过，未把失败尝试改写为 PASS。

### 已知问题

`Docs/KNOWN_ISSUES.md` 已登记 M9-KI-001 至 M9-KI-004，均为已接受范围或 M10 计划项；当前没有
阻止 M10 开始的 `OPEN` 问题。

### 下一步

先通过 GitHub PR 合并 M9、创建并推送 `framework-m9`，清理功能分支并回到干净 `main`。M10
必须从该标签开始，实际执行 30 分钟 Soak、1,500/3,000/5,000 压力、性能 JSON、CI 与框架冻结；
不得把 M9 Preview 数据当作性能门禁。

## M10：性能、CI、构建与框架冻结

- 状态：`COMPLETE`
- 日期：2026-07-28
- Unity：`6000.3.20f1`
- 实现报告：`Docs/Reports/2026-07-28-m10-performance-ci-freeze.md`
- 审查报告：`Docs/Reports/2026-07-28-m10-strict-review.md`
- 冻结签字：`Docs/FRAMEWORK_FREEZE_SIGNOFF.md`
- 最终标签：`framework-m10`（PR 合并后创建）

### 集成记录

| 项目 | 记录 |
|---|---|
| M10 实现提交 | `d74936c1047db5235935a41d9bc33caecf858f2a` |
| 干净克隆修复提交 | `da8980694e7d3713a9dda0781ff35ee6b77496c8` |
| M10 远程分支 | `codex/m10-performance-ci-freeze` |
| 严格审查修复 | PlayMode Scene Processor null 边界、VFX 构造器二进制兼容、Release 混合组防漏打、Unity 子进程等待、Windows 长路径 Manifest 哈希 |

### 最终检查

| 检查 | 结果 | 证据 |
|---|---|---|
| EditMode | PASS | 根工作区与干净克隆均 187/187，0 failed，0 skipped |
| PlayMode | PASS | 根工作区与干净克隆均 9/9，0 failed，0 skipped |
| 内容/工程验证 | PASS | Project Validation PASS，包含五个核心程序集 API Freeze |
| 30 分钟目标规模 | PASS | 54,000 Tick；1,500/3,000/5,000；Tick p99 10.9851 ms；0 B 分配；0 GC；无持续增长 |
| Windows Development Build | PASS | 干净克隆 Manifest 绑定 `da89806`、clean=true、四项测试证据 pass |
| Windows Release Build | PASS | 非 Development、Placeholder=0；EXE SHA-256 `34C4E304...56A8F` |
| Release Player | PASS | 60 Tick、4 actors、0 invalid handles、退出码 0 |
| 独立干净克隆 | PASS | 完整流水线 7 个阶段全部退出码 0，结束后源码树无差异 |
| GitHub Actions 实际运行 | NOT RUN | 运行 `#30338477997` 已触发，但自托管 Job 约 24 小时后取消且步骤数为 0；Runner 尚未实际执行门禁 |

第一次干净克隆的测试、验证和性能均 PASS，但 Development Manifest 在 264 字符路径上失败；该轮
整体为 FAIL，未被计作最终通过。修复后从新提交完整重跑并 PASS。

### 已知问题

`Docs/KNOWN_ISSUES.md` 已关闭此前 M3-M9 的目标规模计划项，并登记 M10-KI-001 至 M10-KI-004；
当前没有阻止框架冻结的 `OPEN` 问题。

### 下一步

通过 GitHub PR 合并 M10 后创建 `framework-m10`，删除远程/本地功能分支并回到干净 `main`。
正式内容生产必须继续遵守冻结 API、Release provenance/许可证门禁和性能回归基线；破坏性变化先
提交 ADR、兼容性与迁移计划。

## Post-M10：文档状态清理

- 状态：`COMPLETE`
- 日期：2026-07-30
- 基线：`8198c5e`（`framework-m10` / `main`）
- 结果报告：`Docs/Reports/2026-07-30-document-status-cleanup.md`

### 清理范围

本次仅根据已接受的 M6、M8、M10 验收证据校准历史文档状态，不修改运行时代码、Content Schema、
资产、包配置或 ADR。`M0-KI-004`、`M0-KI-006`、`M1-KI-004`、`M1-KI-005`、`M2-KI-006`
和 `M4-KI-005` 已改为 `RESOLVED`。

GitHub Actions 运行 `#30338477997` 的最新状态也已复核：工作流确已触发，但自托管 Job 未执行
任何步骤并在约 24 小时后取消，因此 CI 门禁继续如实记录为 `NOT RUN`，`M10-KI-003` 保持
`ACCEPTED`。

### 检查

| 检查 | 结果 | 证据 |
|---|---|---|
| 文档状态一致性 | PASS | 6 个历史计划/限制项均由后续里程碑验收证据支持；真实未完成项未关闭 |
| GitHub Actions 状态 | PASS | GitHub API 返回运行 `#30338477997` 为 `cancelled`，Job `steps=[]` |
| 目标文件差异与空白 | PASS | 仅本节、`KNOWN_ISSUES.md` 和结果报告进入提交；目标文档通过 `git diff --check` |
| Unity 编译、测试与构建 | NOT RUN | 本次为纯文档状态清理，不修改可执行输入 |

### 下一步

激活匹配标签并已预激活 Unity 的 Windows x64 自托管 Runner，重新运行框架冻结 workflow，取得
真实 CI 步骤结果后再更新 `M10-KI-003`；不得用本地等价流水线替代 GitHub CI 的执行结论。

## Qinglan Demo G0.1：开发结构与审计基线

- 状态：`COMPLETE`
- 日期：2026-08-04
- Unity：`6000.3.20f1`
- 原始文档提交：`31afc1f`
- 原始报告提交：`1d6ceba`
- 连续实现基线：`91961da`
- 结果报告：`Docs/Reports/2026-08-04-g0-1-demo-structure-gate.md`

### 最终检查

| 检查 | 结果 | 证据 |
|---|---|---|
| Demo 文档综合门禁 | PASS | 24 文件、148 个具体 ID、11 CR、10 DOD、20 需求；链接/标题/围栏/尾随空白通过 |
| 源文件完整性 | PASS | V2.0 SHA-256 `F5F9B0E...359BC` |
| EditMode | PASS | `TestResults/QinglanDemo/Baseline/editmode.xml`：187/187 |
| PlayMode | PASS | `TestResults/QinglanDemo/Baseline/playmode.xml`：9/9 |
| 内容/工程验证 | PASS | `TestResults/QinglanDemo/Baseline/validation.log`：`[Project Validation] PASS` |
| Build / Performance / Soak | NOT RUN | G0.1 为纯文档与审计工作包 |

### 下一步

只进入 G0.2 CR 决策包；CR-01—CR-11 未形成明确结论前不得修改冻结核心程序集。

## Qinglan Demo G0.2：Change Request 决策

- 状态：`COMPLETE`
- 日期：2026-08-04
- 分支：`codex/qinglan-demo-implementation`
- 决策记录：`Docs/DemoDevelopment/07_CHANGE_REQUEST_DECISIONS.md`
- 结果报告：`Docs/Reports/2026-08-04-g0-2-change-request-decisions.md`

### 决策结果

| 范围 | 结果 |
|---|---|
| CR-01—CR-09 | `ACCEPTED`，映射至 `CR-2026-004`—`CR-2026-012` |
| CR-10 | `SPLIT`，公共 Stat 与 Damage Policy 分别映射至 `CR-2026-013`、`CR-2026-014`，两项接受 |
| CR-11 | `DEFERRED`，映射至 `CR-2026-015`；Demo 禁止提供“继续本局” |

接受只授权进入 G0.3 ADR、Schema/API、迁移和测试契约设计，不表示代码已实现。G0.2 不修改运行时、
Content Schema、Save Schema、Package 或资产。

### 检查

| 检查 | 结果 | 证据 |
|---|---|---|
| CR 结构与编号 | PASS | 12 文件；每份 1 个 H1、1—9 节、状态和 Demo CR 映射完整 |
| 决策覆盖 | PASS | `CR-01`—`CR-11` 在决策矩阵均有且仅有明确结论 |
| 文档链接与格式 | PASS | 相对链接目标存在，Markdown 围栏配对，`git diff --check` 通过 |
| Unity 编译、测试、构建、性能 | NOT RUN | 纯文档决策包，不修改任何可执行输入；沿用 G0.1 当前基线证据 |

### 下一步

只进入 G0.3 跨模块契约包：先定 Content Schema 6，再定 Pipeline/所有者/事件/随机流，随后定
Profile Save Schema 3，最后完成 ADR、API Freeze 变更计划、迁移与测试矩阵。G0.3 完成前不实施
CR-2026-004—014。

## Qinglan Demo G0.3：Schema、API、Save 与测试契约

- 状态：`COMPLETE`
- 日期：2026-08-04
- 分支：`codex/qinglan-demo-implementation`
- ADR：0013、0014、0015（Accepted）
- 契约冻结：`Docs/DemoDevelopment/08_G0_3_CONTRACT_FREEZE.md`
- 结果报告：`Docs/Reports/2026-08-04-g0-3-schema-api-save-contracts.md`

### 冻结结果

| 范围 | 决定 |
|---|---|
| Content | Schema 6 追加 14 kind、模块引用操作数/6 token、10 Reward op、4 Stat；Schema 1—5 保持兼容 |
| Simulation | 24 项 Demo Pipeline；Owner、同 Tick、Cleanup、5 DamageChannel、状态事务和随机流隔离 |
| Save | Settings 2、Profile 3、RunRecovery 2；Profile v1→2→3/v2→3、Loadout 与幂等结算 |
| API Freeze | M10 五程序集 Hash 不变；G1.1 只允许批准追加，先保存旧 Hash 预期 FAIL diff 再更新 |
| Test/Perf | G1.1—G3.6 证据目录、矩阵、配对短测、容量和回滚顺序 |

### 检查

| 检查 | 结果 | 证据 |
|---|---|---|
| ADR/CR 映射 | PASS | 3 份 Accepted ADR；12 份 Formal CR 均回链；无“待 G0.3” |
| Schema/Pipeline 一致性 | PASS | 两份权威规范均包含 14 kind；Pipeline 精确 24 项 |
| API 基线保护 | PASS | `PUBLIC_API_FREEZE.md` 的 5 个 M10 Hash 原值仍存在，未提前更新 |
| 链接/Markdown/空白 | PASS | 相对链接、围栏、标题、尾随空白综合检查通过 |
| Unity 编译、测试、构建、性能 | NOT RUN | G0.3 只改文档契约，未修改可执行输入；执行矩阵从 G1.1 开始 |

首次一致性校验因 PowerShell 文件筛选写法及一个手抄 Hash 首字符错误而 FAIL；修正校验命令后 PASS，
没有为通过检查改写实际 M10 Hash。

### 下一步

只进入 G0.4：固化正式资产、音频、字体、本地化、性能预算、权利/provenance 和交付清单。
G0.4 不实施代码；G1.1 才能按已批准最大面修改冻结程序集。
