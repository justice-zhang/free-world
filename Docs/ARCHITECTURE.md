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

| **Assembly**               | **职责**                             | **允许依赖**                                     |
|----------------------------|--------------------------------------|--------------------------------------------------|
| Game.Core                  | ID、标签、结果、随机数、基础工具     | 无                                               |
| Game.Content.Runtime       | 烘焙后的纯运行时定义                 | Core                                             |
| Game.Simulation            | 实体、系统、战斗、技能、地图模拟     | Core、Content.Runtime                            |
| Game.Platform.Abstractions | 平台、云、成就接口                   | Core                                             |
| Game.Application           | 状态机、Run 协调、内容加载、保存协调 | Core、Runtime、Simulation、Platform.Abstractions |
| Game.Content.Authoring     | ScriptableObject 作者数据            | Unity、Content.Runtime                           |
| Game.Infrastructure        | Addressables、本地化、文件存储       | Application                                      |
| Game.Presentation          | View、动画、特效、音频、摄像机       | Application、Simulation                          |
| Game.UI                    | 菜单、HUD、升级选择、结算            | Application                                      |
| Game.Platform.Null         | 无平台环境实现                       | Platform.Abstractions                            |
| Game.Platform.Steam        | 后续 Steam 适配                      | Platform.Abstractions                            |
| Game.Editor                | 烘焙、验证、向导、构建工具           | Authoring、Runtime                               |
| Game.Tests.EditMode        | 纯逻辑测试                           | Core、Runtime、Simulation                        |
| Game.Tests.PlayMode        | 场景和流程测试                       | Application、Presentation、UI                    |

硬性边界：

- Game.Core 不引用 UnityEngine。

- Game.Simulation 不引用场景、Prefab、MonoBehaviour 或表现资源。

- Game.Presentation 不能直接写入模拟 Store，只能提交命令。

- Game.UI 不能通过场景查找取得服务。

- 不允许程序集循环依赖。

## 3. Composition Root

只允许 GameBootstrapper 负责组合：

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

## 4. 固定 Tick 与时钟

默认模拟 30 Hz，表现目标 60 FPS 或更高。

> SimulationClock: 战斗和地图逻辑  
> UIClock: 菜单、升级界面和过渡  
> PresentationClock: 非关键动画与 VFX

升级选择暂停时只停止 SimulationClock。不依赖全局 Time.timeScale 作为唯一暂停机制。

系统执行顺序固定在一个 Pipeline 中：

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

- SummonStore

Store 使用 Dense Array、Swap-back Remove、Free List 和 Generation。实体句柄：

> public readonly struct EntityHandle  
> {  
> public readonly int Index;  
> public readonly ushort Generation;  
> }

删除实体后递增 Generation，旧句柄不能引用新实体。

## 6. 空间查询

统一空间网格服务用于最近目标、投射物碰撞、范围伤害、拾取吸附、敌人分离和裁剪。

> cellX = floor(position.x / cellSize)  
> cellY = floor(position.y / cellSize)  
> cellKey = Hash(cellX, cellY)

禁止每个技能遍历全部敌人。

## 7. 表现层

表现层只接收：

- Render Snapshot

- Simulation Event

- View Spawn/Release Request

- VFX/Audio Request

ActorView、ProjectileView 和 PickupView 只更新显示和提交输入，不计算伤害、经验、死亡或掉落。

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
