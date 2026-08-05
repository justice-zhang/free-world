# 03 Demo 内容目录与稳定 ID 草案

## 1. ID 治理

- 命名空间统一为 `qinglan.*`；只使用小写字母、数字、下划线和点号。
- 本表全部为 `DRAFT`，首次进入可分发 Catalog 前执行法务、术语、Owner 和重复检查。
- 玩家可见名称不从 ID 派生，只使用本地化 Key。
- 存档保存稳定 ID；`RuntimeContentIndex`、Unity Object、Asset GUID 不进入存档。
- `presentation`、`audio`、`vfx` ID 是表现边界，不冒充 Simulation 内容定义。

## 2. 核心内容

| 类型 | DRAFT ContentId | 名称 | Pack |
|---|---|---|---|
| Character | `qinglan.character.lu_qingye` | 陆青野 | demo.core |
| Trait | `qinglan.trait.lu_qingye.riding_wind` | 乘风 | demo.core |
| Trait | `qinglan.trait.lu_qingye.riding_wind.breeze` | 微风通用输出 | demo.core |
| Trait | `qinglan.trait.lu_qingye.riding_wind.swift` | 疾风通用输出 | demo.core |
| Character Mechanic | `qinglan.mechanic.lu_qingye.riding_wind` | 乘风状态机 | demo.core |
| Skill | `qinglan.skill.weapon.yufeng_sword` | 游风剑 | demo.core |
| Skill | `qinglan.skill.weapon.yellow_talisman` | 镇邪黄符 | demo.core |
| Skill | `qinglan.skill.weapon.lihuo_wheel` | 离火飞轮 | demo.core |
| Skill | `qinglan.skill.weapon.tide_orb` | 听潮珠 | demo.core |
| Skill | `qinglan.skill.weapon.zhenyue_seal` | 震岳印 | demo.core |
| Skill | `qinglan.skill.weapon.spirit_vine_seed` | 灵藤种 | demo.core |
| Passive | `qinglan.passive.treading_wind` | 踏风步 | demo.core |
| Passive | `qinglan.passive.clear_mind` | 清心诀 | demo.core |
| Passive | `qinglan.passive.artifact_control` | 御器篇 | demo.core |
| Passive | `qinglan.passive.domain_expansion` | 开域法 | demo.core |
| Passive | `qinglan.passive.long_breath` | 长息功 | demo.core |
| Passive | `qinglan.passive.spirit_gathering` | 采灵诀 | demo.core |

## 3. 显化与候选

| 类型 | DRAFT ContentId | 结果 | 必要组合 |
|---|---|---|---|
| Evolution | `qinglan.evolution.qinglan_flowing_shadow_sword` | 青岚流影剑 | 游风剑＋踏风步＋显化宝匣 |
| Evolution | `qinglan.evolution.taiyi_spirit_sealing_array` | 太一镇灵符阵 | 镇邪黄符＋清心诀＋显化宝匣 |
| Evolution | `qinglan.evolution.chilu_hundred_craft_wheel` | 赤炉百工轮 | 离火飞轮＋御器篇＋显化宝匣 |
| Evolution | `qinglan.evolution.mirror_sea_tide_wheel` | 镜海潮生轮 | 听潮珠＋开域法＋显化宝匣 |
| Evolution | `qinglan.evolution.mountain_boundary_seal` | 山河镇界印 | 震岳印＋长息功＋显化宝匣 |
| Evolution | `qinglan.evolution.earth_vein_spring_branch` | 地脉生春枝 | 灵藤种＋采灵诀＋显化宝匣 |

显化转换后的可执行 Skill 使用独立稳定 ID，避免把 Evolution 定义 ID 当作运行时技能身份：

