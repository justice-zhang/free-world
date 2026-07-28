# 系统架构规范

## 1. 总体分层

> Content Authoring  
> ScriptableObjects / Map Scenes / Wave Timelines / Visual Profiles  
> \|  
> \| Validate + Bake  
> v  
> Baked Content Packs  
> Manifest / Runtime Definitions / Effect Ops / Asset References  
> \|  
> \| Addressables  
> v  
> Application Layer  
> Bootstrap / State Machine / Run Coordinator / Save / Pack Loading  
> \|  
> \| Commands  
> v  
> Simulation Layer - Fixed Tick  
> Entities / Skills / Damage / Status / Spawn / XP / Loot  
> \|  
> \| Events + Render Snapshot  
> v  
> Unity Presentation  
> Views / Animation / VFX / Audio / Camera / HUD / Menus  
> \|  
> v  
> Platform Adapters  
> Null Platform / Steam / Cloud / Achievements

## 2. Assembly Definition

| **Assembly**               | **职责**                             | **M9 实际直接依赖**                                                       |
|----------------------------|--------------------------------------|---------------------------------------------------------------------------|
| Game.Core                  | ID、标签、结果、随机数、基础工具     | 无                                                                        |
| Game.Content.Runtime       | 烘焙后的纯运行时定义                 | Game.Core                                                                 |
| Game.Simulation            | 实体、系统、战斗、技能、地图模拟     | Game.Core、Game.Content.Runtime                                           |
| Game.Platform.Abstractions | 平台、云、成就接口                   | Game.Core                                                                 |
| Game.Application           | 状态机、Run 协调、内容加载、保存协调 | Game.Core、Game.Content.Runtime、Game.Simulation、Game.Platform.Abstractions |
| Game.Content.Authoring     | ScriptableObject 作者数据与 Baker    | Unity、Game.Core、Game.Content.Runtime                                    |
| Game.Infrastructure        | Composition Root、存档与基础设施入口 | Unity、Game.Core、Game.Content.Runtime、Game.Application、Game.Platform.Abstractions、Game.Platform.Null、Unity Localization |
| Game.Presentation          | View、动画、特效、音频、摄像机       | Unity、Game.Application、Game.Simulation                                  |
| Game.UI                    | 菜单、HUD、升级选择、结算、本地化适配 | Unity、Game.Application、Unity Localization                               |
| Game.Platform.Null         | 无平台环境实现                       | Game.Platform.Abstractions、Game.Core                                     |
| Game.Platform.Steam        | 后续 Steam 适配（M0 未创建）         | Game.Platform.Abstractions                                                |
| Game.Editor                | 验证、Bake、Placeholder、预览与构建工具 | Unity Editor、Addressables Editor、Game.Core、Game.Content.Authoring、Game.Content.Runtime、Game.Simulation、Game.Infrastructure |
| Game.Tests.EditMode        | 治理、内容与纯模拟内核测试           | 产品程序集、Game.Editor、Unity Test Framework                             |
| Game.Tests.PlayMode        | Bootstrap 内容加载和生命周期测试     | Game.Core、Game.Content.Runtime、Game.Application、Game.Infrastructure、Game.Platform.Abstractions、Game.Platform.Null、Unity Test Framework |

M9 实际依赖图（省略 Unity Package 与测试程序集）：

```text
Game.Core
├─ Game.Content.Runtime
│  ├─ Game.Simulation
│  │  ├─ Game.Application
│  │  └─ Game.Presentation
│  ├─ Game.Content.Authoring
│  └─ Game.Infrastructure
└─ Game.Platform.Abstractions
   ├─ Game.Platform.Null
   │  └─ Game.Infrastructure
   └─ Game.Application

Game.Application ─┬─ Game.Infrastructure
                  ├─ Game.Presentation
                  └─ Game.UI

Game.Core ───────────────┐
Game.Content.Authoring ──┤
Game.Content.Runtime ────┼─ Game.Editor
Game.Simulation ─────────┤
Game.Infrastructure ─────┘
```

