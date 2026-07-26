# 内容系统与数据结构规范

## 1. 稳定 ID

所有内容使用命名空间字符串 ID，不使用 enum 表示具体内容。

> base.character.test_runner  
> base.skill.arc_bolt  
> base.status.burning  
> base.enemy.slime_basic  
> base.map.test_arena  
> base.synergy.fire_overload

规则：

- 小写字母、数字、下划线和点号。

- 点号用于层级。

- 至少包含一个点号；段不能为空；下划线只能位于同一段的字母或数字之间。

- 最长 128 个字符。作者资产必须直接保存 canonical 小写形式，不在构建时静默修正。

- `ContentId.Create` 对外部输入执行 trim 和 invariant lowercase 规范化；
  `ContentValidator` 对作者数据要求输入已经规范化。

- 发布后不得修改。

- 显示名称与 ID 分离。

- 存档保存稳定 ID，不保存运行时索引。

- 运行时映射为紧凑 RuntimeContentIndex。

- 不只保存 Hash，始终保留原始字符串。

- `StableHash` 只用于哈希表桶定位；相等、排序和序列化始终使用完整 canonical 字符串。

- JSON/存档边界把 `ContentId` 显式序列化为字符串，再通过验证工厂恢复。

`ContentTag` 使用相同 canonical 字符规则，但类型上与 `ContentId` 分离。
`RuntimeContentIndex` 只在一次成功 Registry 加载中从 0 连续分配；默认值无效，
不得写入存档。

## 2. 内容包

> {  
> "packId": "com.studio.base",  
> "version": "0.1.0",  
> "schemaVersion": 1,  
> "minimumGameVersion": "0.1.0",  
> "dependencies": \[\],  
> "catalogAddress": "packs/base/catalog",  
> "assetLabel": "pack.base",  
> "official": true  
> }

加载顺序：

> 读取 Manifest  
> -\> 检查版本  
> -\> 检查依赖  
> -\> 拓扑排序  
> -\> 加载 Baked Catalog  
> -\> 检查重复 ID  
> -\> 建立 RuntimeIndex  
> -\> 加载必要资源  
> -\> 进入游戏

第一阶段不允许内容包覆盖相同 ID。补丁覆盖机制以后通过显式 PatchTarget 设计。

M1 的 `ContentVersion` 使用严格的 `major.minor.patch` 非负整数格式。Manifest 同时检查：

- 内容 Schema 必须为运行时明确支持的版本。
- 当前游戏版本位于 Pack 的 minimum/maximum 区间。
- 实际依赖 Pack 版本位于依赖声明的 minimum/maximum 区间。
- 缺失依赖、重复 Pack 和循环依赖均为失败。

拓扑排序在所有可选 Pack 之间按调用方输入顺序稳定决胜，因此同一加载顺序会产生
相同的 Pack 顺序和 `RuntimeContentIndex`。

## 3. 作者数据与运行时数据

作者数据：

- CharacterAuthoring

- SkillAuthoring

- PassiveAuthoring

- TraitAuthoring

- EnemyAuthoring

- StatusEffectAuthoring

- MapAuthoring

- EncounterScheduleAuthoring

- EvolutionAuthoring

- SynergyAuthoring

烘焙为：

- RuntimeCharacterDefinition

- RuntimeSkillDefinition

- RuntimePassiveDefinition

- RuntimeTraitDefinition

- RuntimeEnemyDefinition

- RuntimeStatusDefinition

- RuntimeMapDefinition

- RuntimeEncounterSchedule

- RuntimeEvolutionDefinition

- RuntimeSynergyDefinition

运行时定义只含数字、布尔、稳定 ID/运行时索引、标签、紧凑数组、操作码和视觉资源 ID。不得包含 Unity Object。

M1 已实现的最小运行时定义为：

- `RuntimeCharacterDefinition`：本地化 Key、基础生命/速度、初始技能 ID。
- `RuntimeSkillDefinition`：Schema 1/2 为本地化 Key 和冷却元数据；Schema 3 增加 M4
  可执行模块数据，旧格式不会被静默推断为可执行技能。
