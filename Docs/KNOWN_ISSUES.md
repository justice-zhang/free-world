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
| M0-KI-004 | RESOLVED | Low | Windows Player 冒烟测试进入 MainMenu 并稳定运行 8 秒后被主动终止，进程退出码为 `-1`。 | M10 已通过 `M10ReleaseSmokeRunner` 和 `Scripts/run-player-smoke.ps1` 增加显式退出：Release Player 完成 60 Tick、4 actors、0 invalid handles 后退出码为 `0`。 |
| M0-KI-005 | RESOLVED | None | M0 的 MainMenu 是黑色空场景，没有正式 UI。 | M7 已提供程序化 Placeholder UI、完整页面流和键鼠/手柄导航；正式皮肤仍不属于框架资源。 |
| M0-KI-006 | RESOLVED | None | 性能与 30 分钟 Soak Test 未执行。 | M10 已实际完成 30 分钟、54,000 Tick 的目标规模 Soak，持续保持 1,500 敌人、3,000 投射物和 5,000 拾取物，并输出固定 Checksum。 |
| M0-KI-007 | ACCEPTED | Low | Unity 自动生成的 `.meta` 和 Addressables YAML 含空值尾随空格，完整里程碑 `git diff --check` 会报告这些生成字段。 | 手写 C#、PowerShell、Markdown、JSON 和 asmdef 必须通过 whitespace 检查；不手工批量重写 Unity 序列化文件。 |

当前没有阻止 M1 开始的 `OPEN` 问题。

## M1

| ID | 状态 | 严重度 | 问题与影响 | 处理 |
|---|---|---|---|---|
| M1-KI-001 | RESOLVED | Medium | Character 先验证被引用 Skill 的非 canonical ID 时，错误路径曾指向 Character，无法定位实际错误资产。 | Baker 现在先预验证 Pack 内每个定义自身的 ID 和路径；`ContentBakerTests.AuthoringRejectsNonCanonicalIdWithPackAndAssetPath` 已覆盖。 |
| M1-KI-002 | RESOLVED | High | Catalog、Manifest、运行时定义和 Registry 的 `IReadOnlyList` 曾直接暴露 backing array，可绕过验证并使 Hash 或索引状态失配。 | 构造输入继续 clone，对外返回缓存只读视图；`RuntimeCollectionsDoNotExposeMutableBackingArrays` 已覆盖。 |
| M1-KI-003 | ACCEPTED | Low | M1 Bootstrap 直接引用测试 Pack 的 baked `TextAsset`，尚未实现正式 Addressables Pack 生命周期、异步句柄或 DLC。 | 符合 ADR 0003 的 M1 落地边界；正式内容接入前再实现通用 Pack 加载流程，不在 M1 扩张。 |
| M1-KI-004 | RESOLVED | Medium | M1 只验证 Localization Key 非空，尚未验证 Key 是否存在于 Locale 表。 | M8 已接入 Unity Localization 英文、简中和 Pseudo String Table，并以缺 Key 验证门禁覆盖 103 个非空双语 Key。 |
| M1-KI-005 | RESOLVED | None | M1 未运行 30 分钟性能/Soak Test。 | M10 已以固定种子压力场景完成 30 分钟、54,000 Tick Soak，并输出可复核的性能 JSON 和固定 Checksum。 |

当前没有阻止 M2 开始的 `OPEN` 问题。

## M2

