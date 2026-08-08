# 测试计划

## 1. EditMode

必须覆盖：

- ContentId 格式、相等、序列化和重复检测

- 内容包依赖拓扑排序与循环检测

- 作者数据烘焙与引用解析

- EntityHandle Generation 安全

- 固定 Tick 调度顺序

- RandomStream 固定种子复现

- 空间网格插入、查询、移动和删除

- 属性修正顺序

- 暴击、护甲、护盾、生命结算

- 状态叠层、刷新、替换和独立实例

- ProcDepth 限制

- 技能 LevelPatch

- 进化与联动条件

- 升级候选池和固定种子

- 存档原子写入、校验和与迁移

- 缺失内容恢复

## 2. PlayMode

完整流程：

> Bootstrap  
> -\> Main Menu  
> -\> Character Select  
> -\> Map Select  
> -\> Load Run  
> -\> Move  
> -\> Kill Enemy  
> -\> Collect XP  
> -\> Open Level Up  
> -\> Select Upgrade  
> -\> Pause  
> -\> Resume  
> -\> End Run  
> -\> Show Result  
> -\> Save Profile

M7/G2.6 自动化覆盖：

- 键盘和手柄分别完成标题/选择/Run/升级/暂停/结算/据点/再次出发

- UI 打开时 Gameplay 输入被禁用

- 暂停只停止模拟

- View 释放后无悬空绑定

- 缺失正式资源时使用程序化占位

- Snapshot 插值、四类 View 绑定/回收、Generation 失效拒绝

- Hit/Death/Status 表现请求、VFX/伤害数字回收和共享 Canvas

- 场景释放后无 View、池 Owner 或输入订阅残留

- 地图/设置/升级/奖励覆盖层禁用 Gameplay，关闭后立即恢复正确 Action Map

- 手柄断开自动暂停并恢复到可见、启用控件；鼠标滚轮/点击进入相同 UI 命令入口

- 卡牌显示行为描述、目标等级、类型标签、构筑关系和显化资格；配置写入前显示确认页

- 100/125/150% 字体、五种色觉模式、形状/方向危险通道和关闭伤害数字组合

M8 自动化覆盖：

- 无 Steam 环境从 Bootstrap 到设置、开局、结算的完整本地流程。
- 英文、简体中文和扩展伪本地化解析；运行时 CJK 字体覆盖。
- 设置离开页面时保存，开局创建恢复文件，结算保存 Profile 并删除恢复文件。

## 3. Headless Soak Test

- 固定种子

- 自动移动和升级

- 连续运行 30 分钟

- 检查 NaN、无效句柄、未清理事件、实体上限和内存趋势

- 输出性能 JSON

## 4. 扩展性验收

创建以下内容时不修改核心程序集：

> test.character.second  
> test.skill.second  
> test.map.second

只通过内容资产、地图场景和烘焙工具完成。验证它们出现在选择界面、可运行、可保存并通过内容验证。

## 5. 测试纪律

- 修复 Bug 前先添加能复现问题的失败测试。

- 不允许通过删除测试或放宽断言完成修复。

- 测试名称说明行为，不只说明方法名。

- 随机测试必须记录种子。

- 无法在自动化环境运行的项目，必须提供可重复的手工步骤和日志，但不得声称自动测试已通过。

## 6. M1 已落地覆盖

- `ContentId` 有效/无效、大小写规范化、字符串序列化和已知 Hash 碰撞。
- Pack 稳定拓扑排序、缺失依赖、循环和依赖版本不兼容。
- 作者 ScriptableObject 烘焙、纯运行时字段审计、JSON round-trip 和确定性 Hash。
- 重复 ID 同时报告两侧来源，缺失引用报告 owner ID、Pack 和资产路径。
- 非 canonical 被引用资产仍报告该资产自身路径；运行时集合不暴露可变 backing array。
- Registry 同加载顺序索引稳定，并接受无需类型分支的新定义子类。
- Bootstrap 加载一个测试 Pack、四个定义并进入空 MainMenu。

## 7. M2 已落地覆盖

