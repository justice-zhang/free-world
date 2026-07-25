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

| **Assembly**               | **职责**                             | **M2 实际直接依赖**                                                       |
|----------------------------|--------------------------------------|---------------------------------------------------------------------------|
| Game.Core                  | ID、标签、结果、随机数、基础工具     | 无                                                                        |
| Game.Content.Runtime       | 烘焙后的纯运行时定义                 | Game.Core                                                                 |
| Game.Simulation            | 实体、系统、战斗、技能、地图模拟     | Game.Core、Game.Content.Runtime                                           |
| Game.Platform.Abstractions | 平台、云、成就接口                   | Game.Core                                                                 |
| Game.Application           | 状态机、Run 协调、内容加载、保存协调 | Game.Core、Game.Content.Runtime、Game.Simulation、Game.Platform.Abstractions |
| Game.Content.Authoring     | ScriptableObject 作者数据与 Baker    | Unity、Game.Core、Game.Content.Runtime                                    |
| Game.Infrastructure        | Composition Root 与基础设施入口      | Unity、Game.Core、Game.Content.Runtime、Game.Application、Game.Platform.Abstractions、Game.Platform.Null |
| Game.Presentation          | View、动画、特效、音频、摄像机       | Unity、Game.Application、Game.Simulation                                  |
| Game.UI                    | 菜单、HUD、升级选择、结算            | Unity、Game.Application                                                   |
| Game.Platform.Null         | 无平台环境实现                       | Game.Platform.Abstractions                                                |
| Game.Platform.Steam        | 后续 Steam 适配（M0 未创建）         | Game.Platform.Abstractions                                                |
| Game.Editor                | 验证、Bake、Placeholder、场景与构建工具 | Unity Editor、Addressables Editor、Game.Core、Game.Content.Authoring、Game.Content.Runtime、Game.Infrastructure |
| Game.Tests.EditMode        | 治理、内容与纯模拟内核测试           | 产品程序集、Game.Editor、Unity Test Framework                             |
| Game.Tests.PlayMode        | Bootstrap 内容加载和生命周期测试     | Game.Core、Game.Content.Runtime、Game.Application、Game.Infrastructure、Game.Platform.Abstractions、Game.Platform.Null、Unity Test Framework |

M2 实际依赖图（省略测试程序集）：

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
