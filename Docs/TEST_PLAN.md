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

M7 自动化覆盖：

- 手柄完成完整流程

- UI 打开时 Gameplay 输入被禁用

- 暂停只停止模拟

- View 释放后无悬空绑定

- 缺失正式资源时使用程序化占位

- Snapshot 插值、四类 View 绑定/回收、Generation 失效拒绝

- Hit/Death/Status 表现请求、VFX/伤害数字回收和共享 Canvas

- 场景释放后无 View、池 Owner 或输入订阅残留

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

- Settings、Profile、RunRecovery 三类 Schema 2 文档 round-trip；JSON 不含 RuntimeContentIndex。
- 异步原子写入在 temp flush 后取消仍保留主文件并清理 temp。
- 主文件 SHA-256 失败时恢复上一备份；无备份时返回 ChecksumMismatch。
- Settings v1 固定样本通过显式注册表迁移到 v2；三类迁移链均已注册。
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