- 30 Hz 固定 Tick 在不同表现 Delta 切分下得到相同 Tick 数和运动结果。
- 最大追赶 Tick 保留积压；暂停忽略 Delta；暂停单步恰好推进一次。
- 同一次追赶 Advance 保留所有已执行 Tick 的事件，零 Tick Advance 不提前清空。
- 任意非零速度都会积分位置并设置 Moving，不用阈值冻结合法运动。
- 删除后旧 Handle 失效，Slot 复用时 Generation 改变，旧 Handle 不能读写新实体。
- Swap-back 后被移动实体的 Handle 仍解析到正确状态；Store 扩容和 Free List 复用。
- M2 默认 Pipeline 的四个系统顺序和自定义测试 Pipeline 的实际调用顺序。
- Spatial Grid 半径查询与暴力结果一致，并覆盖跨 Cell 更新、删除和邻近查询。
- 相同 RandomStream 种子重复、派生流不受父流调用顺序影响。
- 命令只由 Cleanup 应用，生命周期删除同步 Store、网格、事件和诊断计数。
- RenderSnapshot 保存前后位置、朝向、状态标记并可插值。
- Headless Harness 固定种子摘要重复，并验证不创建 GameObject。

## 8. M3 已落地覆盖

- 14 个稳定 StatId 与 Runtime StatIndex 映射；Modifier 六阶段顺序、Priority、紧凑
  StackingGroup、同优先级最新项和过期回退。
- 属性 Evaluate 热路径 IL 不调用 ContentId/string 比较，也不包含正常路径托管分配指令。
- Actor slot 复用战斗记录及 Stat/Modifier/Status 数组；默认零生命初始化被原子拒绝。
- 暴击、Armor、Resistance、True Damage、单包边界、Shield→Health 顺序和固定种子复现。
- 无效目标安全失败、ProcDepth 截断计数、同实体多次致死只发一个 EntityDied。
- RefreshDuration、AddStacks、ReplaceIfStronger、IndependentInstances 四种状态策略。
- 状态周期、短持续边界、过期、驱散、免疫、死亡后不 Tick 和周期 ProcDepth 截断。
- 状态行为来自 RuntimeStatusDefinition，申请 API 不允许覆盖；非法/非有限行为安全拒绝。
- 临时护盾刷新不重复扩容；过期回收容量，即使当前护盾已耗尽也产生包含容量差值的
  ShieldChanged；有限容量聚合溢出时原子拒绝申请。
- Tick 内结构体事件在 catch-up 批次累积，下一实际批次清空；自定义 Pipeline 不会漏 Flush。
- Schema 1/2 兼容、Status 作者数据/DTO round-trip、稳定 wire token、确定性 Hash、非法字段
  验证和 Runtime Definition 无 Unity Object。

完整 30 分钟 Soak 和 1,500/3,000/5,000 实体压力 JSON 仍按性能预算在 M10 门禁启用；
M3 未引入 Jobs、Burst 或新的第三方运行时依赖。

## 9. M4 已落地覆盖

- Timer、OnHit、OnKill、OnDamageTaken、OnPickup、OnStatusApplied 六种 Trigger 的匹配、
  冷却与触发计数。
- Self、Nearest、Random、Circle、Cone、Line、Ring、RandomPointAroundPlayer 在 SpatialGrid
  上的几何结果、稳定排序和固定种子选择。
- Projectile、Area、Aura、Orbit 的 Cleanup 创建、移动/跟随、重复命中、扫掠碰撞、过期
  和实体删除；Instant 直接命令路径由 Effect 测试覆盖。
- Damage/ApplyStatus 进入 M3 请求管线；Heal、RemoveStatus、Knockback、Pull、ModifyStat、
  SpawnSecondarySkill、GrantShield、GainResource 的统一命令解析。
- Schema 3 作者数据和 JSON round-trip、确定性 Hash、Runtime Definition 无 Unity Object、
  缺失模块 ID、错误内容引用类型、SpawnSecondarySkill 可执行 Skill 引用，以及 LevelPatch
  路径/下标/类型、连续等级、浮点非有限累积结果和整数溢出验证。
- LevelPatch 的 Add/Multiply/Override 累积结果；同一 Compiled Definition 被两个角色的独立
  Skill Instance 复用；Owner 删除时实例释放、代际句柄失效与槽位安全复用；多级二次技能
  递归注册、ProcDepth 逐级传播与上限截断。