`Game.Core`、`Game.Content.Runtime`、`Game.Simulation`、`Game.Platform.Abstractions`、
`Game.Application` 和 `Game.Platform.Null` 均设置 `noEngineReferences: true`。
`Game.Infrastructure` 是 Unity 最外层组合入口，因此可以同时依赖 Core、纯内容运行时、
应用抽象和 Null 平台实现；
该依赖不反向进入应用或模拟程序集。

硬性边界：

- Game.Core 不引用 UnityEngine。

- Game.Simulation 不引用场景、Prefab、MonoBehaviour 或表现资源。

- Game.Presentation 不能直接写入模拟 Store，只能提交命令。

- Game.UI 不能通过场景查找取得服务。

- 不允许程序集循环依赖。

## 3. Composition Root

只允许 `Game.Infrastructure` 中的 GameBootstrapper 负责组合：

> GameBootstrapper  
> -\> ProjectConfiguration  
> -\> ContentService  
> -\> SaveService  
> -\> PlatformFacade  
> -\> SimulationWorld  
> -\> RunCoordinator  
> -\> PresentationCoordinator  
> -\> GameStateMachine

依赖通过构造函数或显式初始化参数传递。禁止把 Service Locator 暴露给任意系统。

M1 在上述组合中增加 `ContentRegistry`。Bootstrap 从 Scene 显式引用的 baked
测试 Catalog TextAsset 读取 JSON，立即转换为纯 DTO/Runtime Catalog、验证并注册，
输出摘要后进入空 `MainMenu`。作者 ScriptableObject、AssetDatabase 和 Unity Object
不会进入 Application、Runtime Catalog 或 Simulation。

M8 在同一组合根创建 `LocalFileSaveStorage`、`UnityJsonSaveCodec`、`SaveCoordinator`、
`M8RuntimeServices`、`UnityLocalizationService` 和同一个 `NullPlatformFacade`。存档加载先于 M7
Runtime Host，使 Locale、Binding 和可访问性设置在首屏呈现前生效；Application Event 同时驱动
低频本地保存和平台路由，不进入固定 Tick。

## 4. 固定 Tick 与时钟

默认模拟 30 Hz，表现目标 60 FPS 或更高。

> SimulationClock: 战斗和地图逻辑  
> UIClock: 菜单、升级界面和过渡  
> PresentationClock: 非关键动画与 VFX

升级选择暂停时只停止 SimulationClock。不依赖全局 Time.timeScale 作为唯一暂停机制。

M2 已落地 Pipeline 只包含当前里程碑需要的系统，执行顺序由
`SimulationPipeline.CreateM2Default` 显式构造并可由测试逐项断言：

```text
01 MovementSystem
02 LifetimeSystem
03 CleanupSystem
04 SnapshotBuildSystem
```

`MovementSystem` 只修改运动列并同步空间网格；`LifetimeSystem` 只产生删除命令；
`CleanupSystem` 是唯一应用 M2 结构变化的系统；`SnapshotBuildSystem` 必须最后执行。
不得依赖 Script Execution Order 或逐实体 Update 改变该顺序。

Clock 使用 `double` 累积表现 Delta。每次推进最多执行 `MaxCatchUpTicks`，剩余积压
保留到后续推进；暂停时忽略新 Delta，暂停状态下可单步一个 Tick。
同一次 Runner Advance 的追赶 Tick 事件累积为一个批次，下一次实际执行 Tick 的
Advance 或 Step 才清空，避免表现层获得控制前丢失前序 Tick 事件。

以下为后续里程碑逐步启用的目标完整 Pipeline，不代表 M2 已实现：

> 01 InputCommandSystem  
> 02 SpawnRequestSystem  
> 03 SkillCooldownSystem  
> 04 EnemyDecisionSystem  
> 05 TargetingSystem  
> 06 MovementSystem  
> 07 SpatialGridBuildSystem  
> 08 ProjectileMovementSystem  
> 09 CollisionQuerySystem  
> 10 SkillExecutionSystem  
> 11 DamageResolutionSystem  
> 12 StatusTickSystem  
> 13 DeathSystem  
> 14 LootDropSystem  
> 15 PickupSystem  
> 16 ExperienceSystem  
> 17 LevelUpRequestSystem  
> 18 LifetimeSystem  
> 19 CleanupSystem  
> 20 EventFlushSystem  
> 21 SnapshotBuildSystem