| Evolution | Result SkillId |
|---|---|
| 青岚流影剑 | `qinglan.skill.evolved.qinglan_flowing_shadow_sword` |
| 太一镇灵符阵 | `qinglan.skill.evolved.taiyi_spirit_sealing_array` |
| 赤炉百工轮 | `qinglan.skill.evolved.chilu_hundred_craft_wheel` |
| 镜海潮生轮 | `qinglan.skill.evolved.mirror_sea_tide_wheel` |
| 山河镇界印 | `qinglan.skill.evolved.mountain_boundary_seal` |
| 地脉生春枝 | `qinglan.skill.evolved.earth_vein_spring_branch` |

每个 Skill/Passive 等级都需要独立 UpgradeOffer。建议 ID：

```text
qinglan.offer.skill.<short_name>
qinglan.offer.passive.<short_name>
qinglan.offer.evolution.<short_name>
```

Offer 的权重、前置、互斥和满级过滤由 M6 现有候选真值处理；显化宝匣资格若需要“已拾取宝匣”
条件，必须经过 CR-04，不得在 UI 中强行插入 Evolution。

完整 Offer 草案如下；一个 Offer 负责目标内容的首次取得和后续升级，不为每一级复制 ID：

| 目标 | DRAFT OfferId |
|---|---|
| 游风剑 | `qinglan.offer.skill.yufeng_sword` |
| 镇邪黄符 | `qinglan.offer.skill.yellow_talisman` |
| 离火飞轮 | `qinglan.offer.skill.lihuo_wheel` |
| 听潮珠 | `qinglan.offer.skill.tide_orb` |
| 震岳印 | `qinglan.offer.skill.zhenyue_seal` |
| 灵藤种 | `qinglan.offer.skill.spirit_vine_seed` |
| 踏风步 | `qinglan.offer.passive.treading_wind` |
| 清心诀 | `qinglan.offer.passive.clear_mind` |
| 御器篇 | `qinglan.offer.passive.artifact_control` |
| 开域法 | `qinglan.offer.passive.domain_expansion` |
| 长息功 | `qinglan.offer.passive.long_breath` |
| 采灵诀 | `qinglan.offer.passive.spirit_gathering` |
| 青岚流影剑 | `qinglan.offer.evolution.qinglan_flowing_shadow_sword` |
| 太一镇灵符阵 | `qinglan.offer.evolution.taiyi_spirit_sealing_array` |
| 赤炉百工轮 | `qinglan.offer.evolution.chilu_hundred_craft_wheel` |
| 镜海潮生轮 | `qinglan.offer.evolution.mirror_sea_tide_wheel` |
| 山河镇界印 | `qinglan.offer.evolution.mountain_boundary_seal` |
| 地脉生春枝 | `qinglan.offer.evolution.earth_vein_spring_branch` |

### 3.1 隐藏技能与通用构筑内容

隐藏技能不进入普通候选池，只被主技能、阶段、词缀或 Reward 引用：

| 消费者 | DRAFT ContentId | 用途 |
|---|---|---|
| 乘风 | `qinglan.resource.riding_wind` | 真实位移资源 |
| 游风剑 | `qinglan.skill.hidden.yufeng_return` | 回返段 |
| 游风剑 | `qinglan.skill.hidden.riding_wind_blade` | 满风势风刃 |
| 青岚流影剑 | `qinglan.skill.hidden.flowing_shadow_wind_trail` | 有界流影风痕 |
| 黄符 | `qinglan.skill.hidden.talisman_detonation` | 标记引爆 |
| 飞轮 | `qinglan.skill.hidden.lihuo_return_explosion` | 回程爆裂 |
| 听潮珠 | `qinglan.skill.hidden.tide_rising` | 涨潮吸附 |
| 听潮珠 | `qinglan.skill.hidden.tide_falling` | 退潮击退/爆发 |
| 震岳印 | `qinglan.skill.hidden.zhenyue_guard_domain` | 安全领域 |
| 震岳印 | `qinglan.skill.hidden.zhenyue_countershock` | 反震 |
| 灵藤种 | `qinglan.skill.hidden.vine_growth` | 藤丛生长 |
| 灵藤种 | `qinglan.skill.hidden.vine_propagation` | 相邻传播 |
| Synergy | `qinglan.synergy.moving_sword_path` | 移动御剑 |
| Synergy | `qinglan.synergy.talisman_detonation` | 符阵爆发 |
| Synergy | `qinglan.synergy.living_garden` | 草木铺场 |