- 单体投射物、环绕物、地面区域和伤害光环四个 Placeholder Fixture 在固定种子预览中产生
  稳定 DPS、命中数和触发次数，且不需要专用 MonoBehaviour。

完整 30 分钟 Soak、实体压力和性能预算对比仍按 M10 门禁执行；M4 未引入 Jobs、Burst 或
第三方运行时依赖。

## 10. M5 已落地覆盖

- Schema 4 Enemy/Map/Encounter 作者数据、DTO round-trip、确定性 Hash、旧 Schema Enemy
  拒绝规则，以及同一 Encounter 被有限/无限两种 Map Definition 复用。
- Chase、KeepDistance、Charge Windup/Execute、Ranged Attack 状态转换；重叠敌人的 Steering、
  局部分离和障碍规避保持有限数值。
- Ring、Edge、Cluster、Line、Ambush、Portal、FixedAnchor、OffscreenRandom 八种生成图样在
  有限地图产生合法位置。
- Encounter 的预算、阶段插值、权重、群组、Elite、Boss 一次性规则和全局/阶段并发上限。
- FiniteArena 的边界/障碍 Walkable 与 ResolveMovement；ChunkedInfinite 的固定种子区块签名。
- 相同固定种子产生相同区块和 Spawn Checksum；玩家与敌人共同使用 M4 Skill Runtime。
- finite 与 chunked-infinite 各运行五分钟（9000 Tick）的 Headless Harness，验证 Boss 一次、
  并发上限、有限坐标、无效句柄为零和显式清理后无实体泄漏。

五分钟 Harness 是小型 Placeholder Encounter 的正确性门禁。30 分钟 Soak、1,500 敌人、
3,000 投射物、5,000 拾取物及性能分位 JSON 在 M5 为 `NOT RUN`，继续按计划在 M10 执行。

## 11. M6 已落地覆盖

- XP 多级溢出、等级曲线、死亡奖励拾取、OnPickup 生产和 LevelUp Request。
- Skill/Passive 槽位、重复升级、最大等级、满槽拒绝和显式替换策略。
- Offer 权重、前置、互斥、满级/满槽过滤；固定派生流可复现，Reroll 序列可预测且变化，
  Banish 后不再出现，Skip/Select 进入历史。
- OwnsContent、HasTagCount、SkillLevelAtLeast、StatAtLeast、MapHasTag 条件；AddModifier、
  UnlockOffer、AddEffectOp、TransformSkill、GrantTrait 输出。
- Evolution 技能转换、资格重算、被动消费和构筑标签更新。
- RunSession 在升级请求时暂停 SimulationClock，通过命令选择并恢复，结束后生成不可变结果。
- Schema 5 DTO round-trip、确定性 Hash，以及 Unity 可恢复的 M6 ScriptableObject Placeholder Pack。
- 同一 Seed 的十分钟自动移动/拾取/升级运行两次，比较 Tick、等级、击杀、拾取、选择数和校验值。

十分钟 Harness 是 M6 正确性、确定性和清理门禁，不是目标实体规模性能证明。30 分钟 Soak、
1,500/3,000/5,000 压力和性能分位 JSON 继续为 `NOT RUN`，在 M10 执行。

## 12. M8 已落地覆盖

- Settings 3、Profile 3、RunRecovery 2 独立 round-trip；JSON 不含 RuntimeContentIndex。
- 异步原子写入在 temp flush 后取消仍保留主文件并清理 temp。
- 主文件 SHA-256 失败时恢复上一备份；无备份时返回 ChecksumMismatch。
- Settings v1 固定样本连续迁移到 v3，Profile v1/v2→v3，RunRecovery v1→v2；三类迁移链均已注册。
- Profile 缺失解锁保留 ID 并告警；RunRecovery 缺少必需内容返回 MissingContent。
- 英文、简中和 Pseudo Locale 实际解析；Project Validation 检查两张表全部固定/内容 Key 非空。
- 云冲突覆盖本地较新、远端较新和分叉；Null 的五个子服务无需 Steam SDK 即完成调用。
- Simulation Assembly 静态验证不引用 Platform；应用事件路由完成统计/成就边界。
- PlayMode 覆盖语言呈现、CJK 字体、设置保存、恢复文件和 Profile 生命周期。