## 5. 实体模型

不编写通用 ECS，只实现游戏所需的紧凑 Store：

- ActorStore

- ProjectileStore

- AreaStore

- PickupStore

- SummonStore（后续召唤里程碑，M2 未实现）

Store 使用 Dense Array、Swap-back Remove、Free List 和 Generation。实体句柄：

> public readonly struct EntityHandle  
> {  
> public readonly int Index;  
> public readonly ushort Generation;  
> }

删除实体后递增 Generation，旧句柄不能引用新实体。

M2 Store 生命周期：

1. Create 从 Free List 复用 Slot，或扩展 Slot；Generation 从 `1` 开始。
2. Handle 的 Index 解析为 Dense Index，读写前必须同时验证 Generation。
3. 系统遍历只读取或写入 Dense 状态，不直接 Create/Remove。
4. Cleanup 删除时用最后一项覆盖空洞，并更新被移动实体的 Slot → Dense 映射。
5. 被删 Slot 的 Generation 递增后进入 Free List；旧 Handle 的读、写和删除均失败。

Handle 只在所属 Store 内有意义。网格、快照、命令和事件使用
`EntityKind + EntityHandle` 形成跨 Store 标识。M2 四个 Store 只共享固定运动列的
内部实现，不暴露组件注册、Archetype 或通用查询 API。

## 6. 空间查询

统一空间网格服务用于最近目标、投射物碰撞、范围伤害、拾取吸附、敌人分离和裁剪。

> cellX = floor(position.x / cellSize)  
> cellY = floor(position.y / cellSize)  
> cellKey = Hash(cellX, cellY)

禁止每个技能遍历全部敌人。

M2 网格使用 `EntityKind + EntityHandle` 唯一索引实体，支持插入、跨 Cell 更新、
删除、半径查询和排除自身的邻近查询。查询写入调用方复用的
`SpatialQueryBuffer`；查询顺序不构成模拟契约，需要稳定排序的后续系统必须显式处理。

## 7. 表现层

表现层只接收：

- Render Snapshot

- Simulation Event

- View Spawn/Release Request

- VFX/Audio Request

ActorView、ProjectileView 和 PickupView 只更新显示和提交输入，不计算伤害、经验、死亡或掉落。

M7 以单个 `PresentationCoordinator` 对快照做集合对账。Actor、Projectile、Area、Pickup
分别进入持久池；池只在容量不足时扩容，View 内没有独立 `Update`。`RunSession` 可把敌人
EntityHandle 解析为稳定 `VisualProfileId`，`VisualProfileCatalog` 只做表现资源匹配；未命中、
玩家或当前没有实例级表现 ID 的实体统一使用运行时生成的方形 Sprite、按 EntityKind 着色。

```text
FixedTickRunner
  -> RenderSnapshot -----------------------> View Pool reconcile/interpolate
  -> SimulationEventBuffer (same batch) ---> exact-handle release
  -> CombatEventBuffer --------------------> Hit/Death/Status requests
                                               -> pooled VFX
                                               -> pooled test-tone AudioSource
                                               -> shared-Canvas damage numbers
```

事件消费者只在 `RunSession.Advance` 实际执行 Tick 后、下一批次开始前读取一次，符合 M2
事件缓冲的单生产者批次契约。表现请求不会反向调用伤害、经验、死亡或掉落系统。

M2 `RenderSnapshot` 格式：

```text
Tick
Entries[]
├─ EntityKind + EntityHandle
├─ PreviousPosition / CurrentPosition
├─ PreviousFacingRadians / CurrentFacingRadians
└─ PreviousStateFlags / CurrentStateFlags
```

