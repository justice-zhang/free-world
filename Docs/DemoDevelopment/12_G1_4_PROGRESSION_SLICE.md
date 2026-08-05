# G1.4 六心诀、Offer、Synergy 与显化实施切片

## 1. 范围与边界

本工作包实现 M05 的纯内容与 Simulation 构筑真值：六个 5 级心诀、十二个普通升级候选、六个锁定
显化候选、三条目标 Synergy、六条 Evolution 资格链和六个显化结果技能。全部资产位于程序化 Placeholder
目录，Pack 为 `qinglan.pack.demo` 0.3.0 / Content Schema 6。

本包不实现显化宝匣 UI、独立 Reward Choice Context、敌人、Encounter、Player Build 或正式表现资源。
CR-2026-007 的奖励来源、空池回退、幂等提交与历史记录由 G1.7 负责。

## 2. 内容拓扑

```text
普通升级流
  ├─ 6 Skill Offer（初始解锁）
  └─ 6 Passive Offer（初始解锁）

BuildState
  ├─ Skill L8 + 对应 Passive L1 → Evolution Eligible
  ├─ 三组 OwnsContent → Synergy 一次性锁存
  └─ 原子 Transform：Source Skill → Result Skill，保留 Passive

受控显化流（G1.7 消费）
  └─ 6 Evolution Offer（初始锁定，不进入普通升级池）
```

Pack 在 G1.3 的 28 个定义上新增 40 个定义：

| 类型 | 数量 | 说明 |
|---|---:|---|
| Passive | 6 | 每个 MaximumLevel=5 |
| Result Skill | 6 | 每个 Evolution 独立可执行结果 |
| Hidden Skill | 1 | 流影风痕 |
| Evolution | 6 | Source L8＋对应 Passive L1；保留 Passive |
| UpgradeOffer | 18 | 6 Skill＋6 Passive＋6 Evolution |
| Synergy | 3 | 移动御剑、符阵爆发、草木铺场 |
| 合计新增/Pack 总数 | 40 / 68 | Baked Catalog 单 Pack |

## 3. 心诀数值冻结

每个 Modifier 都显式保存 `StatId / Operation / Value / Priority / StackingGroup`。StackingGroup 按心诀、
等级和属性唯一，禁止不同等级互相覆盖。

| 心诀 | L1—L5 修改 | Operation | Priority |
|---|---|---|---:|
| 踏风步 | 每级 MoveSpeed +4% | AddPercent | 100 |
| 清心诀 | 每级 Cooldown -3%、AttackSpeed +3% | AddPercent | 100/110 |
| 御器篇 | L1 ProjectileSpeed +6%；L2 Pierce +1；L3 ProjectileSpeed +8%；L4 ProjectileCount +1；L5 Pierce +1 | AddPercent/AddFlat | 100 |
| 开域法 | 每级 Range +5%、Duration +6% | AddPercent | 100/110 |
| 长息功 | L1 Health +5%；L2 Armor +1；L3 Regeneration +0.25；L4 Health +7%；L5 Armor +2 | AddPercent/AddFlat | 100 |
| 采灵诀 | 每级 Duration +5%、PickupRange +0.5 | AddPercent/AddFlat | 100/110 |

现有 Schema 没有 `PassiveLevelAtLeast`。Demo 把显化需要的心诀等级批准为 L1；不得用某个 Stat 阈值
间接推断心诀等级，因为其他 Trait/Synergy 也可改变同一 Stat。

## 4. 六条显化组合

| Evolution | Source → Result | 数据组合 | 有界条件 |
|---|---|---|---|
| 青岚流影剑 | 游风剑 → `qinglan.skill.evolved.qinglan_flowing_shadow_sword` | Timer 同时调用基础回返剑和当前位置风痕 | 风痕 1.5 秒；Area 不递归 |
| 太一镇灵符阵 | 镇邪黄符 → `qinglan.skill.evolved.taiyi_spirit_sealing_array` | 1.6 秒符阵周期叠印，复用原子 DetonateStatus | 4 单位范围；每 0.5 秒结算 |
| 赤炉百工轮 | 离火飞轮 → `qinglan.skill.evolved.chilu_hundred_craft_wheel` | 双目标往返轮，回收调用既有范围回爆 | 目标数 2；每相命中额度 3 |
| 镜海潮生轮 | 听潮珠 → `qinglan.skill.evolved.mirror_sea_tide_wheel` | 复用涨/退潮隐藏技能并按 ActivationSequence 交替 | 无随机相位；1.5 秒间隔 |
| 山河镇界印 | 震岳印 → `qinglan.skill.evolved.mountain_boundary_seal` | OnDamageTaken 范围反震并调用护域 Shield | 1.2 秒冷却；不是无敌 |
| 地脉生春枝 | 灵藤种 → `qinglan.skill.evolved.earth_vein_spring_branch` | OnKill 固定调用主藤丛和单代传播 | 每次最多 1 主区＋2 邻区；不递归 |