- `RuntimeEnemyDefinition`：本地化 Key、生命和碰撞半径；不含实体或刷怪逻辑。
- `RuntimeMapDefinition`：本地化 Key、Runtime Provider ID 和 Scene Address；不加载场景。
- `RuntimeStatusDefinition`（M3 / Schema 2）：生命周期、叠层、驱散、免疫和经烘焙的
  通用行为；不持有 Unity Object 或调用方提供的行为载荷。

`BakedContentCatalog` 保存一个纯 `ContentPackManifest`、按作者顺序排列的
`RuntimeContentDefinition[]` 和 SHA-256 `ContentHash`。磁盘 JSON 使用只含字符串、
数字、布尔和数组的 DTO；Unity `TextAsset` 只存在于最外层 Bootstrap，
不进入 Runtime Catalog。

Hash 按固定字段顺序、长度前缀字符串、invariant 数字格式和作者定义顺序计算；
Hash 不包含 Unity 实例 ID，也不使用反射序列化。加载 JSON 时重新计算并拒绝不一致。
Catalog、Manifest、运行时定义和 Registry 对外只暴露不可变集合视图，调用方不能通过
`IReadOnlyList` 强制转换回内部数组并绕过验证或使已计算 Hash 失效。

## 4. 角色

> CharacterDefinition  
> ├─ Id  
> ├─ LocalizedNameKey  
> ├─ LocalizedDescriptionKey  
> ├─ BaseStatBlock  
> ├─ StartingSkillIds\[\]  
> ├─ StartingPassiveIds\[\]  
> ├─ TraitIds\[\]  
> ├─ AllowedTagRules\[\]  
> ├─ VisualProfileId  
> ├─ AudioProfileId  
> └─ UnlockConditionId

角色特殊能力由 Trait 表达，不为每个角色创建专用 Controller 子类。

## 5. 技能

技能由五部分组合：

> Trigger + Targeting + Delivery + Effects + LevelPatches

Trigger：Timer、OnHit、OnKill、OnDamageTaken、OnPickup、OnDistanceMoved、OnStatusApplied、Manual（预留）。

Targeting：Self、Nearest、Random、LowestHealth、HighestHealth、Cone、Circle、Ring、Line、PlayerAim、RandomPointAroundPlayer。

Delivery：Instant、Projectile、Area、Aura、Orbit、Beam、Chain、Summon、Trap。

Effects：Damage、Heal、ApplyStatus、RemoveStatus、Knockback、Pull、ModifyStat、SpawnEntity、SpawnSecondarySkill、GrantShield、GainResource、Execute、Split、Repeat。

### 5.1 M4 Skill Schema 3

Schema 3 的 `skill` 定义新增：

```text
RuntimeSkillDefinition
├─ Id / Localization Keys / Tags
├─ CooldownSeconds / ResourceCost
├─ Trigger: SkillModuleDefinition
├─ Condition: SkillModuleDefinition
├─ Targeting: SkillModuleDefinition
├─ Delivery: SkillModuleDefinition + PresentationId
├─ Effects: EffectOp[]
└─ LevelPatches: SkillLevelPatch[]
```

`SkillModuleDefinition` 是稳定 Module ContentId 加 `Value0..3`、`Int0..1` 和可选稳定
PresentationId。模块 ID 必须存在于显式白名单和 Composition Root 注册表，运行时不扫描
程序集。

`EffectOp` 在磁盘和 Hash 中保留稳定 `ReferenceId0/1`；成功构建 ContentRegistry 后绑定为
当前加载生命周期的 `RuntimeContentIndex`。ApplyStatus 的 Ref0 必须指向 Status，
SpawnSecondarySkill 的 Ref0 必须指向可执行 Skill。存档只允许稳定 ID。

LevelPatch 的 JSON 字段为 `level`、`path`、`valueType`、`operation`、`floatValue` 或
`integerValue`。Baker/DTO 只接受 `Docs/EFFECT_MODULES.md` 登记的路径并转换为 enum slot；
运行时定义不包含路径字符串。等级从 2 连续增长，同等级按作者顺序应用。

Schema 兼容规则：