M8 文件 I/O 和平台调用只在低频 Application Event 发生，不属于固定 Tick 性能热点。30 分钟 Soak、
1,500/3,000/5,000 压力和性能分位 JSON 继续为 `NOT RUN`，在 M10 执行。

## 13. M9 已落地覆盖

- 向导 Fixture 覆盖 Pack 和十种 Definition；每种 Definition 可 Bake，并具有双语 Key、Pack/
  Placeholder/Development Addressables 标签、测试模板和来源占位。
- 第二角色、第二技能和第二地图加载到同一 Registry；Fixture 目录无 C#，不修改核心程序集。
- Content Pack Builder 对相同输入两次生成一致 Content Hash 和 Catalog SHA-256，报告包含版本、
  依赖、Catalog、标签和 Hash。
- Release Policy 对 Placeholder 路径或标签返回稳定阻断码；实际 CLI 负向门禁必须非零退出并输出
  `M9-RELEASE-PLACEHOLDER`。
- provenance 缺失与输出 SHA-256 不一致分别产生可定位错误；正式 Project Validation 复用该逻辑。
- SpawnSecondarySkill 循环返回稳定 ContentId 路径。
- Wave Timeline 的预算/间隔与 Runtime Scheduler 共用精确采样器，并输出阶段预算、权重、理论并发、
  生命、经验和 Boss 时间。
- Skill Editor Service 与 Headless Harness 在相同 Seed、等级、属性和目标数下结果一致，并报告范围、
  命中盒、DPS、命中、触发、分配和有限日志。
- 完整 EditMode、PlayMode、Project Validation、Pack CLI 和 Windows Development Build 仍是 M9
  最终门禁；30 分钟 Soak 和目标实体压力测试为 `NOT RUN`，保留到 M10。

## 14. M10 性能、发布与冻结覆盖

- 目标配置固定为 1,500 Enemy、3,000 Projectile、5,000 Pickup、200 VFX，30 Hz 推进 54,000 Tick；
  配置、实际数量、有限坐标、无效句柄和最终 Checksum 全部写入 JSON。
- 同一目标配置先运行双实例确定性检查；正式测量前预热 300 Tick，预热后的固定 Tick 分配为 0 B。
- 记录 Tick 与渲染 CPU average/p95/p99/max，EnemyDecision/Movement/Lifetime/Cleanup/Snapshot 分系统
  计时，每模拟分钟采样托管/Native/GC 内存，并记录 GC、池、触发截断和 VFX 丢弃。
- EditMode 覆盖目标计数、确定性、热路径零分配、VFX 池复用/容量/丢弃和非法配置。
- PlayMode 与 Project Validation 必须在最终实现上重跑；API Freeze 漂移是 Validation 失败。
- Windows Development 和非 Development Release verification 都必须实际成功并生成完整 Manifest；
  Release Player 必须启动并输出 60 Tick Smoke JSON。
- `verify-clean-clone.ps1` 在独立克隆执行完整测试、验证、目标规模性能、两个 Build 和 Player Smoke。
- GitHub Actions 只有实际 Runner 成功时才是 PASS；仅验证工作流文件或本地脚本不能替代 CI 运行。

正式结果在 `Docs/Reports/2026-07-28-m10-performance-ci-freeze.md` 中按 PASS/FAIL/NOT RUN 记录，
不得用短测或历史里程碑证据替代 54,000 Tick 与最终构建。

## 15. Qinglan Demo G0.3 契约测试计划

ADR 0013—0015 和 `DemoDevelopment/08_G0_3_CONTRACT_FREEZE.md` 批准下列实施门禁。G0.3 本身是
纯文档契约包，表中测试从 G1.1/G2 对应实现包起执行；未执行时必须记录 `NOT RUN`。

### 15.1 G1.1 Schema 与公共模块

- Schema 1—5 固定 Catalog 逐字节/Hash Golden 不变；Schema 6 全部 14 kind DTO、Hash、Bake、Load
  round-trip。
