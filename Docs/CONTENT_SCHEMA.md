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
- `RuntimeSkillDefinition`：本地化 Key、冷却元数据；不含执行逻辑。
- `RuntimeEnemyDefinition`：本地化 Key、生命和碰撞半径；不含实体或刷怪逻辑。
- `RuntimeMapDefinition`：本地化 Key、Runtime Provider ID 和 Scene Address；不加载场景。

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