- Schema 1：Character/旧 Skill/Enemy/Map。
- Schema 2：在 Schema 1 上增加 Status；旧 Skill 仍不可执行。
- Schema 3：可包含完整 M4 Skill；Schema 3 中的 Skill 必须完整配置模块和至少一个 Effect。
- Schema 4：增加可执行 Enemy、Map 和 Encounter；Schema 4 中的 Enemy/Map 必须包含完整 M5 数据。
- Schema 5：增加 Passive、Trait、Offer、Synergy 和 Evolution；这些定义只能出现在 Schema 5 Pack。
- Schema 1/2/3/4 继续可加载，现有 Catalog Hash 不因新字段改变；升级内容必须重 Bake。

四个 M4 Fixture 使用 `test.*` ContentId 和 `placeholder.presentation.*` 表现 ID，属于
development-only Placeholder 内容，不得进入 release label。

## 6. 构筑

“火焰流”“召唤流”“暴击流”不建立硬编码类。构筑状态来自：

> BuildState  
> ├─ OwnedSkills  
> ├─ OwnedPassives  
> ├─ ActiveTraits  
> ├─ ActiveStatuses  
> ├─ ContentTags  
> ├─ EvolutionEligibility  
> └─ ActivatedSynergies

标签示例：

> element.fire  
> element.ice  
> delivery.projectile  
> delivery.aura  
> damage.dot  
> mechanic.summon  
> mechanic.critical  
> weapon.magic  
> weapon.melee

## 7. 联动与进化

> SynergyDefinition  
> ├─ Conditions\[\]  
> │ ├─ OwnsContent  
> │ ├─ HasTagCount  
> │ ├─ SkillLevelAtLeast  
> │ ├─ StatAtLeast  
> │ └─ MapHasTag  
> └─ Outputs\[\]  
> ├─ AddModifier  
> ├─ UnlockOffer  
> ├─ AddEffectOp  
> ├─ TransformSkill  
> └─ GrantTrait
>
> EvolutionDefinition  
> ├─ RequiredSkillId  
> ├─ RequiredSkillLevel  
> ├─ RequiredPassiveIds\[\]  
> ├─ AdditionalConditions\[\]  
> ├─ ResultSkillId  
> └─ ConsumePolicy

## 8. 地图

> MapDefinition  
> ├─ Id  
> ├─ LocalizedNameKey  
> ├─ RuntimeProviderId  
> ├─ SceneAddress  
> ├─ MapTags\[\]  
> ├─ BoundsMode  
> ├─ SeedRules  
> ├─ EncounterScheduleId  
> ├─ EnemyPoolId  
> ├─ LootTableId  
> ├─ EnvironmentModifiers\[\]  
> ├─ VisualProfileId  
> ├─ AudioProfileId  
> └─ UnlockConditionId

## 9. 遭遇表

> EncounterSchedule  
> ├─ Phases\[\]  
> │ ├─ StartTime  
> │ ├─ EndTime  
> │ ├─ SpawnBudgetCurve  
> │ ├─ SpawnIntervalCurve  
> │ ├─ EnemyEntries\[\]  
> │ ├─ EliteRules\[\]  
> │ └─ BossRules\[\]  
> └─ GlobalRules

## 10. 内容验证

构建前至少检查：

- ID 格式与重复

- 引用缺失

- 内容包依赖缺失或循环

- 技能等级不连续

- 不可达进化条件

- 非法概率、负冷却、空掉落表

- 缺失本地化 Key

- Placeholder 进入 Release

- 正式资源无来源记录

- Map 无 Encounter

- Enemy 无碰撞半径

- 状态或技能触发链可能无限递归

M1 当前自动执行基础门禁：canonical ID、重复 ID、缺失内容引用、Pack 依赖缺失/循环
以及 Schema、游戏和依赖版本不兼容。后续条目随对应里程碑的数据字段落地后启用，
不以空验证器提前宣称通过。

Registry 加载是事务式的：先对完整 Catalog 集合验证并拓扑排序，再建立临时
`ContentId → ContentRegistryEntry` 和 index 数组，全部成功后一次替换当前状态。
重复 ID 的错误同时记录两侧 Pack 和作者资产路径；不允许最后加载者覆盖。

## 11. M1 测试内容包

