# 05 Schema 缺口与 Change Request 计划

## 1. 判定

现有 Schema 5 与固定模拟可以直接实现基础伤害/状态、Timer/事件技能、五种 Delivery、六类敌人基础
行为、12 分钟 Encounter、经验升级、被动、候选、Synergy/Evolution 和基本 Settings/Profile。

以下能力不能在不改变体验的前提下由现有模块完整表达，因此标记为 `CR-BLOCKED`。

## 2. 缺口清单

| CR | 通用能力 | Demo 消费者 | 当前缺口 | 优先级 |
|---|---|---|---|---|
| CR-01 | 角色机制资源与真实位移触发 | 乘风、风脉铜片 | 无 OnDistanceMoved/资源档位/受伤降档 | P0 |
| CR-02 | 回返/多段轨迹 Delivery | 游风剑、离火飞轮、终式扩展 | Projectile 只单向生命周期 | P0 |
| CR-03 | 状态条件、计数、消费与引爆 | 黄符、标记、奇物 | Condition 只有 Always；无 ConsumeStatus | P0 |
| CR-04 | 关键掉落资格与受控 Evolution | 显化宝匣、精英三选一 | Offer 只在升级触发；无宝匣选择上下文 | P0 |
| CR-05 | 泛化 Pickup/Reward/Relic | 六灵物、六奇物、藏品、灵砂 | 当前 Pickup 记录仅 XP float | P0 |
| CR-06 | 地图目标/交互/事件状态机 | 三风脉台、三事件、五地标 | Map 只有边界、障碍、锚点 | P0 |
| CR-07 | Boss 阶段、抗性和地图修正 | 折枝、听风 | Enemy 只有单行为/单攻击 Skill | P0 |
| CR-08 | 精英词缀组合 | 狂奔、结界、分裂、震地 | Elite 只有固定 1.5 倍倍率 | P1 |
| CR-09 | 局外内容与 Loadout Schema | 行脉、嵌片、故事、收藏、设施 | Profile 可存 ID，但无可验证 Definition/规则 | P0 |
| CR-10 | 缺失公共属性/伤害规则 | 暴伤、弹速、经验、击退、接触保护 | BuiltInStat 只有 14 项，暴击倍率固定 2 | P1 |
| CR-11 | 完整 Run Recovery | 12 分钟可恢复本局 | 当前只保存种子/选择/初始内容 | P2，可延期 |

## 3. 不接受的伪替代

| 缺口 | 禁止替代 |
|---|---|
| 乘风 | 从 View Transform/每帧距离积累；按陆青野 ID 特判 |
| 回返 | 只提高穿透并把文案写成“回返” |
| 黄符引爆 | 纯周期 AoE 却保留叠印引爆描述 |
| 风脉台 | 只播放动画，不改变 Boss 数值/规则 |
| Boss 阶段 | 仅按时间换 VFX，Simulation 仍无阶段真值 |
| 精英词缀 | 复制 24 个 EnemyDefinition 或只用 1.5 倍生命 |
| 灵物/奇物 | 把非 XP 奖励塞进 XP Value 或 UI 本地状态 |
| 行脉/嵌片 | 直接写 PlayerPrefs、运行时索引或未验证字符串 |

## 4. 合并/拆分建议

- CR-01 与 CR-10 分开：角色资源是通用机制系统，公共 Stat 是基础值域/API 变更。
- CR-04 与 CR-05 可共用 Reward 上下文，但 Evolution 资格仍由 BuildState 持有。
- CR-06 与 CR-07 不合并：地图目标和 Boss 阶段是不同 Owner，只通过稳定修正 ID/状态快照协作。
- CR-08 可在 CR-05 之后实施，以同一奖励系统产生精英灵核；行为组合仍独立。
- CR-09 必须包含 Save Schema 迁移，但不应把局外规则放入 Simulation 固定 Tick。

## 5. 每份 CR 必填

1. 现有模块为何不足，以及所有尝试过的组合；
2. 至少两个可复用消费者；
3. 新 Definition/Module/Operation 的稳定 wire token；
4. Assembly 依赖、公开 API Freeze、Content/Save Schema 影响；
5. 确定性、随机流、Pipeline 和热路径分配影响；
6. 旧内容/旧存档迁移、兼容读取和回滚；
7. EditMode、PlayMode、Validation、性能与 Build 测试；
8. 文档、向导、预览和 Release 门禁更新范围。

## 6. 建议技术边界

这些是评审输入，不是已接受实现：

- `CharacterMechanicDefinition`：稳定资源 ID、积累/消耗事件、阈值、Modifier/Skill 输出；
- `TrajectoryDefinition`：Outbound/Turn/Return 阶段与命中去重策略；
- `SkillConditionDefinition`：StatusCount/ResourceThreshold/TargetState；
- `RewardDefinition`：Pickup、Choice、Guaranteed、Unique、Currency、ContentUnlock 操作；
- `MapObjectiveDefinition`：状态、锚点、条件、事件输出、Boss Modifier 输出；
- `BossPhaseDefinition`：进入条件、技能组、抗性、空间/Encounter 输出；
- `EliteAffixDefinition`：兼容标签、Modifier、Skill、死亡输出与表现 Profile；
- `MetaNode/Insert/Collectible/Story/FacilityDefinition`：解锁、成本、容量、互斥和纯值输出。

所有模块都必须显式注册、运行前绑定为紧凑索引、高频路径不做字符串查找或反射。

## 7. 决策门禁

G0.2 结束时每项只能是：

- `ACCEPTED`：进入 ADR/迁移/测试设计；
- `REJECTED`：同步删除或重写产品承诺；
- `DEFERRED`：明确不阻塞 Demo 的降级范围和玩家文案；
- `SPLIT`：拆成独立可审查 CR。

在状态确定前，依赖模块只可做接口无关的内容清单和程序化视觉原型，不得修改冻结核心程序集。