- 每种新定义至少一个合法 Fixture 和引用 kind 错误、缺失引用、非有限数值、非法状态图/阈值负例。
- 原 14 个 StatIndex 不变；新 4 项按 14—17 追加，默认值、范围、叠加和至少两个消费者覆盖。
- Skill Module 引用操作数只在加载期绑定；未知 token/错误引用失败，Tick 中无字符串解析。
- 回返 Delivery 覆盖 Outbound/Turn/Return、每相位去重、Owner 失效和 Cleanup。
- Status 查询/消费/引爆覆盖 Ref/Tag、零层、部分不足、同 Tick 竞争、原子回滚和 TriggerPosition。

### 15.2 Pipeline 与运行时

- `CreateQinglanDemo` 精确断言 24 项顺序；旧 M2—M6 Pipeline 顺序逐项回归。
- Map/Boss 使用上一 Tick 事件；Damage 后 MechanicReaction 使用当前 Tick 实际结果；选择在 Tick 边界
  暂停，EventFlush 和 Snapshot 顺序不变。
- 免疫、五类 DamageChannel、按 Target＋Channel 冷却、完全屏障、Shield/Health、零伤害和
  `DamageResolved`/`DamageApplied` 事件矩阵。
- 角色机制覆盖真实 PlayerCommand 位移、墙/暂停/传送/纠错/击退排除、跨档、同 Tick 多伤一次损失。
- Reward 同事务重放、随机流隔离、唯一固定输出、空选择回退、Cleanup；Objective/Boss/Affix 各两个
  通用 Fixture，核心源码具体 Qinglan ID 静态搜索零命中。

### 15.3 Profile 3 与应用流

- Settings 2、Profile 3、RunRecovery 2 各自 round-trip，Codec 按 kind 选择目标版本。
- Profile v1→2→3、v2→3 固定 Envelope/Payload，迁移两次结果一致且不猜测首通。
- Loadout 6＋1＋2、互斥、前置、缺失 ID、安全默认与保留原文件。
- 胜利/失败/重复胜利/保存重试使用相同/不同事务 ID 的幂等矩阵。
- 主文件、备份、temp、取消、校验失败、未来版本；成功保存前 Recovery 不删除、平台事件不发布。
- 检测不完整 Recovery 只显示本地化提示并清理开始新局；没有 Continue，不能提交 Outcome。

### 15.4 Freeze 与最终门禁

- 旧 API Hash 下预期 Validation FAIL 并保存规范签名 diff；只允许 G0.3 清单中的追加项。
- 更新 Hash 后完整 EditMode、PlayMode、Project Validation、性能短测和 Windows Development Build
  全部重跑；任一 FAIL 不得把 G1.1 标记 COMPLETE。
- G1.7 重跑完整 Pack/双语 Placeholder Development；G2.8 重跑垂直切片 PlayMode/Build；G3.6
  运行 Release、Player Smoke、DOD-01—10 和合规门禁。

证据目录固定为 `TestResults/QinglanDemo/<work-package>/`；XML、日志、JSON、Manifest 和文件 Hash
必须在结果报告中逐项引用。

## 16. Qinglan Demo G1.6 Encounter 证据

- Focused EditMode：最终 7/7；覆盖 Pack/Hash、九段连续性、Schema 6 EliteRule round-trip、Timeline
  Analyzer、双实例 21,600 Tick 和旧构造兼容。首轮 5/6 因人工批量死亡与 Skill 同 Tick 访问已死亡目标
  产生 194 次无效句柄而失败；Harness 收窄为时间轴所需生产 Pipeline 后复测通过，敌人技能由 G1.5
  专项测试继续负责。
- 全量 EditMode：首轮 233/234；G1.2 的核心源码防具体 ID 测试发现 Harness 内测试 ID 使用
  `qinglan.*`。改为通用 `test.encounter.*`，并补并发预留专项后最终 235/235。
- PlayMode：9/9；Project Validation 和 API Freeze 均 PASS。旧 API Hash 验证曾按预期只报告 Content
  Runtime 漂移，批准 17 条追加并更新基线后通过。
- Headless：同 Seed 双实例各 21,600 Tick，2,582 Spawn、2,571 Death、2 Elite、2 Affixed、0 Boss、
  Peak 16、0 InvalidHandle，Combined Checksum `e86df634f50d29e8`。
