# 《剑起青岚》Demo 完整开发结构

- 文档集版本：G0-DRAFT-1
- 适用产品：青岚旧庭 12 分钟垂直切片
- 规范来源：`Docs/Game Proposal/《剑起青岚》游戏系统总纲_V2.0.md`
- 技术基线：Unity `6000.3.20f1`、URP、Windows x64、30 Hz 固定模拟
- 文档状态：开发结构与详细设计；不是已实现、已平衡或已发布内容

## 1. 使用方式

本目录把总纲中的 Demo 承诺拆成可实施的工作结构。实施者应先阅读 00—06，再进入对应模块文档。
任何标记为 `CR-BLOCKED` 的条目在 Change Request、ADR、迁移和测试方案获批前不得实现。

本文档中的 ContentId 均为 `DRAFT`，未发布、未占用兼容性承诺；首次进入可分发 Catalog 前必须由
内容 Owner 审核并转为 `RESERVED`，发布后才成为不可变 ID。

## 2. 总体依赖

```text
G0 方案冻结与缺口决策
  ├─ 内容 ID / Pack / 本地化 / 资产清单
  └─ Change Request / ADR / API 与迁移门禁
       ↓
G1 数据切片
  ├─ 角色、武器、心诀、显化
  ├─ 状态、敌人、Encounter
  └─ Headless 预览、验证和 EditMode
       ↓
G2 可玩切片
  ├─ 地图目标、事件、Boss、拾取与奇物
  ├─ 页面流、据点、存档和表现
  └─ PlayMode、Development Build、实机可读性
       ↓
G3 发布候选
  ├─ 正式资产、双语、可访问性和合规
  ├─ 正式内容性能、Soak、Release Build
  └─ Demo 完成定义逐项证据
```

## 3. 控制文档

| 文档 | 用途 |
|---|---|
| [00_DEMO_BASELINE.md](00_DEMO_BASELINE.md) | 范围、数量、体验闭环、完成定义与非目标 |
| [01_ARCHITECTURE_AND_REPOSITORY.md](01_ARCHITECTURE_AND_REPOSITORY.md) | 目标目录、程序集边界、内容包和资产组织 |
| [02_DELIVERY_ROADMAP.md](02_DELIVERY_ROADMAP.md) | G0—G3 工作包、Git 分支顺序、门禁与退出条件 |
| [03_CONTENT_CATALOG_AND_IDS.md](03_CONTENT_CATALOG_AND_IDS.md) | Demo 内容清单、稳定 ID 草案和本地化命名 |
| [04_CROSS_MODULE_CONTRACTS.md](04_CROSS_MODULE_CONTRACTS.md) | 跨模块状态、命令、事件、时钟和数据所有权 |
| [05_SCHEMA_GAP_AND_CHANGE_REQUESTS.md](05_SCHEMA_GAP_AND_CHANGE_REQUESTS.md) | 现有能力、缺口、替代方案和 CR 优先级 |
| [06_REQUIREMENTS_TRACEABILITY.md](06_REQUIREMENTS_TRACEABILITY.md) | 总纲需求到模块、测试和门禁的追踪矩阵 |
| [07_CHANGE_REQUEST_DECISIONS.md](07_CHANGE_REQUEST_DECISIONS.md) | G0.2 CR-01—CR-11 正式决策、映射与 G0.3 输入顺序 |
| [08_G0_3_CONTRACT_FREEZE.md](08_G0_3_CONTRACT_FREEZE.md) | G0.3 获批 Schema 6、公共 API、Pipeline、Profile 3、迁移与测试契约 |
| [09_G0_4_ASSET_PRODUCTION_PLAN.md](09_G0_4_ASSET_PRODUCTION_PLAN.md) | G0.4 正式资产、音频、字体、本地化、预算与权利生产计划 |
| [10_G1_2_CHARACTER_COMBAT_SLICE.md](10_G1_2_CHARACTER_COMBAT_SLICE.md) | G1.2 陆青野、乘风、七状态与固定 Seed 实施切片 |
| [11_G1_3_WEAPON_SKILL_SLICE.md](11_G1_3_WEAPON_SKILL_SLICE.md) | G1.3 六武器、隐藏技能、预览 Golden 与回返/引爆/相位实施切片 |
| [12_G1_4_PROGRESSION_SLICE.md](12_G1_4_PROGRESSION_SLICE.md) | G1.4 六心诀、18 Offer、三 Synergy、六显化资格与转换实施切片 |
| [13_G1_5_ENEMY_ELITE_SLICE.md](13_G1_5_ENEMY_ELITE_SLICE.md) | G1.5 六敌人、四精英词缀、友军目标与一代分裂实施切片 |