| ID | 状态 | 严重度 | 问题与影响 | 处理 |
|---|---|---|---|---|
| M2-KI-001 | RESOLVED | High | 一次 `FixedTickRunner.Advance` 追赶多个 Tick 时，World 曾在每个 Tick 开始清空事件，表现调用者重新获得控制前会丢失前序 Tick 事件。 | Event Buffer 改为按 Runner 批次清理；`CatchUpAdvanceRetainsEventsFromEveryExecutedTick` 覆盖多 Tick 和零 Tick Advance。 |
| M2-KI-002 | RESOLVED | Medium | Movement 曾把速度阈值同时用于位置积分，极小但非零的合法速度会完全冻结。 | 任意非零速度均积分位置并设置 Moving；`MovementIntegratesAnyNonZeroVelocity` 覆盖。 |
| M2-KI-003 | ACCEPTED | Low | Event Buffer 只保留最近一次实际执行 Tick 的 Runner 批次；消费者若跨过下一批次才读取会错过旧事件。 | 这是 M2 明确的单生产者批次契约；M7 接入表现层时必须在下一 Tick 批次前消费。 |
| M2-KI-004 | ACCEPTED | Low | Spatial Grid 半径和邻近查询结果顺序不是模拟契约。 | 后续需要稳定优先级的系统必须按明确键选择或排序，不得依赖 Dictionary/Cell 链接顺序。 |
| M2-KI-005 | ACCEPTED | Low | Store Handle 只在所属 Store 内有效，裸 Handle 不能跨 Actor/Projectile/Area/Pickup 比较身份。 | 跨 Store 数据必须使用 `SpatialEntity(EntityKind, EntityHandle)`；文档和公共缓冲格式已遵守。 |
| M2-KI-006 | RESOLVED | Medium | 单线程 Dictionary 网格/快照索引尚未完成目标实体规模基准和 30 分钟 Soak。 | M10 目标规模基准和 30 分钟 Soak 已 PASS：Tick p99 10.9851 ms、热路径 0 B 分配、0 GC、无持续内存增长；现有证据不要求迁移 Jobs/Burst。 |

当前没有阻止 M3 开始的 `OPEN` 问题。

## M3

| ID | 状态 | 严重度 | 问题与影响 | 处理 |
|---|---|---|---|---|
| M3-KI-001 | RESOLVED | High | 临时护盾已耗尽时，状态过期只改变最大容量，旧实现不发 `ShieldChanged`，表现层可能保留过期容量。 | 事件现在携带 Current 和 Maximum 的前后值，最大值单独变化也发事件；`ConsumedTemporaryShieldStillEmitsCapacityChangeWhenItExpires` 已覆盖。 |
| M3-KI-002 | RESOLVED | High | 两个各自有限的护盾容量相加可溢出为正无穷，旧实现会接受状态并污染 Shield 数值。 | 聚合结果在任何状态写入和事件产生前验证为有限非负值，失败时原子回滚；`TemporaryShieldApplicationRejectsAggregateCapacityOverflow` 已覆盖。 |
| M3-KI-003 | ACCEPTED | Low | 状态的 Dispel/Immunity 标签匹配当前为线性比较，大量并发状态时可能成为热点。 | M3 保持无每 Tick 临时集合的正确性实现；M10 在目标规模基准证明为热点后才调整索引结构。 |
| M3-KI-004 | ACCEPTED | Low | M3 的每个状态定义只表达一项 Modifier、一项周期效果和一项临时护盾，不是完整效果列表。 | 这是 M3 Placeholder 和最小 Schema 边界；多效果组合只能在 M4 通用技能/效果模块中扩展，不硬编码到伤害系统。 |
| M3-KI-005 | RESOLVED | Medium | M3 未运行 30 分钟 Soak 和 1,500/3,000/5,000 实体压力基准。 | M10 已实际完成 54,000 Tick、1,500/3,000/5,000 目标规模，Tick p99 10.9851 ms、0 B 热路径分配、无持续内存增长；无需迁移 Jobs/Burst。 |

当前没有阻止 M4 开始的 `OPEN` 问题。

## M4