Tick 开始前捕获 Previous，Cleanup 后捕获 Current。新创建实体以前后相同状态进入
快照；本 Tick 删除的实体不进入 Current，并由 `SimulationEventType.Removed` 通知未来
View 释放。位置按夹紧到 `[0, 1]` 的 alpha 线性插值，朝向按最短角路径插值。

### 7.1 输入、UI 与摄像机

`M7InputRouter` 持有 `Gameplay`、`UI`、`Debug` 三个 Action Map。RunHUD 仅启用 Gameplay，
其余页面仅启用 UI，Debug 在开发框架阶段保持启用。键鼠和 Gamepad 绑定进入同一命令入口；
UI 不读取 Simulation Store。

```text
Bootstrap -> MainMenu -> CharacterSelect -> MapSelect -> Loading -> RunHUD
                                ContentError <-/             |
RunHUD <-> Pause -> Settings                              LevelUpDraft
   |          |                                               |
   +----------+-----------------> RunResult ------------------+
                                      |
                                   MainMenu
```

页面由 `GameFlowPresenter` 把 `GameState` 和 UI-safe 数据投影为只含本地化 Key 的
`UiPageViewModel`。`RuntimeUiRoot` 使用一个共享 Canvas；伤害数字只在该 Canvas 的共享层中池化。
Settings 的运行时模型包含重映射接口、摇杆死区、震动强度、屏幕震动、闪光强度、伤害数字和
自动瞄准策略。`PresentationCameraRig` 只跟随 View Transform，提供边界夹紧、Shake Request 和
总效果开关，不读取模拟 Store。

M8 的 Presenter 仍只产生 Key；`UnityLocalizationService` 在 View 边界从 `UI` String Table 解析
`en`、`zh-Hans` 或 Pseudo。Project Validation 检查所有固定 UI/诊断 Key 与 baked 内容 Key 在英、
中表均非空。设置只保存 Locale Code，语言正文不会进入 Application 或存档。

## 8. 地图运行时

> public interface IMapRuntime  
> {  
> void Initialize(in MapRuntimeContext context);  
> bool IsWalkable(float2 position);  
> float2 SampleEnemySpawnPosition(  
> float2 playerPosition,  
> float minDistance,  
> float maxDistance,  
> ref RandomStream random);  
> float2 ResolveMovement(  
> float2 currentPosition,  
> float2 desiredPosition,  
> float radius);  
> MapEnvironmentSnapshot GetEnvironmentSnapshot();  
> }

首批实现：

- FiniteArenaMapRuntime

- ChunkedInfiniteMapRuntime 的最小可用版本

普通敌人不使用逐个 NavMeshAgent，优先使用 Steering、局部分离和轻量障碍规避。

## 9. 平台隔离

> public interface IPlatformFacade  
> {  
> bool IsAvailable { get; }  
> IAchievementService Achievements { get; }  
> IPlatformStatsService Stats { get; }  
> ICloudSyncService Cloud { get; }  
> IRichPresenceService RichPresence { get; }  
> IUserIdentityService Identity { get; }  
> }

初始只实现 NullPlatformFacade。Steam SDK 只存在于单独 Assembly，不反向污染游戏逻辑。

M8 的子服务固定为 Achievements、Stats、Cloud、RichPresence 和 Identity。应用层
`ApplicationEventStream` 发布 SettingsChanged、RunStarted、RunCompleted；存档服务处理三个本地
文件生命周期，平台路由只从 RunCompleted 更新统计/成就。云冲突策略比较 Local、Remote 与最后
同步校验和；双方分叉时返回 RequireUserChoice，不静默覆盖。

## 9.1 存档隔离

`Game.Application` 定义三个纯数据文档和 `ISaveStorage` / Codec / Migration 合约；Unity 文件和
JsonUtility 实现只存在于 Infrastructure。写入顺序为 temp flush、上一版本 backup、同卷 atomic
replace；外层 SHA-256 信封先校验后迁移。Profile 缺失解锁保留 ID 并告警，RunRecovery 缺少角色、
地图或已拥有内容时明确拒绝恢复。完整 wire 规则见 `Docs/SAVE_FORMAT.md`。