## 4. 模块分支

| 模块 | 详细设计 | G 阶段 |
|---|---|---|
| M01 应用流程与单局生命周期 | [M01_GAME_FLOW.md](Modules/M01_GAME_FLOW.md) | G2 |
| M02 陆青野与“乘风” | [M02_CHARACTER_LU_QINGYE.md](Modules/M02_CHARACTER_LU_QINGYE.md) | G1/G2 |
| M03 属性、伤害与状态 | [M03_COMBAT_STATS_STATUS.md](Modules/M03_COMBAT_STATS_STATUS.md) | G1 |
| M04 六把武器与技能运行时 | [M04_WEAPONS_SKILLS.md](Modules/M04_WEAPONS_SKILLS.md) | G1 |
| M05 六心诀、候选与显化 | [M05_PASSIVES_EVOLUTIONS_OFFERS.md](Modules/M05_PASSIVES_EVOLUTIONS_OFFERS.md) | G1 |
| M06 灵物、奇物与奖励 | [M06_PICKUPS_RELICS_REWARDS.md](Modules/M06_PICKUPS_RELICS_REWARDS.md) | G2 |
| M07 敌人与精英词缀 | [M07_ENEMIES_ELITES.md](Modules/M07_ENEMIES_ELITES.md) | G1/G2 |
| M08 地图、风脉台、事件与地标 | [M08_MAP_OBJECTIVES_EVENTS.md](Modules/M08_MAP_OBJECTIVES_EVENTS.md) | G2 |
| M09 刷怪导演与时间轴 | [M09_ENCOUNTER_DIRECTOR.md](Modules/M09_ENCOUNTER_DIRECTOR.md) | G1/G2 |
| M10 中段与最终 Boss | [M10_BOSSES.md](Modules/M10_BOSSES.md) | G2 |
| M11 局外成长、据点与收藏 | [M11_META_HUB_PROGRESSION.md](Modules/M11_META_HUB_PROGRESSION.md) | G2 |
| M12 UI、输入与可访问性 | [M12_UI_INPUT_ACCESSIBILITY.md](Modules/M12_UI_INPUT_ACCESSIBILITY.md) | G2/G3 |
| M13 表现、资产与音频 | [M13_PRESENTATION_ASSETS_AUDIO.md](Modules/M13_PRESENTATION_ASSETS_AUDIO.md) | G2/G3 |
| M14 存档、本地化与平台 | [M14_SAVE_LOCALIZATION_PLATFORM.md](Modules/M14_SAVE_LOCALIZATION_PLATFORM.md) | G2/G3 |
| M15 内容工具与生产管线 | [M15_CONTENT_TOOLS_PIPELINE.md](Modules/M15_CONTENT_TOOLS_PIPELINE.md) | G0—G3 |
| M16 测试、性能与发布 | [M16_TEST_PERFORMANCE_RELEASE.md](Modules/M16_TEST_PERFORMANCE_RELEASE.md) | G0—G3 |

## 5. 全局门禁

1. 不修改冻结公共 API，除非相应 CR 与 ADR 已接受并完成迁移/回滚设计。
2. 不使用角色、武器、敌人或地图 ContentId 作为核心代码分支条件。
3. Simulation 不引用 Unity Object；UI/View 不写 Store。
4. 正式资产必须通过 provenance、许可证、本地化和 Release 标签校验。
5. 每次只交付一个明确工作包；前一工作包未通过门禁不得开始依赖项。
6. 所有结果只写 `PASS`、`FAIL`、`NOT RUN`，并指向实际证据。