## 4. 状态

| DRAFT ContentId | 用途 | 现有 Schema 映射 |
|---|---|---|
| `qinglan.status.burning` | 灼烧 DOT | PeriodicDamage |
| `qinglan.status.poisoned` | 中毒 DOT | PeriodicDamage |
| `qinglan.status.slowed` | 减速 | MoveSpeed Modifier |
| `qinglan.status.rooted` | 定身 | MoveSpeed Clamp/Override；Boss 需抗性层 |
| `qinglan.status.armor_broken` | 破甲 | Armor Modifier |
| `qinglan.status.marked` | 标记/引爆/增伤 | Schema 6 状态查询/原子消费/引爆 |
| `qinglan.status.damage_immunity` | 护心玉短时免伤 | 通用 `base.damage_policy.immune.*` 标签 |
| `qinglan.status.riding_wind` | 乘风档位表现标记 | 只作表现标签；真值由角色机制模块 |

## 5. 敌人、词缀与 Boss

| 类型 | DRAFT ContentId | 名称 |
|---|---|---|
| Enemy | `qinglan.enemy.grass_spirit` | 草灵 |
| Enemy | `qinglan.enemy.paper_crane_spirit` | 纸鹤符灵 |
| Enemy | `qinglan.enemy.wooden_sword_puppet` | 木制剑傀 |
| Enemy | `qinglan.enemy.stone_lantern_guard` | 石灯守卫 |
| Enemy | `qinglan.enemy.wind_bell_spirit` | 鸣风铃灵 |
| Enemy | `qinglan.enemy.explosive_seed_pod` | 爆裂种囊 |
| Affix | `qinglan.affix.rampaging` | 狂奔 |
| Affix | `qinglan.affix.barrier` | 结界 |
| Affix | `qinglan.affix.splitting` | 分裂 |
| Affix | `qinglan.affix.quaking` | 震地 |
| Boss | `qinglan.enemy.boss.zhezhi` | 试剑傀·折枝 |
| Boss | `qinglan.enemy.boss.tingfeng` | 守庭剑傀·听风 |

敌人攻击技能使用 `qinglan.skill.enemy.<enemy>.<attack>`；Boss 招式使用
`qinglan.skill.boss.<boss>.<move>`。词缀必须是组合定义，不得以四倍 EnemyDefinition 复制实现。

完整攻击/招式草案：

| Owner | DRAFT SkillId |
|---|---|
| 草灵 | `qinglan.skill.enemy.grass_spirit_aura` |
| 纸鹤符灵 | `qinglan.skill.enemy.paper_crane_dive` |
| 木制剑傀 | `qinglan.skill.enemy.wooden_puppet_attack` |
| 木制剑傀 | `qinglan.skill.enemy.wooden_puppet_heavy_slash` |
| 石灯守卫 | `qinglan.skill.enemy.stone_lantern_bolt` |
| 鸣风铃灵 | `qinglan.skill.enemy.wind_bell_support` |
| 爆裂种囊 | `qinglan.skill.enemy.explosive_seed_burst` |
| 结界词缀 | `qinglan.skill.elite.barrier_pulse` |
| 震地词缀 | `qinglan.skill.elite.quaking_pulse` |
| 折枝 | `qinglan.skill.boss.zhezhi.horizontal_trial` |
| 折枝 | `qinglan.skill.boss.zhezhi.falling_wood_shadow` |
| 折枝 | `qinglan.skill.boss.zhezhi.training_dummy` |
| 听风 | `qinglan.skill.boss.tingfeng.sword_qi` |
| 听风 | `qinglan.skill.boss.tingfeng.charge` |
| 听风 | `qinglan.skill.boss.tingfeng.obscuring_windfield` |
| 听风 | `qinglan.skill.boss.tingfeng.false_sword_chime` |
| 听风 | `qinglan.skill.boss.tingfeng.remnant_sword` |
| 听风 | `qinglan.skill.boss.tingfeng.crossing_wind_scar` |
| 听风 | `qinglan.skill.boss.tingfeng.undying_oath` |