## 10. 禁止 API 与模式

在 Simulation 和高频路径中禁止：

- GameObject.Find

- FindObjectOfType

- Resources.Load

- 反射式类型扫描

- LINQ

- 每帧字符串拼接或格式化

- 每帧创建 List/Dictionary

- 高频 Instantiate/Destroy

- 任意系统访问全局 Service Locator

- 依赖 Script Execution Order 解决逻辑顺序

## 11. M3 属性、伤害与状态边界

M3 在 M2 Store 上增加 Generation-safe 的 Actor 战斗侧车。公开 API 只返回不可变
`Health`、`Shield`、`ActiveStatus` 和属性值；技能、状态和测试入口只能提交请求，不能
直接写生命。`DamageResolutionSystem` 是伤害导致的 Health 唯一写入者。

稳定 `StatId` 位于 `Game.Core`，当前 Run 内由 `StatCatalog` 映射为 `StatIndex`。
Modifier 加入时把 StackingGroup 映射为集合内紧凑整数；属性求值只比较整数和数组，
不进行字符串比较或创建临时集合。Actor slot 复用时同时复用 Stat、Modifier 和 Status
数组，高频出生/死亡不会为已经达到的并发高水位重复创建战斗记录。

伤害结算顺序固定为：

```text
验证目标、类型与 ProcDepth
→ 规范化 BaseValue
→ 来源 Damage 属性
→ 固定随机流暴击
→ Physical Armor 或元素 Resistance（True 跳过）
→ 单包伤害边界
→ Shield
→ Health
→ DamageApplied
→ 首次致死 DeathRequest
```

状态申请只携带来源、目标、来源 ContentId、Status RuntimeContentIndex、Strength 和
ProcDepth。Modifier、周期伤害与临时护盾行为固定在经验证和 Baker 转换的
`RuntimeStatusDefinition.Behavior` 中，申请者不能覆盖。系统不按 Burning、Slow 或
Shielded 的 ContentId 分支。

临时护盾按实例、层数和 Strength 计算容量贡献；刷新不重复扩容，替换、过期或驱散会
回收对应容量。周期伤害只排入下一次 DamageResolution，且继承并递增 ProcDepth。
`DeathPending` Actor 不再推进状态或 Modifier。

M3 默认 Pipeline 为：

```text
Movement → DamageResolution → StatusTick → Death → Lifetime
→ Cleanup → EventFlush → SnapshotBuild
```

`SimulationWorld.RunTick` 在 Pipeline 返回后无条件完成一次幂等事件 Flush，因此显式
测试 Pipeline 即使省略 `EventFlushSystem` 也不会静默丢失 Tick 内战斗事件。M2 的
`CreateM2Default` 顺序保持不变。

## 12. M4 模块化技能运行时

M4 不改变程序集依赖方向：`Game.Content.Runtime` 定义纯 Skill Schema，
`Game.Content.Authoring` 负责 ScriptableObject 与 Baker，`Game.Simulation` 只依赖 Core 和
Content.Runtime。表现资源通过稳定 PresentationId 留在边界外。

```text
Game.Content.Authoring
        │ Bake / Validate
        ▼
Game.Content.Runtime ── Registry bind ──► RuntimeContentIndex
        │
        ▼
Game.Simulation
  SkillModuleRegistry → SkillRuntimeCatalog → SkillInstance
        │                                     │
        └──────── executor refs ◄─────────────┘
```

Composition Root 显式构造 `SkillModuleRegistry`，没有运行时反射扫描或全局 Service Locator。
Runtime Catalog 在 Run 开始前把稳定模块 ID 解析为 executor，把内容引用和 StatId 解析为紧凑
索引，并预构建全部等级。两个角色实例共享不可变编译定义，各自持有 Owner、等级和冷却。

M4 默认 Pipeline 为：

```text
SkillTrigger → Movement → SkillDelivery → SkillEffectResolution
→ DamageResolution → StatusTick → Death → Lifetime
→ Cleanup → EventFlush → SnapshotBuild
```