- Boss 两次一次性与真实地图出生公平为 `NOT RUN`：分别由 G2.2 和 G2.6/G2.8 首次提供可执行内容。
- Windows Development Build 为 `NOT RUN`：按路线固定由 G1.7 在完整 Placeholder Pack 上执行。

## 17. Qinglan Demo G1.7 Pack / Reward Choice 证据

- Focused EditMode 最终 4/4：完整 Pack/双语/Addressables、锁定 Evolution 候选与 Reward RNG 隔离、
  空池 fallback/重放、RunSession 暂停/恢复与普通 Level-up 回归。
- 全量 EditMode 239/239、PlayMode 9/9、Project Validation、API Freeze 均 PASS。旧 Freeze Hash 下
  Validation 按预期只报告 Simulation/Application 漂移；签名对比为追加 32/9、删除 0。
- `qinglan.pack.demo` 0.5.0 / Schema 6 / 94 项的 Content Hash 固定为
  `798dbb302dda57b9f0158e83010ee89392ffdc291cc629715ba357b691ebd5ad`。
- 两次 Pack CLI 各构建 7 个 Pack 且 PASS；Qinglan `catalog.json` 字节相同，SHA-256 均为
  `9d3979964418cecfda875e5e2dba9d1f067f4c3daafeebe0f7b63db71de200cb`。
- Windows x64 Development Build PASS；Manifest 证据为 EditMode/PlayMode/Validation/Soak 全 pass，
  Qinglan Pack includedInPlayer、Placeholder=true、未批准资产 0。Player 冒烟进入 MainMenu，无错误标记。
- Boss/精英实际消费者、宝匣/fallback RewardDefinition、选择 UI 为 `NOT RUN`，分别由 G2.2/G2.3/G2.6
  关闭；本阶段不能用 Adapter 测试替代完整奖励内容 PlayMode。

## 18. Qinglan Demo G2.1 Map Runtime 证据

- 最终稳定 ID 下 Focused EditMode 6/6、全量 EditMode 245/245、全量 PlayMode 10/10；覆盖 13 个 Scene
  Binding、可行走、目标距离/中断/恢复/一次输出、事件 Seed 隔离、地标幂等和 8 种完成子集。
- 首次全量 EditMode 为 244/245，唯一失败是 API Freeze 按预期发现 Simulation 漂移；ADR 0019 接受
  81 条追加、0 删除后，Project Validation 与完整回归均 PASS。
- `qinglan.pack.demo` 0.6.0 / Schema 6 / 107 项 Content Hash 为
  `fbb58777702837b2730be64e515ef4b2386254089bb109e4c8c6e926ab2ca67c`。
- 两次 Pack CLI 各构建 7 个 Pack；Qinglan Catalog 字节相同，SHA-256 均为
  `01195cf04c0f1668ebb7384594a77f0e6ca0485b088e00fca1eb74e4b647d86c`。
- 性能短测 900 Tick＋300 预热：Tick p99 4.6635 ms、Render p99 0.6965 ms、0 B、GC 0/0/0、
  Checksum `b455f50ce958d212`。
- Boss 参数组合、实际奖励、RunResult/Profile、UI/可读性和 Development Build 为 `NOT RUN`，由
  G2.2—G2.8 按路线关闭。

## 19. Qinglan Demo G2.2 Boss Runtime 证据

- Focused EditMode 最终 15/15：G1.6 Encounter 7 项和 G2.2 Boss 8 项；覆盖两只三阶段 Boss、最终
  719.9 秒边界、8 种风脉组合 Golden、跨多阈值/致命优先、三种清理策略、控制倍率、阶段技能预加载
  与 54,000 次阶段解析 0 B。
- 首次全量 EditMode 为 250/252：Harness 写入两个具体青岚锚点 ID、G2.2 新资产未进入 Addressables。
  Harness 改为从 BossRule 动态收集锚点，配置步骤统一调用 Pack Addressables；再加入真实
  TelegraphOnly Delivery 禁伤/保留视觉寿命测试后最终 253/253 PASS。
- 全量 PlayMode 10/10、Project Validation 和 API Freeze PASS。旧 Freeze Hash 下 Validation 按预期只
  报告 Simulation 漂移；签名对比为追加 58、删除 0，其他四个程序集 0/0。