| ID | 状态 | 严重度 | 问题与影响 | 处理 |
|---|---|---|---|---|
| M4-KI-001 | RESOLVED | High | LevelPatch 曾只验证路径和操作数，逐级累积可产生非有限浮点、32 位整数溢出或非法 Effect 参数。 | ContentValidator 现在按等级应用并验证累积结果；float 非有限值和 int 溢出回归测试已覆盖。 |
| M4-KI-002 | RESOLVED | High | SpawnSecondarySkill 曾接受不可执行的旧 Schema Skill，且只预注册一层引用，三级调用链会静默中断。 | 引用必须指向可执行 Skill；secondary 引用递归预注册并通过已有实例去重防环；一层和三级 ProcDepth 测试已覆盖。 |
| M4-KI-003 | RESOLVED | Medium | Actor 删除时 Skill Instance 曾不释放，实例计数与固定 Tick 扫描上界会持续增长，旧句柄也无法安全复用。 | 实例槽增加空闲链表和 ushort 代际；Owner 删除释放全部实例并使旧句柄失效；删除/复用测试已覆盖。 |
| M4-KI-004 | RESOLVED | Medium | Heal resolver 曾直接写 ActorCombatRecord.HealthCurrent，不符合 Skill 不直接修改 Health 的验收边界。 | Health 变更下沉到 ActorStore 的程序集内部 TryApplyHealing；Skill 文件对 HealthCurrent 静态搜索零命中，既有 Heal 行为测试通过。 |
| M4-KI-005 | RESOLVED | Low | OnPickup Trigger 已有纯模拟提交入口，但尚无实际拾取事件生产者。 | M6 已接入敌人死亡奖励拾取物和固定流水线中的实际拾取事件生产，并以 `EnemyDeathCreatesCollectibleExperiencePickupInFixedPipeline` 等测试覆盖 OnPickup 生产与经验结算。 |
| M4-KI-006 | ACCEPTED | Low | Skill Preview 使用固定静止目标和有限窗口，不代表最终移动构筑或高并发性能。 | 仅将 Preview 作为固定种子 DPS、命中和触发次数回归工具；不把结果外推为性能预算。 |
| M4-KI-007 | RESOLVED | Medium | M4 未运行 30 分钟 Soak 和 1,500/3,000/5,000 实体压力基准。 | M10 正式与干净克隆目标规模 Soak 均 PASS；固定 Checksum 一致，未发现需要 Jobs/Burst 的预算超限热点。 |
| M4-KI-008 | ACCEPTED | Low | 最终 Validation 日志中 Unity 公共配置请求出现 Curl 42/超时，同时启动阶段有已知 LicenseClient 握手噪声。 | 脚本仍以 exit 0 完成并输出 `[Project Validation] PASS`；测试 XML、Build PASS 标记和 `Succeeded` Manifest 均独立有效。若后续缺少任一门禁证据，必须按 FAIL 处理。 |

当前没有阻止 M5 开始的 `OPEN` 问题。

## M5

| ID | 状态 | 严重度 | 问题与影响 | 处理 |
|---|---|---|---|---|
| M5-KI-001 | ACCEPTED | Low | ChunkedInfiniteMapRuntime 当前只维护确定性区块签名和逻辑活动窗口，不含正式地形内容流送、区块存档或表现对象池。 | 这是 M5 提示词要求的最小版本；后续表现/内容工具里程碑通过现有 IMapRuntime 边界扩展，不把流送逻辑写入 Scene。 |
| M5-KI-002 | ACCEPTED | Low | 障碍输入仅支持轴对齐矩形和滑轴回退，复杂静态几何不会生成全局路径。 | 普通敌人继续使用 Steering、局部分离和轻量规避；只有证据证明需要时才提交通用寻路 Change Request。 |
| M5-KI-003 | RESOLVED | Low | VisualProfileId 已是稳定表现边界 ID，但具体 Profile 内容和运行时 View 解析尚未实现。 | M7 通过 RunSession 只读边界解析敌人 VisualProfileId，Presentation Catalog 匹配；缺失时使用程序化 fallback，Simulation 不持有 Unity Object。 |
| M5-KI-004 | RESOLVED | Medium | M5 未运行 30 分钟 Soak 和 1,500/3,000/5,000 目标实体压力基准。 | M10 使用生产 EnemyRuntime/稠密 Store 实际保持 1,500/3,000/5,000，54,000 Tick 后无效句柄为零。 |

当前没有阻止 M6 开始的 `OPEN` 问题。

## M6

| ID | 状态 | 严重度 | 问题与影响 | 处理 |
|---|---|---|---|---|
| M6-KI-001 | RESOLVED | High | Unity `JsonUtility` 会把不适用于某类 Synergy Output 的嵌套 DTO 恢复为空对象，旧解析器按非 null 判断并尝试解析空 Effect/Modifier，导致有效 baked Pack 验证失败。 | DTO 按 Output Type 只解析对应字段；真实 `JsonUtility` round-trip 与 Project Validation 已覆盖。 |
| M6-KI-002 | RESOLVED | High | 多个具体 ScriptableObject 类型最初与共享作者类放在同一文件，Unity 生成 Placeholder 时无法稳定解析脚本，资产可能出现 `m_Script: 0`。 | Passive/Trait/Synergy/Evolution/UpgradeOffer 具体类已拆分到同名文件；重新生成 Pack 后无缺失脚本，验证 PASS。 |
| M6-KI-003 | ACCEPTED | Low | Synergy 在条件首次满足时一次性激活并锁存；后续因 Evolution 消费或替换而不再满足条件时，不撤销已应用的 Modifier、Effect、Trait 或 Unlock。 | M6 将条件定义为激活条件并保证输出只应用一次，避免反复抖动；需要可撤销联动时必须先提出通用 Schema/生命周期 Change Request。 |
| M6-KI-004 | RESOLVED | Low | M6 没有升级 UI，只有应用层 `RunSession` 命令接口和 `UpgradeOfferSet`。 | M7 LevelUpDraft 只展示 UI-safe 候选投影并提交 Select/Skip/Reroll 命令；过滤、权重和构筑规则仍在 Simulation。 |
| M6-KI-005 | ACCEPTED | Low | 10 分钟自动玩家 Harness 使用小型 Placeholder Encounter，不输出 Tick 分位、GC 或内存趋势。 | 只把结果用于成长链路、确定性和显式清理证明，不外推为目标规模性能结论。 |
| M6-KI-006 | RESOLVED | Medium | M6 未运行 30 分钟 Soak 和 1,500 敌人、3,000 投射物、5,000 拾取物压力基准。 | M10 正式 54,000 Tick 与提交后干净克隆完整基准均 PASS。 |