唯一 M1 测试包位于：

```text
Assets/GameAssets/Placeholder/TestContent/
├─ TestM1ContentPack.asset
├─ TestCharacter.asset
├─ TestSkill.asset
├─ TestEnemy.asset
├─ TestMap.asset
└─ TestM1ContentPack.baked.json
```

该包只包含程序化 Placeholder 元数据，不包含正式美术、音频或第三方资源。
Bootstrap Scene 显式引用 baked JSON，启动时校验 Hash、加载 Registry、输出 Pack
与条目数量并进入空 MainMenu；不会进入战斗。

新增内容的可重复步骤见 `Docs/CONTENT_AUTHORING_WORKFLOW.md`。

## 12. M3 状态 Schema 2

Schema 2 首次定义 `status` kind。Schema 1 继续完整支持 M1 的 Character、Skill、Enemy
和 Map，但不得包含状态。需要状态的 Pack 必须声明 `schemaVersion: 2` 并重新 Bake。

```text
RuntimeStatusDefinition
├─ ContentId / Localization Keys / Tags
├─ StackingPolicy
│  ├─ refresh_duration
│  ├─ add_stacks
│  ├─ replace_if_stronger
│  └─ independent_instances
├─ DurationSeconds / MaxStacks / TickIntervalSeconds
├─ DispelTags[] / ImmunityTags[]
└─ Behavior
   ├─ optional Modifier
   │  └─ StatId / Operation / Value / Priority / StackingGroup
   ├─ optional PeriodicDamage
   │  └─ DamageType / DamageTags / BaseValue / CanCritical /
   │     ProcCoefficient / Knockback
   └─ temporary ShieldCapacity
```

`DamageType`、`DamageTags`、`StatId` 和 `ModifierOperation` 是 `Game.Core` 中的纯领域值；
作者 ScriptableObject 使用 Unity 可序列化的 32 位 DamageTags 掩码，Baker 验证后转换为
运行时 `ulong` 位标记。DTO 对策略、Modifier Operation 和 DamageType 使用稳定文本 token，
不依赖 C# enum 名称或数组位置。

所有行为字段按固定顺序进入确定性 SHA-256 Content Hash。Schema 1 Catalog 的字段顺序、
Hash 和加载路径不变；Schema 2 状态内容变更必须重新 Bake。

M3 状态验证覆盖叠层策略、Duration、MaxStacks、TickInterval、Dispel/Immunity 标签、
Modifier StatId/Operation/有限值、周期伤害类型/Tags/ProcCoefficient、Knockback 和临时
护盾容量。周期伤害必须有正 TickInterval，非法状态不能进入 Runtime Catalog。

M3 独立 Placeholder Pack 位于：

```text
Assets/GameAssets/Placeholder/TestStatusContent/
├─ TestM3StatusContentPack.asset
├─ TestBurningStatus.asset
├─ TestSlowStatus.asset
├─ TestShieldedStatus.asset
└─ TestM3StatusContentPack.baked.json
```

Burning 使用 AddStacks + Fire 周期伤害，Slow 使用 RefreshDuration + MoveSpeed Modifier，
Shielded 使用 ReplaceIfStronger + 临时护盾容量。三者仅为测试 Fixture，不是正式内容。

## 13. M5 敌人、地图与 Encounter Schema 4

Schema 4 首次增加 `encounter` kind，并为 Enemy/Map 增加纯运行时字段。旧 Schema 1–3
Enemy/Map 仍走原构造和原 Hash 字段顺序；只有 Schema 4 定义追加 M5 标记与字段。