## 6. 地图、Encounter、目标和事件

| 类型 | DRAFT ContentId | 名称 |
|---|---|---|
| Map | `qinglan.map.old_court` | 青岚旧庭 |
| Encounter | `qinglan.encounter.old_court.demo_12m` | 旧庭十二分钟时间轴 |
| Objective | `qinglan.objective.wind_altar.listen` | 听风台 |
| Objective | `qinglan.objective.wind_altar.guide` | 引风台 |
| Objective | `qinglan.objective.wind_altar.stop_balance` | 止衡台 |
| Event | `qinglan.event.wind_vein_riot` | 风脉暴动 |
| Event | `qinglan.event.herb_garden_revival` | 药圃复苏 |
| Event | `qinglan.event.old_sword_resonance` | 旧剑共鸣 |
| Landmark | `qinglan.landmark.wind_vein_stele` | 风脉旧碑 |
| Landmark | `qinglan.landmark.sealed_sword_cache` | 藏剑封存匣 |
| Landmark | `qinglan.landmark.herb_garden_variant` | 药圃异种 |
| Landmark | `qinglan.landmark.broken_wall_sword_mark` | 断墙剑痕 |
| Landmark | `qinglan.landmark.guest_pavilion_letter` | 迎客亭旧信 |

Map Anchor ID 使用 `qinglan.anchor.old_court.<purpose>`，Scene 名称不得替代稳定 ID。目标、事件与
地标 Definition 需要 CR-06/CR-07 决策后才能进入 Runtime Catalog。

## 7. 即时灵物、奇物与关键奖励

| 类型 | DRAFT ContentId | 名称/效果 |
|---|---|---|
| Pickup | `qinglan.pickup.greenwood_dew` | 青木露：恢复 |
| Pickup | `qinglan.pickup.boundary_talisman` | 定界符：短时控场 |
| Pickup | `qinglan.pickup.thunder_jade` | 震霄雷玉：清场 |
| Pickup | `qinglan.pickup.spirit_gourd` | 聚灵葫芦：吸取合格拾取物 |
| Pickup | `qinglan.pickup.heart_guard_jade` | 护心玉：短时免伤 |
| Pickup | `qinglan.pickup.riding_wind_feather` | 乘风羽：穿阵/移动 |
| Relic | `qinglan.relic.broken_sword_tassel` | 断剑穗：重复释放 |
| Relic | `qinglan.relic.wind_vein_copper` | 风脉铜片：移动蓄势 |
| Relic | `qinglan.relic.herb_garden_seed_pod` | 药圃种囊：治疗溢出 |
| Relic | `qinglan.relic.listening_wind_core` | 听风木芯：自动索敌 |
| Relic | `qinglan.relic.old_court_bell` | 旧庭残钟：周期控场 |
| Relic | `qinglan.relic.blank_sword_trial_token` | 无字试剑牌：高风险 Boss 输出 |
| Reward | `qinglan.reward.manifestation_chest` | 显化宝匣 |
| Currency | `qinglan.currency.spirit_sand` | 灵砂 |
| Key Progress | `qinglan.progress.region_mark.qinglan` | 青岚山河脉印 |

## 8. 局外内容