当前没有阻止 M7 开始的 `OPEN` 问题。

## M7

| ID | 状态 | 严重度 | 问题与影响 | 处理 |
|---|---|---|---|---|
| M7-KI-001 | RESOLVED | Low | Placeholder UI 当前显示 Localization Key，不包含正式语言表、字体回退或伪本地化裁切证据。 | M8 已接入英文、简中、Pseudo String Table，运行时解析 Key、使用 Windows CJK 动态字体候选，并以 EditMode/PlayMode 验证三语言和中文字符覆盖。 |
| M7-KI-002 | ACCEPTED | Low | Projectile、Area、Pickup 和玩家当前没有实例级 VisualProfileId，因而使用 EntityKind 程序化 fallback。 | 敌人已消费稳定 VisualProfileId；未来若需要实例级外观，先扩展通用只读 Snapshot 身份，不把 Unity Object 放入 Simulation。 |
| M7-KI-003 | RESOLVED | Medium | 四类 View/VFX/Audio/伤害数字已池化，但尚未在 1,500/3,000/5,000 目标规模测量池命中、GC 和帧时间。 | M10 的 200 VFX 容量探针达到峰值 200，2,700,000 次池命中，0 失败/丢弃；渲染 CPU p99 1.2482 ms，GC 为 0。 |
| M7-KI-004 | ACCEPTED | Low | Debug Action Map 在框架开发阶段始终启用，包含触发一次真实升级流程和完成测试局的入口。 | 仅用于 Placeholder 验收；Release 配置门禁在发布前必须禁用 Debug Map。 |

当前没有阻止 M8 开始的 `OPEN` 问题。

## M8

| ID | 状态 | 严重度 | 问题与影响 | 处理 |
|---|---|---|---|---|
| M8-KI-001 | ACCEPTED | Low | `run_recovery.json` 当前在开局记录 Seed、角色、地图和初始技能，结算时删除；尚未周期保存完整局中 Snapshot，也没有“继续本局”UI。 | M8 交付恢复文档、迁移、校验和缺失内容拒绝边界；在定义完整可重建 Snapshot 前不得假称任意时刻恢复。 |
| M8-KI-002 | ACCEPTED | Low | Cloud 只有 Revision、冲突分类和 Null 服务，没有远端传输、用户冲突选择页面或真实 Steam SDK。 | 符合 M8 禁止集成真实 SDK 的范围；后续平台任务通过 `ICloudSyncService` 接入，本地文件始终是真值。 |
| M8-KI-003 | ACCEPTED | Low | Placeholder UI 的简中覆盖依赖 Windows 已安装的微软雅黑/黑体等系统字体候选，不随游戏分发正式字体资产。 | 首发目标 Windows x64 的 PlayMode 已验证中文字符；正式品牌字体必须在来源、嵌入许可和 fallback 完成后单独导入。 |
| M8-KI-004 | RESOLVED | Medium | 低频存档 I/O、Localization、平台边界未执行 30 分钟 Soak 或目标实体规模性能基准。 | M10 完整测试、Validation、目标规模 Soak 和无 Steam 的 Release Player 均 PASS；低频边界未进入固定 Tick。 |

当前没有阻止 M9 开始的 `OPEN` 问题。

## M9

