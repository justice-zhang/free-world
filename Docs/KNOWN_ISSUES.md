# 已知问题

状态定义：

- `OPEN`：尚未解决，可能影响当前工作。
- `ACCEPTED`：当前里程碑允许的限制，已明确影响和后续处理阶段。
- `PLANNED`：已分配到后续里程碑。
- `RESOLVED`：已修复并有验证证据。

## M0

| ID | 状态 | 严重度 | 问题与影响 | 处理 |
|---|---|---|---|---|
| M0-KI-001 | RESOLVED | High | Unity 在 `Library/SourceAssetDB` 无法打开时可能返回 0 且不生成测试 XML，旧脚本会误报成功。 | `Scripts/test.ps1` 现在删除旧结果，并要求 XML 存在、可解析且结果为 Passed；缺失或无效结果返回 4，测试失败返回 5。 |
| M0-KI-002 | RESOLVED | Low | 打开的 Office 文档会产生未跟踪的 `~$*.docx` 临时锁文件，污染 Git 状态。 | `.gitignore` 精确忽略 `~$*.docx`，不删除用户正在使用的临时文件。 |
| M0-KI-003 | RESOLVED | High | Unity CLI 日志偶尔包含 Licensing Client 握手或令牌刷新错误，失败启动甚至可能返回 0。 | 三个脚本均清除对应旧结果；测试要求有效 Passed XML，验证要求 PASS 标记，构建要求新 EXE、PASS 标记和有效 Build Manifest。缺少任一证据必须判定 FAIL。 |
| M0-KI-004 | ACCEPTED | Low | Windows Player 冒烟测试进入 MainMenu 并稳定运行 8 秒后被主动终止，进程退出码为 `-1`。 | 该值代表测试主动关闭，不是崩溃；日志不得出现未处理异常。后续自动化应增加显式退出命令。 |
| M0-KI-005 | ACCEPTED | None | M0 的 MainMenu 是黑色空场景，没有正式 UI。 | 符合 M0 禁止提前实现玩法和正式菜单的范围；表现与 UI 在 M7 实现。 |
| M0-KI-006 | PLANNED | None | 性能与 30 分钟 Soak Test 未执行。 | M0 无正式模拟负载；在模拟内核和压力场景具备后按性能里程碑执行。 |
| M0-KI-007 | ACCEPTED | Low | Unity 自动生成的 `.meta` 和 Addressables YAML 含空值尾随空格，完整里程碑 `git diff --check` 会报告这些生成字段。 | 手写 C#、PowerShell、Markdown、JSON 和 asmdef 必须通过 whitespace 检查；不手工批量重写 Unity 序列化文件。 |

当前没有阻止 M1 开始的 `OPEN` 问题。

## M1

| ID | 状态 | 严重度 | 问题与影响 | 处理 |
|---|---|---|---|---|
| M1-KI-001 | RESOLVED | Medium | Character 先验证被引用 Skill 的非 canonical ID 时，错误路径曾指向 Character，无法定位实际错误资产。 | Baker 现在先预验证 Pack 内每个定义自身的 ID 和路径；`ContentBakerTests.AuthoringRejectsNonCanonicalIdWithPackAndAssetPath` 已覆盖。 |
| M1-KI-002 | RESOLVED | High | Catalog、Manifest、运行时定义和 Registry 的 `IReadOnlyList` 曾直接暴露 backing array，可绕过验证并使 Hash 或索引状态失配。 | 构造输入继续 clone，对外返回缓存只读视图；`RuntimeCollectionsDoNotExposeMutableBackingArrays` 已覆盖。 |
| M1-KI-003 | ACCEPTED | Low | M1 Bootstrap 直接引用测试 Pack 的 baked `TextAsset`，尚未实现正式 Addressables Pack 生命周期、异步句柄或 DLC。 | 符合 ADR 0003 的 M1 落地边界；正式内容接入前再实现通用 Pack 加载流程，不在 M1 扩张。 |
| M1-KI-004 | PLANNED | Medium | M1 只验证 Localization Key 非空，尚未验证 Key 是否存在于 Locale 表。 | Unity Localization 表、伪本地化和缺 Key 门禁在 M8 实现。 |
| M1-KI-005 | PLANNED | None | M1 未运行 30 分钟性能/Soak Test。 | M1 无模拟负载；固定种子压力场景和性能 JSON 在 M10 执行。 |

