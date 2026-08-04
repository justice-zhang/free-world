# ADR 0013：Qinglan Demo Content Schema 6 与可扩展模块操作数

- 状态：Accepted
- 日期：2026-08-04
- 决策人：依据用户当前连续 Demo 开发指令
- 关联里程碑：G0.3、G1.1、G1.3—G1.5、G2.1—G2.3、G2.5
- 取代：无

## 背景

Schema 5 已能表达技能、敌人、地图、Encounter、被动、Trait、Offer、Synergy 和 Evolution，但
CR-2026-004—014 需要角色机制、通用奖励、地图目标、Boss、精英词缀和局外定义。现有
`SkillModuleDefinition` 也不能让 Condition/Targeting 按稳定状态或标签引用操作数。若把这些能力
写进具体青岚 ContentId、Scene 或 Prefab，将违反内容扩展、纯模拟和稳定 ID 规则。

## 决策

### Schema 6 定义族

Content Schema 6 追加以下稳定 kind，旧 kind 不改名、不重解释：

```text
character_mechanic
reward / pickup / relic
map_objective / map_event / landmark
boss / elite_affix
meta_node / meta_insert / meta_facility / story / collectible
```

Schema 6 的 Character 可选引用 `MechanicIds[]`；Map 可选引用 Objective/Event/Landmark；Encounter
可选引用 Elite Affix Pool 和 Boss。Schema 1—5 DTO、Hash 字段顺序和构造路径保持原样；只有声明
Schema 6 的 Pack 才能包含新 kind 或新字段，并必须从 Authoring 重新 Bake。

### 模块引用操作数与稳定 token

`SkillModuleDefinition` 追加稳定 `ReferenceId0/1` 和 `Tag0/1` 作者/磁盘字段，运行前绑定为紧凑
索引/标签。运行时不保存任意字符串参数。新增模块 token：

- `base.condition.status_count_at_least`、`base.condition.target_has_status`；
- `base.targeting.trigger_position`；
- `base.delivery.outbound_return`；
- `base.effect.consume_status`、`base.effect.detonate_status`。

Reward 操作 token 固定为 `heal`、`apply_status`、`damage_area`、
`collect_eligible_pickups`、`grant_relic_choice`、`grant_evolution_choice`、`add_currency`、
`unlock_content`、`grant_unique`、`trigger_story`。操作只携带纯值、稳定 ID/Tag 和受验证枚举。

### 公共 Stat 与验证

`BuiltInStatIds` 只追加 `base.stat.projectile_speed`、`base.stat.critical_multiplier`、
`base.stat.experience_gain`、`base.stat.knockback_resistance`；`StatCatalog` 只在末尾追加索引，原 14 项
索引和语义不变。每个新定义的引用 kind、有限数值、容量、状态机可达性、互斥、唯一规则和
本地化 Key 在完整 Catalog 上验证；加载仍为全有或全无事务。

## 备选方案

### 方案 A：把所有能力塞入既有 Trait/Enemy/Map 字段

- 优点：新增顶级 kind 较少。
- 缺点：不同生命周期和所有者混在一起，Hash/验证和编辑器工作流不可审计。
- 未采用原因：无法保持通用组合与明确引用类型。

### 方案 B：为青岚角色、Boss、事件和奖励编写专用脚本

- 优点：短期实现直接。
- 缺点：核心程序集出现具体 ID 分支，Scene/Prefab 成为真值，不能无头复现。
- 未采用原因：违反仓库硬约束和商业框架扩展目标。

## 影响

### 正面影响

- 新角色、奖励、Boss、词缀、地图和局外内容可只加数据。
- 所有引用可在 Bake/Load 期验证并在 Tick 前绑定。
- 旧 Schema 和已发布 ContentId 保持可读。

### 负面影响与成本

- `Game.Content.Runtime`、Authoring、DTO/Hash、Editor 和验证器公开 API 会增长。
- G1.1 必须提供旧 Schema 1—5 Golden Catalog 和 Hash 回归。

### 对兼容性的影响

- Content Schema：支持范围从 1—5 扩为 1—6；旧版本不重 Bake。
- Save Schema：内容定义本身不改变存档；Profile 3 由 ADR 0015 处理。
- API：只追加类型/成员/token；基线实施后按冻结协议更新。
- 性能：引用加载期绑定；固定 Tick 禁止字符串查找、反射和临时集合。
- 构建：Build Manifest 的 Content Schema 目标变为 6。
- 资产：不导入外部资产；Presentation/Audio 仍只保存稳定 Profile ID。

## 实施与迁移

1. G1.1 先增加 Schema 6 常量、纯运行时定义、DTO/Hash、Validator 和 Authoring/Baker。
2. 增加模块引用操作数、token 注册和旧 Schema 兼容分支。
3. 追加四个 Stat，保持原索引不变并更新冻结 API 证据。
4. 新建最小 Placeholder Fixture 覆盖每个 kind；正式 Qinglan Pack 只在后续工作包创建。
5. 新 Pack 声明 Schema 6 并重新 Bake；旧 Pack 不迁移、不改 Hash。

## 测试与验收证据

- 测试：Schema 1—5 Golden/Hash、Schema 6 round-trip、全部引用/状态机/互斥负例、模块绑定和 Stat 索引稳定。
- 构建：G1.1 Project Validation；G1.7 Development Build；G3.6 Release Build。
- 性能：Registry/绑定短测和 54,000 Tick 新模块零热路径分配；目标规模沿用 M10 基线比较。
- 日志或产物位置：实施后写入 `TestResults/QinglanDemo/G1.1/` 和对应结果报告。

## 回滚方案

先移除所有未发布 Schema 6 Pack，再移除实现。不得降低已发布 Pack 的版本或复用追加 Stat/token；
Schema 1—5 读取器、Golden Fixture 和原 Hash 始终保留。