| ID | 状态 | 严重度 | 问题与影响 | 处理 |
|---|---|---|---|---|
| M9-KI-001 | ACCEPTED | Low | Content Creation Wizard 固定生成程序化 Placeholder、占位双语正文和 `provenance.placeholder.json`，不能直接转为正式发布内容。 | 正式内容仍必须单独完成翻译、资产 provenance、商业许可复核并移除 Placeholder/Development 标签；Release 门禁不可绕过。 |
| M9-KI-002 | ACCEPTED | Low | Wave Timeline 是作者数据理论产出，Skill Preview 使用固定静止目标；两者不代表最终数值平衡或高并发性能。 | 只用于相同输入的设计回归；目标规模与长时间数据由 M10 性能 Harness 输出。 |
| M9-KI-003 | ACCEPTED | Low | Content Pack Builder 当前输出未签名的 loose JSON Catalog 和审计报告，不提供远端发布、DLC 下载、签名或 Workshop 生命周期。 | 保持 M9 内容生产/审计范围；正式分发后端必须复用稳定 Pack/Hash 边界并单独审批。 |
| M9-KI-004 | RESOLVED | Medium | M9 未执行 30 分钟 Soak 和 1,500 敌人、3,000 投射物、5,000 拾取物目标规模测试。 | M10 正式和干净克隆性能 JSON 均 PASS，固定 Checksum 为 `13193d7c4cc3251a`。 |

当前没有阻止 M10 开始的 `OPEN` 问题。

## M10

| ID | 状态 | 严重度 | 问题与影响 | 处理 |
|---|---|---|---|---|
| M10-KI-001 | ACCEPTED | Low | Headless 性能 JSON 的 Render 指标是 Null Device 下的 Snapshot 插值与池化 VFX CPU 探针，不是正式内容的 GPU Frame Time。 | 指标名称和环境已在 JSON 中明确；正式美术/Shader/目标 GPU 到位后需另跑 GPU/RenderDoc 基线，不得把本数据外推为正式内容渲染预算。 |
| M10-KI-002 | ACCEPTED | Low | 成功 Release 是内容为空的框架管线验证，不包含可销售正式内容。 | 实际 Scene/Addressables 输入通过门禁且 Placeholder 为 0；正式内容仍必须完成 provenance、许可证、本地化与 Release 标签审查。 |
| M10-KI-003 | ACCEPTED | Low | 自托管 GitHub Actions 已触发运行 `#30338477997`，但 `framework-freeze` Job 等待约 24 小时后取消，实际执行步骤为 0。 | GitHub CI 门禁仍为 `NOT RUN`；等价的提交后独立干净克隆完整流水线已 PASS。需配置并激活带 `self-hosted, Windows, X64, unity` 标签且预激活 Unity 的 Runner 后重新运行。 |
| M10-KI-004 | RESOLVED | Medium | 首次干净克隆 Development Build 在 264 字符 Addressables 路径上完成 Player 后，Manifest 哈希读取失败。 | Windows 哈希使用扩展长路径，默认临时目录缩短；新提交的第二次完整干净克隆两个 Build 与 Player 均 PASS。 |

当前没有阻止框架冻结的 `OPEN` 问题。

## Qinglan Demo