当前没有阻止 M2 开始的 `OPEN` 问题。

## M2

| ID | 状态 | 严重度 | 问题与影响 | 处理 |
|---|---|---|---|---|
| M2-KI-001 | RESOLVED | High | 一次 `FixedTickRunner.Advance` 追赶多个 Tick 时，World 曾在每个 Tick 开始清空事件，表现调用者重新获得控制前会丢失前序 Tick 事件。 | Event Buffer 改为按 Runner 批次清理；`CatchUpAdvanceRetainsEventsFromEveryExecutedTick` 覆盖多 Tick 和零 Tick Advance。 |
| M2-KI-002 | RESOLVED | Medium | Movement 曾把速度阈值同时用于位置积分，极小但非零的合法速度会完全冻结。 | 任意非零速度均积分位置并设置 Moving；`MovementIntegratesAnyNonZeroVelocity` 覆盖。 |
| M2-KI-003 | ACCEPTED | Low | Event Buffer 只保留最近一次实际执行 Tick 的 Runner 批次；消费者若跨过下一批次才读取会错过旧事件。 | 这是 M2 明确的单生产者批次契约；M7 接入表现层时必须在下一 Tick 批次前消费。 |
| M2-KI-004 | ACCEPTED | Low | Spatial Grid 半径和邻近查询结果顺序不是模拟契约。 | 后续需要稳定优先级的系统必须按明确键选择或排序，不得依赖 Dictionary/Cell 链接顺序。 |
| M2-KI-005 | ACCEPTED | Low | Store Handle 只在所属 Store 内有效，裸 Handle 不能跨 Actor/Projectile/Area/Pickup 比较身份。 | 跨 Store 数据必须使用 `SpatialEntity(EntityKind, EntityHandle)`；文档和公共缓冲格式已遵守。 |
| M2-KI-006 | PLANNED | Medium | 单线程 Dictionary 网格/快照索引尚未完成目标实体规模基准和 30 分钟 Soak。 | M2 保持正确性优先；M10 使用固定种子压力场景输出性能 JSON，再依据证据决定 Jobs/Burst 后端。 |

当前没有阻止 M3 开始的 `OPEN` 问题。

## M3

| ID | 状态 | 严重度 | 问题与影响 | 处理 |
|---|---|---|---|---|
| M3-KI-001 | RESOLVED | High | 临时护盾已耗尽时，状态过期只改变最大容量，旧实现不发 `ShieldChanged`，表现层可能保留过期容量。 | 事件现在携带 Current 和 Maximum 的前后值，最大值单独变化也发事件；`ConsumedTemporaryShieldStillEmitsCapacityChangeWhenItExpires` 已覆盖。 |
| M3-KI-002 | RESOLVED | High | 两个各自有限的护盾容量相加可溢出为正无穷，旧实现会接受状态并污染 Shield 数值。 | 聚合结果在任何状态写入和事件产生前验证为有限非负值，失败时原子回滚；`TemporaryShieldApplicationRejectsAggregateCapacityOverflow` 已覆盖。 |
| M3-KI-003 | ACCEPTED | Low | 状态的 Dispel/Immunity 标签匹配当前为线性比较，大量并发状态时可能成为热点。 | M3 保持无每 Tick 临时集合的正确性实现；M10 在目标规模基准证明为热点后才调整索引结构。 |
| M3-KI-004 | ACCEPTED | Low | M3 的每个状态定义只表达一项 Modifier、一项周期效果和一项临时护盾，不是完整效果列表。 | 这是 M3 Placeholder 和最小 Schema 边界；多效果组合只能在 M4 通用技能/效果模块中扩展，不硬编码到伤害系统。 |
| M3-KI-005 | PLANNED | Medium | M3 未运行 30 分钟 Soak 和 1,500/3,000/5,000 实体压力基准。 | 当前性能数据为 `NOT RUN`；M10 使用固定种子压力场景输出性能 JSON，再依证据决定 Jobs/Burst 后端。 |

当前没有阻止 M4 开始的 `OPEN` 问题。

## M4