```text
RuntimeEnemyDefinition
├─ BaseMaxHealth / CollisionRadius / BaseMoveSpeed / BaseDamage / AttackRange
├─ AttackSkillId
├─ ExperienceReward / LootReward
├─ VisualProfileId / Tags[]
└─ Behavior
   ├─ MovementMode: Chase / KeepDistance / Charge / Ranged
   ├─ PreferredDistance / DecisionInterval
   ├─ ChargeWindup / ChargeDuration / ChargeSpeedMultiplier
   ├─ AttackCooldown
   └─ SeparationRadius / SeparationWeight / ObstacleAvoidanceWeight

RuntimeMapDefinition
├─ RuntimeProviderId / SceneAddress
├─ BoundsMode: Finite / ChunkedInfinite
├─ Minimum / Maximum / ChunkSize / ActiveChunkRadius
├─ EncounterScheduleId / VisualProfileId
├─ Obstacles[]: axis-aligned Minimum / Maximum
└─ Anchors[]: stable ContentId / Position

RuntimeEncounterSchedule
├─ MaximumConcurrentEnemies / MinimumSpawnDistance / MaximumSpawnDistance
└─ Phases[]
   ├─ StartTime / EndTime
   ├─ BudgetPerSecond Start/End
   ├─ SpawnInterval Start/End
   ├─ MaximumConcurrentEnemies / SpawnPattern / optional AnchorId
   ├─ EnemyEntries[]: EnemyId / Weight / BudgetCost / GroupMin/Max / Elite
   └─ BossRules[]: EnemyId / SpawnTime / Pattern / optional AnchorId
```

AttackSkillId 必须解析为 Schema 3+ 可执行 Skill；Map 的 EncounterScheduleId 必须指向
Encounter；Encounter 的普通和 Boss 条目必须指向 Schema 4 Enemy。VisualProfileId 是稳定
表现边界 ID，当前不要求 Registry 中存在 Unity 表现对象。

Encounter phase 必须从 0 秒开始、时间连续、曲线值有限且合法；Boss 时间必须落在所属
phase 内。Ring、Edge、Cluster、Line、Ambush、Portal、FixedAnchor 和 OffscreenRandom 是
Schema 4 的固定 pattern 集合，Portal/FixedAnchor 必须配置稳定 AnchorId。

M5 Placeholder Pack 位于 `Assets/GameAssets/Placeholder/TestM5Content/`，包含四种普通敌人、
一个 Boss、同一个五分钟 Encounter 和两个分别使用 finite/chunked-infinite provider 的地图。
两个 Scene 只含占位根对象；刷怪时间线不存储在 Scene 或 MonoBehaviour 中。

## 14. M6 构筑、候选与进化 Schema 5

Schema 5 新增五个 kind：`passive`、`trait`、`offer`、`synergy`、`evolution`。所有引用在磁盘
保存稳定 ContentId，并在 `BuildRuntimeCatalog` 创建时解析为本次 Registry 的紧凑索引。

```text
RuntimePassiveDefinition
├─ MaximumLevel
└─ LevelModifiers[]: Level + StatId/Operation/Value/Priority/StackingGroup

RuntimeTraitDefinition
└─ Modifiers[]

RuntimeUpgradeOfferDefinition
├─ TargetContentId: executable Skill / Passive / Evolution
├─ Weight / InitiallyUnlocked
├─ Prerequisites[]
└─ MutuallyExclusiveIds[]

RuntimeSynergyDefinition
├─ Conditions[]
└─ Outputs[]

RuntimeEvolutionDefinition
├─ RequiredSkillId / RequiredSkillLevel / RequiredPassiveIds[]
├─ AdditionalConditions[]
├─ ResultSkillId
└─ ConsumePolicy: retain_required_passives / consume_required_passives
```

Condition wire token 固定为 `owns_content`、`has_tag_count`、`skill_level_at_least`、
`stat_at_least`、`map_has_tag`。Synergy Output 固定为 `add_modifier`、`unlock_offer`、
`add_effect_op`、`transform_skill`、`grant_trait`。新增操作必须先通过 Change Request，不能按
具体流派 ContentId 分支。

Offer 的权重必须为正有限值；条件操作数、Modifier、Effect 引用和目标类型在完整 Catalog
集合上验证。技能/被动已满级、槽位已满、互斥命中、前置不满足、未解锁或进化不具备资格时
不会进入候选池。Banish 作用于本次 Run 的 Offer ID，不修改内容资产。

M6 Placeholder Pack 位于 `Assets/GameAssets/Placeholder/TestBuildContent/`，包含两个 Passive、
一个 Trait、两个 Synergy、一个 Evolution 和五个 Offer。它依赖 M4 测试技能 Pack，只用于
开发验证，不是正式构筑或平衡内容。