青岚流影剑的风痕使用固定 Timer 采样 Owner 当前位置。移动时形成轨迹采样；现有 Skill Trigger 不提供通用
真实移动事件，因此当前静止时不会抑制风痕。若 G2 手感门禁要求严格“只有真实位移才落痕”，必须新增
通用移动 Trigger CR，不能在 SkillRuntime 按 `qinglan.*` ID 分支。

## 5. Offer 与显化边界

- `qinglan.offer.skill.*` 六项和 `qinglan.offer.passive.*` 六项 `InitiallyUnlocked=true`；同一 Offer 同时负责
  首次取得和后续升级，由 BuildState 统一过滤满级、满槽、前置与互斥。
- `qinglan.offer.evolution.*` 六项 `InitiallyUnlocked=false`；普通 `OfferGenerator` 无法抽到它们。
- BuildState 仍实时计算六个 Evolution 的 Eligible 状态；G1.7 的受控奖励适配器只可提交已冻结的 Eligible
  ID，并复用 `ApplyOffer` 的原子 Transform。
- Evolution 结果替换 Source Skill，结果从 L1 开始，Required Passive 按
  `RetainRequiredPassives` 保留；同一事务后重算标签、Synergy 与 Evolution 资格。

## 6. 三条目标构筑

| Synergy | 条件 | 输出 | 目标构筑 |
|---|---|---|---|
| `qinglan.synergy.moving_sword_path` | Owns 游风剑＋踏风步 | ProjectileSpeed +12% | 移动御剑 |
| `qinglan.synergy.talisman_detonation` | Owns 镇邪黄符＋清心诀 | 黄符每次命中追加 1 层 Mark | 符阵爆发 |
| `qinglan.synergy.living_garden` | Owns 灵藤种＋采灵诀 | Duration +15% | 草木铺场 |

Synergy 遵守 M6 的一次激活锁存语义。输出由通用 AddModifier/AddEffectOp 执行，Simulation 没有具体
Synergy ID 判断。

## 7. 固定 Seed 验收

Seed：`0x47313450524F4752`；窗口 6 秒；20 个静止目标；显化结果 L1。

| Result Skill | DPS | Hits | Triggers |
|---|---:|---:|---:|
| 青岚流影剑 | 165.6667 | 179 | 15 |
| 太一镇灵符阵 | 467.5 | 340 | 89 |
| 赤炉百工轮 | 128 | 84 | 9 |
| 镜海潮生轮 | 46.66666 | 28 | 8 |
| 山河镇界印 | 58.33333 | 25 | 28 |
| 地脉生春枝 | 215 | 480 | 24 |

每项执行两次并要求 Summary 完全相等、固定 Tick Managed Allocation=0 B、Hits 不超过 2,000。地脉
生春枝的第一版曾在 Area 每次命中时生成传播，产生 15,990 Hits；该设计已删除且不计为通过证据。

## 8. 测试矩阵

| 检查 | 自动化 |
|---|---|
| Pack/Schema/Hash/数量 | 68 定义、0.3.0、Schema 6、固定 ContentHash |
| Passive | 六项 5 级连续覆盖、显式 Modifier、唯一 StackingGroup |
| Offer | 相同 Seed 序列一致；战斗 RNG 调用不影响；普通池无 Evolution |
| Synergy | 三组 OwnsContent 激活；ProjectileSpeed/Duration 实值校验 |
| Evolution | 六组 Source L8＋Passive L1；缺 Passive 不合格；原子替换并保留 Passive |
| Preview | 六显化精确 Golden、确定性、0 B、有界 Hits |
| 回归 | 全量 EditMode、全量 PlayMode、Project Validation、API Freeze、性能短测 |

## 9. 后续前置条件

- G1.5 只能新增 M07 六敌人和四词缀，不修改本包资格/Offer 真值。
- G1.7 必须完成 CR-2026-007 Reward Choice Context，关闭 QD-KI-009，并证明空资格 fallback、一次提交、
  独立随机流和暂停语义。
- G2.6 才验证卡牌关系、不可选原因和键鼠/手柄交互；G3.4 才冻结正式平衡。