| ID | 状态 | 严重度 | 问题与影响 | 处理 |
|---|---|---|---|---|
| M4-KI-001 | RESOLVED | High | LevelPatch 曾只验证路径和操作数，逐级累积可产生非有限浮点、32 位整数溢出或非法 Effect 参数。 | ContentValidator 现在按等级应用并验证累积结果；float 非有限值和 int 溢出回归测试已覆盖。 |
| M4-KI-002 | RESOLVED | High | SpawnSecondarySkill 曾接受不可执行的旧 Schema Skill，且只预注册一层引用，三级调用链会静默中断。 | 引用必须指向可执行 Skill；secondary 引用递归预注册并通过已有实例去重防环；一层和三级 ProcDepth 测试已覆盖。 |
| M4-KI-003 | RESOLVED | Medium | Actor 删除时 Skill Instance 曾不释放，实例计数与固定 Tick 扫描上界会持续增长，旧句柄也无法安全复用。 | 实例槽增加空闲链表和 ushort 代际；Owner 删除释放全部实例并使旧句柄失效；删除/复用测试已覆盖。 |
| M4-KI-004 | RESOLVED | Medium | Heal resolver 曾直接写 ActorCombatRecord.HealthCurrent，不符合 Skill 不直接修改 Health 的验收边界。 | Health 变更下沉到 ActorStore 的程序集内部 TryApplyHealing；Skill 文件对 HealthCurrent 静态搜索零命中，既有 Heal 行为测试通过。 |
| M4-KI-005 | ACCEPTED | Low | OnPickup Trigger 已有纯模拟提交入口，但尚无实际拾取事件生产者。 | M4 只交付并测试 Trigger 契约；实际 Pickup 流程在后续对应里程碑接入，不在 M4 扩张。 |
| M4-KI-006 | ACCEPTED | Low | Skill Preview 使用固定静止目标和有限窗口，不代表最终移动构筑或高并发性能。 | 仅将 Preview 作为固定种子 DPS、命中和触发次数回归工具；不把结果外推为性能预算。 |
| M4-KI-007 | PLANNED | Medium | M4 未运行 30 分钟 Soak 和 1,500/3,000/5,000 实体压力基准。 | 当前为 `NOT RUN`；M10 使用固定种子压力场景输出性能 JSON 后再决定 Jobs/Burst 优化。 |
| M4-KI-008 | ACCEPTED | Low | 最终 Validation 日志中 Unity 公共配置请求出现 Curl 42/超时，同时启动阶段有已知 LicenseClient 握手噪声。 | 脚本仍以 exit 0 完成并输出 `[Project Validation] PASS`；测试 XML、Build PASS 标记和 `Succeeded` Manifest 均独立有效。若后续缺少任一门禁证据，必须按 FAIL 处理。 |

当前没有阻止 M5 开始的 `OPEN` 问题。

## M5

| ID | 状态 | 严重度 | 问题与影响 | 处理 |
|---|---|---|---|---|
| M5-KI-001 | ACCEPTED | Low | ChunkedInfiniteMapRuntime 当前只维护确定性区块签名和逻辑活动窗口，不含正式地形内容流送、区块存档或表现对象池。 | 这是 M5 提示词要求的最小版本；后续表现/内容工具里程碑通过现有 IMapRuntime 边界扩展，不把流送逻辑写入 Scene。 |
| M5-KI-002 | ACCEPTED | Low | 障碍输入仅支持轴对齐矩形和滑轴回退，复杂静态几何不会生成全局路径。 | 普通敌人继续使用 Steering、局部分离和轻量规避；只有证据证明需要时才提交通用寻路 Change Request。 |
| M5-KI-003 | ACCEPTED | Low | VisualProfileId 已是稳定表现边界 ID，但具体 Profile 内容和运行时 View 解析尚未实现。 | M7 实现 View Pool/表现桥接时消费该 ID；Simulation 不持有 Unity Object。 |
| M5-KI-004 | PLANNED | Medium | M5 未运行 30 分钟 Soak 和 1,500/3,000/5,000 目标实体压力基准。 | 五分钟 Headless 只证明小型 Encounter 的正确性和有界清理；目标规模与性能 JSON 在 M10 执行，当前不得描述为通过。 |

当前没有阻止 M6 开始的 `OPEN` 问题。