Timer 与上一 Tick 的 OnHit/OnKill/OnDamageTaken/OnStatusApplied 事件在 SkillTrigger 消费；
OnPickup 由后续拾取模块通过同一命令入口提交。移动之后推进 Projectile/Area/Aura/Orbit，
EffectResolution 再把通用命令路由到 M3 Damage/Status API。二次技能保留来源上下文并增加
ProcDepth。Cleanup 继续是结构创建/删除的唯一应用点。

Targeting 只查询统一 SpatialGrid；结果、Trigger、Effect 和 Delivery 都使用可复用结构缓冲。
运行时不访问 SkillAuthoring、不解析 LevelPatch 字符串、不用 LINQ/反射，也不为技能创建
MonoBehaviour。`SkillPreviewHarness` 使用同一纯模拟管线和固定随机种子输出 DPS、命中数与
触发次数，UI 属于后续里程碑。

## 13. M5 敌人、刷怪与地图运行时

M5 保持原程序集方向。`Game.Content.Runtime` 只保存 Schema 4 定义；
`Game.Simulation` 编译 Enemy/Skill 的 load-local index，并集中持有行为 sidecar、Spawn
Request Buffer、地图 Provider 和 Encounter Scheduler。Scene 不参与模拟决策。

```text
EncounterScheduler → SpawnRequestBuffer
                           │
                           ▼ Cleanup（唯一结构写入点）
IMapRuntime ◄── EnemyDecisionSystem ──► EnemyRuntime sidecar
                           │
                           ▼
                    M4 SkillRuntime → M3 Combat
```

M5 Pipeline 为：

```text
SpawnScheduler → EnemyDecision → SkillTrigger → Movement → SkillDelivery
→ SkillEffectResolution → DamageResolution → StatusTick → Death → Lifetime
→ Cleanup → EventFlush → SnapshotBuild
```

所有敌人在 `EnemyDecisionSystem` 的单次稠密遍历中推进 Chase、KeepDistance、Charge
Windup/Charging/Recovering 和 RangedAttack 状态。局部分离复用 SpatialGrid 查询缓冲；
障碍规避通过 `IMapRuntime.ResolveMovement` 校正期望步长。没有逐敌人 Update、NavMeshAgent、
全局寻路或按敌人类型派生的 Controller 树。

`FiniteArenaMapRuntime` 以有限矩形和轴对齐障碍提供 Walkable、采样及滑轴回退。
`ChunkedInfiniteMapRuntime` 的 M5 最小版本把玩家所在 chunk 周围
`(2 × ActiveChunkRadius + 1)²` 个区块视为逻辑活动窗口；区块签名只由 run seed 和坐标决定。
窗口外区块在 M5 不持有实体或生成对象，因此释放等价于从活动窗口移除坐标，没有 Scene
或 Unity Object 生命周期。正式区块内容流送、持久化与表现复用留给后续里程碑。

Encounter 与 Map 通过稳定 EncounterScheduleId 解耦；同一 Encounter 可由两个 Provider
复用。Scheduler 线性采样预算/间隔曲线、按权重选择敌人和群组，并在普通刷怪上为未触发
Boss 预留并发槽。Boss rule 使用一次性标记，结构创建只在 Cleanup 应用。

Enemy 与 Player 共用 M4 SkillRuntime。目标过滤通过 Enemy sidecar 判断双方阵营；没有 M5
敌人的旧 M4 World 保留“除 Owner 外均可选”的兼容行为。DifficultySnapshot 在 Run 创建时
冻结 Health、Damage、Speed、Spawn Rate、Elite Probability 和 Reward 倍率。

## 14. M6 局内成长、构筑与 Run 协调

M6 保持程序集方向不变。Content.Runtime 保存 Schema 5；Simulation 在 Run 开始前编译引用，
Application 只协调时钟和命令，UI 尚不参与候选生成。

```text
Enemy Death → pending XP Pickup → Cleanup create
                                  ↓
PickupSystem → ExperienceSystem → LevelUpRequestSystem
                                      ↓
                            UpgradeOfferSet / PauseRequested
                                      ↓
Application RunSession → Select / Reroll / Banish / Skip
                                      ↓
                                  BuildState
```

