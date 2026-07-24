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

- 发布后不得修改。

- 显示名称与 ID 分离。

- 存档保存稳定 ID，不保存运行时索引。

- 运行时映射为紧凑 RuntimeContentIndex。

- 不只保存 Hash，始终保留原始字符串。

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