| 类型 | DRAFT ContentId | 名称 |
|---|---|---|
| Facility | `qinglan.facility.vein_inquiry_platform` | 问脉台 |
| Facility | `qinglan.facility.scroll_pavilion` | 藏卷楼 |
| Facility | `qinglan.facility.hundred_artifact_pavilion` | 百器阁 |
| Facility | `qinglan.facility.myriad_phenomena_pavilion` | 万象阁 |
| Story | `qinglan.story.lu_qingye.hearing_sword` | 山脚听剑 |
| Story | `qinglan.story.lu_qingye.old_sword_and_gourd` | 旧剑与酒葫 |
| Story | `qinglan.story.lu_qingye.refusing_inheritance` | 不认传承 |
| Insert | `qinglan.insert.qinglan_wind_pattern` | 青岚风纹片 |
| Insert | `qinglan.insert.herb_garden_spring_clasp` | 药圃生春扣 |
| Insert | `qinglan.insert.old_court_vein_needle` | 旧庭寻脉针 |

行脉节点使用以下规则：

```text
qinglan.meta.lu_qingye.innate.01..04
qinglan.meta.lu_qingye.movement.01..04
qinglan.meta.lu_qingye.mind.01..04
```

完整节点草案：

| 分支 | DRAFT MetaNodeId | 功能占位（非正式名称） |
|---|---|---|
| 本命 | `qinglan.meta.lu_qingye.innate.01` | 本命器候选亲和 |
| 本命 | `qinglan.meta.lu_qingye.innate.02` | 乘风阈值微调 |
| 本命 | `qinglan.meta.lu_qingye.innate.03` | 本命器预览/信息 |
| 本命终端 | `qinglan.meta.lu_qingye.innate.04` | 终式资格方向 |
| 身法 | `qinglan.meta.lu_qingye.movement.01` | 移动宽容 |
| 身法 | `qinglan.meta.lu_qingye.movement.02` | 受击恢复窗口 |
| 身法 | `qinglan.meta.lu_qingye.movement.03` | 拾取/路线宽容 |
| 身法终端 | `qinglan.meta.lu_qingye.movement.04` | 路线强化方向 |
| 心性 | `qinglan.meta.lu_qingye.mind.01` | 候选信息 |
| 心性 | `qinglan.meta.lu_qingye.mind.02` | Reroll/Banish 容量 |
| 心性 | `qinglan.meta.lu_qingye.mind.03` | 失败保留/地标信息 |
| 心性终端 | `qinglan.meta.lu_qingye.mind.04` | 高风险选择方向 |

完整藏品草案：

| DRAFT CollectibleId | 专题 | 正式名称 |
|---|---|---|
| `qinglan.collectible.old_court.01` | 止衡剑庭 | G0 叙事任务锁定 |
| `qinglan.collectible.old_court.02` | 止衡剑庭 | G0 叙事任务锁定 |
| `qinglan.collectible.old_court.03` | 沈停云线索 | G0 叙事任务锁定 |
| `qinglan.collectible.old_court.04` | 沈停云线索 | G0 叙事任务锁定 |
| `qinglan.collectible.old_court.05` | 旧庭生活 | G0 叙事任务锁定 |
| `qinglan.collectible.old_court.06` | 旧庭生活 | G0 叙事任务锁定 |

## 9. 表现 ID

```text
qinglan.presentation.character.lu_qingye
qinglan.presentation.enemy.<short_name>
qinglan.presentation.skill.<short_name>
qinglan.presentation.map.old_court
qinglan.vfx.<feature>
qinglan.audio.<feature>
```

这些 ID 只解析 Profile/Addressables，不进入模拟分支。未命中时 Development 使用程序化 fallback；
Release 必须被验证器阻断。

## 10. 本地化 Key

| 内容 | Key 模式 |
|---|---|
| 名称 | `content.<type>.<short_name>.name` |
| 描述 | `content.<type>.<short_name>.description` |
| 等级变化 | `content.skill.<short_name>.level_<n>` |
| UI | `ui.demo.<page>.<control>` |
| 目标 | `objective.old_court.<short_name>.<state>` |
| Boss 提示 | `boss.<boss>.<move>.warning` |
| 故事 | `narrative.old_court.<arc>.<entry>` |

所有 Key 在 `zh-Hans`、`en` 必须非空，Pseudo 必须完成裁切验证；不得把中文正文写入 ContentId。