M6 Pipeline 为：

```text
SpawnScheduler → EnemyDecision → SkillTrigger → Movement → SkillDelivery
→ SkillEffectResolution → DamageResolution → StatusTick → Death
→ Pickup → Experience → LevelUpRequest → Lifetime → Cleanup
→ EventFlush → SnapshotBuild
```

`BuildState` 集中持有 Skill/Passive Inventory、Trait、标签计数、激活 Synergy、进化资格、
Modifier 和技能附加 Effect。技能类不判断流派；Synergy/Evolution 只解释已验证的通用操作码。
结构创建仍仅在 Cleanup 发生。

`OfferGenerator` 从 Run Seed 派生固定 Offer 流，不消耗战斗或刷怪流。历史记录包含动作序号、
流 RootSeed、调用前后计数、最多三个 OfferId 和动作主体 ID，可用于复现候选与选择路径。
候选数组只在 SimulationClock 已请求暂停的升级阶段产生。

`RunSession` 将 `PauseRequested` 映射到 SimulationClock Pause 和 GameState.LevelUpChoice；选择或
跳过后恢复时钟。RunResult 冻结 Tick、时长、等级、库存数、联动数、击杀、拾取、经验和候选
动作统计。M7 只能读取这些接口并提交命令，不能直接写 Store 或重新实现过滤。

## 15. M9 内容生产工具边界

M9 的 EditorWindow 是薄界面；Creation、Validation、Timeline、Preview 和 Pack Build 规则都在
独立服务中，可由测试或命令行复用。向导写 Authoring/Localization/Addressables 后立即通过现有
Baker 生成 Catalog，不维护第二套 Registry。

```text
Content Authoring ── Bake ──► ContentRegistry
       │                         │
       ├─ Validator/Pack Builder │
       └─ Editor Windows ────────┼─► WaveTimelineAnalyzer
                                 └─► SkillPreviewHarness
                                          │
                                          └─ Fixed Tick Simulation
```

`Game.Editor → Game.Simulation` 是允许的最外层单向依赖。Timeline Scheduler 与 Editor 共用
`EncounterTimelineSampler`；Skill UI 调用同一个 Headless Harness。Simulation 不认识 EditorWindow、
AssetDatabase、ScriptableObject 或 UnityEngine，且不把预览日志放入高频运行时路径。

Development Build 允许程序化 Placeholder 用于框架验收；非 Development Build 在普通 Project
Validation 之后追加 Release 门禁。Placeholder、缺失/不合格 provenance、Hash 不一致、未登记
Third Party 或内容错误都会抛出 `BuildFailedException`，不存在全局忽略入口。

## 16. M10 性能证据、构建与冻结边界

M10 保持 Simulation 架构和 30 Hz Tick 不变。`M10StressScenario` 只组合既有稠密 Store、
EnemyDecision、Movement、Lifetime、Cleanup 和 Snapshot，以固定种子创建实际目标数量；Editor 命令
只负责预热、测量、内存/池指标和 JSON，不把 Unity Profiler 或构建逻辑带入 Simulation。

```text
Pure target-scale Simulation ──► timing/memory/pool JSON
                                    │
EditMode / PlayMode / Validation ───┼──► BuildManifest
                                    │
Development Player ─────────────────┤
Release verification + Smoke ───────┘
```

Release Validator 以实际 `IncludeInBuild` Addressables Group 和 Build Scene 依赖为边界。框架验证构建
使用临时纯程序化 Smoke Scene，并只在构建作用域排除 development-only/placeholder Group；finally
恢复 Editor 状态。它证明 Release 管线而不是宣布 Placeholder 已成为正式内容。

`Game.Editor → Game.Application` 是 M10 新增的最外层直接依赖，用于读取 Save Schema 和生成完整
Manifest；依赖不反向。五个稳定程序集的 public/protected 签名 Hash 被 Project Validation 冻结，
后续变更须经 ADR 和迁移审查。完整决定见 ADR 0012。