- 12 分钟 Headless 双实例各推进 21,600 Tick：2,584 Spawn、2,572 Death、2 Elite、2 Affixed、2 Boss、
  Peak 16、0 InvalidHandle；确定性 Checksum `049cb8bdc48092eb`，清理后只剩玩家。
- `qinglan.pack.demo` 0.7.0 / Schema 6 / 121 definitions，Content Hash
  `a654cca5b99f355d9d5122fe106fa4bdba73aebcd745ddbbf136446b5214895a`。两次 Pack CLI 各构建 7 个
  Pack，Qinglan Catalog 字节一致，SHA-256 为
  `b2f0a3aca2544619159ca7a1b55b7535c7d79153701e33fcd57c14211a188270`。
- Boss RewardDefinition/拾取/奇物消费、表现层 Telegraph/音频/可访问性和 Windows Development Build
  为 `NOT RUN`，依次由 G2.3、G2.6/G2.8 和 G2.8 关闭。

## 20. Qinglan Demo G2.3 Reward / Pickup / Relic 证据

- Focused EditMode 最终 20/20：G1.7 Reward Choice 4 项、G2.2 Boss 8 项、G2.3 新增 8 项；覆盖六种
  即时操作、满血不消费、葫芦排除、活动/已提交事务重放、Reward RNG 隔离、三槽 fallback、Relic
  输出、Boss 风险、显化空池、首通唯一快照、默认 256 事务和 5,000 Pickup 扫描 0 B。
- 新增 PlayMode 端到端覆盖 RewardResolution 产生 Relic Choice、FixedTickRunner 暂停、RunSession
  投影/提交和 Clock 恢复；最终全量 EditMode 261/261、PlayMode 11/11。
- 旧 Freeze Hash 下 Project Validation 按预期只报告 Simulation 漂移；ADR 0021 接受 65 条追加、
  0 删除，其他四个冻结程序集逐字节不变。更新后 Project Validation 与 API Freeze 均 PASS。
- 12 分钟 Headless 双实例各 21,600 Tick：2,584 Spawn、2,572 Death、2 Elite、2 Affixed、2 Boss、
  Peak 16、0 InvalidHandle、无泄漏，Checksum `049cb8bdc48092eb`。
- `qinglan.pack.demo` 0.8.0 / Schema 6 / 150 definitions，Content Hash
  `5f233508384d0f9b4b5babc98571ccd45e0d35319c776f4de217ae99e3107c9d`；两次 Pack CLI 各构建
  7 Pack，Qinglan Catalog 字节一致，SHA-256 为
  `b274fc24afa07194682968eb2a290e0b3e12c631a621dd98f2799f0925702236`。
- Profile v3 原子合并、胜负 RunResult、叙事、实际选择 UI/输入、正式表现和 Windows Development Build
  为 `NOT RUN`，依次由 G2.4—G2.8 关闭。

## 21. Qinglan Demo G2.4 RunResult / Game Flow 证据

- Focused EditMode 7/7：覆盖 dependency-sorted Pack 快照、真实青岚 Factory、Build/Map/Reward/统计聚合、
  Victory/Defeat/Abandoned/RecoveryRejected、最终 Boss 双条件、输入数组隔离和幂等全 Entity 释放。
- Focused PlayMode 1/1：覆盖 Title→角色→地图→Preparing→Active→Pause/Resume→Ending→Result→Hub→
  再次出发并完成第二局 Abandoned；证明旧结果不复用、G2.4 不发布 `RunCompleted` 且离开 Result 后
  Session 已释放。最终全量 EditMode 268/268、PlayMode 12/12。
- 旧 Freeze Hash 下 Project Validation 预期只报告 Simulation 6 条与 Application 95 条追加，删除 0；
  Core、Content Runtime、Platform Abstractions 规范签名逐字节不变。ADR 0022 接受后更新 Freeze。
- Profile v3 提交/失败重试、Recovery 清理和平台事件为 `NOT RUN`（G2.5）；实际页面/输入为 `NOT RUN`
  （G2.6）；Windows Development Build 为 `NOT RUN`（G2.8）。

## 22. Qinglan Demo G2.5 Meta / Profile / Settlement 证据