| ID | 状态 | 严重度 | 问题与影响 | 处理 |
|---|---|---|---|---|
| QD-KI-001 | RESOLVED | High | CR-01—CR-11 曾未形成正式决定，G1/G2 多项核心能力被阻塞。 | G0.2 已形成 12 份正式 CR：CR-01—09 接受、CR-10 拆为属性/伤害策略两项接受、CR-11 延期；决定见 `DemoDevelopment/07_CHANGE_REQUEST_DECISIONS.md`。 |
| QD-KI-002 | ACCEPTED | Medium | Demo 设计提交尚未合并 `main`；用户要求在单一新分支连续开发，与路线文档默认“每包独立分支并先合并”不同。 | 以 `codex/qinglan-demo-implementation` 为唯一 Owner 分支，每个工作包单独提交并 Push；未经新授权不自动合并 `main` 或打标签。 |
| QD-KI-003 | PLANNED | High | 正式角色、敌人、地图、UI、VFX、音频、字体和商业本地化尚无实际文件、完整 provenance、许可证和目标硬件证据。 | G0.4 已由 `DemoDevelopment/09_G0_4_ASSET_PRODUCTION_PLAN.md` 固化 41 批生产/预算/权利清单；G3 仅导入可审计资产，任何缺失都阻断 Release。 |
| QD-KI-004 | RESOLVED | High | 已接受 CR 曾未形成 ADR、Schema 6、Profile Schema 3、公共 API Freeze、迁移和测试契约。 | G0.3 已由 ADR 0013—0015 和 `DemoDevelopment/08_G0_3_CONTRACT_FREEZE.md` 固化全部契约；现有 Hash 保持不变直到 G1.1 实现门禁。 |
| QD-KI-005 | ACCEPTED | Low | CR-11 完整 Run Recovery 延期，Demo 不支持任意 Tick 继续本局。 | 只检测不完整记录、显示本地化提示并在明确开始新局后清理；不得显示 Continue 或把不完整 Run 结算为胜利。 |
| QD-KI-006 | RESOLVED | High | Schema 6、Demo Pipeline、Profile 3 和批准公共 API 曾只有契约，尚未实现或取得新 Freeze Hash 证据。 | G1.1 已实现通用骨架/Codec/Migration/Fixture，保留旧 Hash 预期差异并完成 203 EditMode、9 PlayMode、Validation、配对性能短测和 Windows x64 Development Build；新 Hash 见 `PUBLIC_API_FREEZE.md`。 |
| QD-KI-007 | PLANNED | High | 当前 `AssetProvenanceValidator` 只主动扫描 AI 目录，FirstParty 正式资产尚无等价自动 provenance/Hash 门禁。 | G3.1 在任何 FirstParty 文件取得 `release` 标签前，把校验扩展到全部实际 Release 输入并补负向测试；仅有 sidecar 文档不能关闭。 |
| QD-KI-008 | PLANNED | Medium | Noto CJK SC 只锁定官方候选和 OFL 1.1 许可路径，尚未固定发布版本、下载文件、SHA-256、Notice 或 TMP 缺字证据。 | G3.3 按官方发布固定版本/Hash，保存 LICENSE、登记每个路径并做简中/英文/Pseudo 缺字与裁切；任一缺失阻断 Release。 |
| QD-KI-009 | RESOLVED | Medium | G1.4 的六个锁定 Evolution Offer 曾缺少独立候选、回退、暂停和幂等事务。 | G1.7 已按 CR-2026-007 / ADR 0018 实现 Reward Choice、Reward RNG、BuildState 再验证、fallback 和 RunSession 暂停/恢复；普通 Level-up 流回归通过。 |
| QD-KI-010 | RESOLVED | Medium | G1.5 已把异相灵核 Reward 绑定到四个精英词缀并执行有限 `SpawnEnemy` 死亡输出，但 AddCurrency/奇物三选一、暂停、回退和幂等提交仍未由 RewardResolution 消费。 | G2.3 已完成异相灵核地面来源、三槽奇物选择、灵砂回退、活动/已提交事务幂等和 Application 暂停/恢复；专项与完整回归均 PASS。 |
| QD-KI-011 | PLANNED | High | G1.6 Encounter 已完成九段普通敌群与两个固定精英，但没有折枝/听风 BossDefinition、Boss Phase 或 BossRule，实际地图出生公平和 Boss 过渡也未验证。 | G2.2 追加两个 Boss 与一次性规则；G2.6/G2.8 在实际地图 PlayMode 验证出生保护、压力可读和过渡清理。在此之前“两 Boss 一次”和实机公平保持 `NOT RUN`。 |
| QD-KI-012 | PLANNED | Medium | G1.7 曾只有受控 Evolution 选择适配器；G2.3 已完成显化宝匣、Boss/精英消费者和 fallback，但实际选择页面、键鼠/手柄输入与可访问性尚不存在。 | G2.6 通过现有 `RunSession.CurrentRewardChoice` 接入实际 UI/输入；在此之前只能宣称模拟与 Application 命令闭环，不能宣称玩家可见闭环。 |
| QD-KI-013 | PLANNED | High | G2.3 的 Currency/Unique/Unlock/Story 只形成局内 `RewardResultEntry`，尚未进入 Profile v3 原子事务、保存重试或平台事件。 | G2.4 汇总胜负 RunResult，G2.5 以同一 Outcome 事务原子合并并持久化；Simulation 不得直接写 Profile。 |

当前没有阻止 G2.4 RunResult 开始的 `OPEN` 问题；QD-KI-012/013 在 G2.6/G2.5 前阻止玩家可见与持久化
奖励闭环，QD-KI-011 在 G2.8 前阻止完整 Encounter 验收，QD-KI-003/007/008 继续阻止 Release。