- G2.5 Focused EditMode 8/8：覆盖 12/3/4/3/6 内容拓扑、购买与设施派生、6＋1＋2 容量、终端互斥、
  缺失 ID 保留/安全默认、胜负过滤、事务幂等、Profile 保存失败、Recovery 清理失败、明确拒绝和真实
  Factory Meta 注入。
- Focused PlayMode 1/1：真实 Title→Run→Result 路径在 Profile 原子保存和 Recovery 清理前拒绝离开；
  保存、清理和 `RunResultCommitted` 完成后才允许进入 Hub。最终全量 EditMode 276/276、PlayMode 13/13。
- 首次全量 EditMode 为 272/276；失败均来自旧测试硬编码 0.8.0/150 项、地标单输出和扩展前引用文案。
  更新为 0.9.0 的向前兼容契约后完整复测通过，运行时行为没有通过删除断言规避。
- 旧 Freeze Hash 下 Validation 按预期只报告 Simulation +1、Application +73，删除 0；Core、Content
  Runtime、Platform Abstractions 签名逐字节不变。ADR 0023 更新 Hash 后 Project Validation PASS。
- `qinglan.pack.demo` 0.9.0 / Schema 6 / 193 definitions，Content Hash
  `d332199604988624b32837002059ed0218a4f89b947874810adfc2bfbf098d8d`。两次 CLI 各构建 7 Pack，
  Qinglan Catalog SHA-256 均为 `1a56442c6c05839a9a4b9e6dc3bf776566530690cbb20c26d308d616026d05aa`。
- 性能短测 900 Tick＋300 预热：Tick p99 2.6436 ms、Render p99 0.7134 ms、0 B、GC 0/0/0；
  Windows x64 Development Build PASS，Manifest 的 EditMode/PlayMode/Validation/Soak 均为 pass。
- 12 分钟 Headless `NOT RUN`：本包没有修改固定 Tick，只新增开局/页面低频路径；Release Player Smoke
  `NOT RUN`，G3 使用带专用 Smoke Scene 的 Release Build 执行。实际 UI/输入仍由 G2.6 关闭。

## 23. Qinglan Demo G2.6 UI / Input / Accessibility 证据

- G2.6 Focused EditMode 最终 7/7：Settings 1/2→3 与 round-trip、标准绑定/Composite 冲突、禁用项
  跳过和焦点恢复、三档字体/CJK/五种色觉/形状方向危险通道、复用 UI Snapshot 0 B、真实 held 交互和
  Game.UI 不引用 Simulation Store。
- Focused PlayMode 2/2：键盘和手柄分别完成标题→角色/地图/Loadout→Run→暂停/地图/升级→结果保存→
  据点设施/收藏/故事→确认装配→再次出发；覆盖层禁用 Gameplay，手柄连接不暂停、移除自动暂停并恢复焦点。
- 最终全量 EditMode 283/283、PlayMode 15/15；第一次最终 PlayMode 为 14/15，原因是旧 G2.4 测试仍
  预期 Result 页存在可离开命令。改为先完成 G2.5 持久提交契约后复测通过，没有移除保存门禁。
- 旧 API Hash 验证按预期只报告 Game.Application 漂移；ADR 0024 接受 67 条追加及 Settings 版本常量
  替换，其他四个冻结程序集逐字节不变。最终 Project Validation 与 API Freeze 均 PASS。
- 内容未变：两次 CLI 各构建 7 Pack 且逐文件一致；性能短测 900 Tick＋300 预热，Tick p99
  2.3676 ms、Render p99 0.6256 ms、热路径 0 B、GC 0/0/0、Checksum `a21da08ecd51c5a5`。
- Windows x64 Development Build PASS；Manifest 为 Settings 3/Profile 3/Recovery 2，四项证据均
  `pass`，Placeholder 210、未批准资产 0。Player 无图形启动实际记录 `packs=5, entries=220`。
- 12 分钟 Headless `NOT RUN`：本包不改变固定 Tick 公式，真实 held 交互由 125 Tick 专项和性能短测覆盖。
  Release Build/Release Player Smoke、正式字体/音频/视觉与目标硬件可读性评审 `NOT RUN`，由 G3 执行。
