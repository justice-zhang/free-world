# G1.5 六类敌人与四种精英词缀实施切片

## 1. 范围与边界

本工作包实现 M07 的纯内容与 Simulation 执行切片：六类普通敌人、九个攻击/辅助技能、三个增益状态、
四个可组合精英词缀、两个精英死亡 Reward 和通用 Spawn 绑定。Pack 为 `qinglan.pack.demo` 0.4.0 / 
Content Schema 6，共 93 个定义；所有新增资产位于程序化 Placeholder 目录。

本包不创建 12 分钟 Encounter、Boss、异相灵核三选一 UI 或正式 VFX/音频。G1.6 把六敌人与 Affix Pool
接入时间轴并运行 21,600 Tick；G2.3 消费已绑定的异相灵核 Reward；G2.6/G3 才做可读轮廓、预警、
音效与实机手感验收。

## 2. 六类敌人冻结

| Enemy | Health / Speed / Damage | Movement | AttackSkill | 压力与边界 |
|---|---:|---|---|---|
| 草灵 | 18 / 2.8 / 2 | Chase | `qinglan.skill.enemy.grass_spirit_aura` | 1.2 接触 Aura，0.6s 生命周期 |
| 纸鹤符灵 | 22 / 3.3 / 4 | Charge | `qinglan.skill.enemy.paper_crane_dive` | 0.45s 前摇、0.55s 锁向冲刺、2.8×速度 |
| 木制剑傀 | 65 / 1.7 / 7 | Chase | `qinglan.skill.enemy.wooden_puppet_attack` | 通用状态提供 0.8 击退抗性，次级技能重斩 |
| 石灯守卫 | 35 / 1.6 / 5 | Ranged | `qinglan.skill.enemy.stone_lantern_bolt` | 9 单位保持距离；弹体 7u/s、2.5s 寿命 |
| 鸣风铃灵 | 28 / 2.0 / 0 | KeepDistance | `qinglan.skill.enemy.wind_bell_support` | 5 单位内最近 6 友军；护盾 8、不含自身、不叠加 |
| 爆裂种囊 | 26 / 1.3 / 8 | Chase | `qinglan.skill.enemy.explosive_seed_burst` | 2.8s 周期、2.6 半径、0.25s 短寿命危险区 |

全部行为继续由稠密 `EnemyDecisionSystem` 推进。技能只使用通用 Trigger/Targeting/Delivery/Effect；
木制剑傀的抗击退由 RefreshDuration 状态修正，爆裂种囊采用设计允许的“延迟爆裂”而非 EnemyId 死亡
特判。没有新增逐敌人 `MonoBehaviour.Update`、Controller、NavMeshAgent 或 Scene 依赖。

## 3. 四种词缀

| Affix | Required / Excluded | 输出 | Generation | Reward multiplier |
|---|---|---|---:|---:|
| 狂奔 | normal / fast,boss | MoveSpeed +35%、AttackSpeed +20% Trait | 0 | 1.10 |
| 结界 | normal / support,boss | 2.6s 周期重建 18 容量护盾 | 0 | 1.15 |
| 分裂 | normal / boss,environment,explosive | 死亡生成 2 个 0.35×同类子体 | 1 | 1.10 |
| 震地 | normal / boss,environment,explosive | 3.5s 周期、3 半径短时冲击区 | 0 | 1.15 |

Encounter 每个 Elite 从 canonical Affix Pool 用独立 Encounter RNG 选至多两个合法词缀。候选同时检查
Enemy Tags、已选 Affix Tags 和反向 Excluded Tags；Boss 直接返回空组合。空 Affix Pool 或旧 SpawnRequest
仍走历史 Elite 1.5 倍路径，保证旧 Schema 4/5 行为不变。

SpawnRequest 的 Affix 绑定是 internal 固定两槽，不增加 Simulation 公共 API。创建时一次安装 Modifier、
附加 Skill 与 RewardMultiplier；Tick 不再解析 ContentId。分裂子体 `Elite=false`、无 Affix、Generation=1，
生命/伤害/XP/Loot 均为 0.35，Cleanup 延迟创建；一代子体死亡不再产生子体。

## 4. ADR 0016 通用追加

- `base.targeting.allies_circle`：V0 半径、I0 最大数；排除 Owner/死亡中/敌对 Actor，按距离稳定截断。
- `RewardOperationCode.SpawnEnemy = 11`：I0 1—2、V0 `(0,1]` 子体倍率、Ref0 可选 Enemy。
- `RuntimeEliteAffixDefinition`：保留旧构造函数，追加 MaximumGeneration、RewardMultiplier 与新构造函数。
- Content Runtime API 从 918 条增加至 923 条；其余四个冻结程序集签名完全不变。

`qinglan.reward.elite.afflicted_core` 与 `qinglan.reward.elite.splitting` 已绑定到词缀。G1.5 只执行
`SpawnEnemy` 这一结构输出；AddCurrency/三选一等其余 Reward 操作仍由 G2.3 的 RewardResolution 处理，
不能把“已绑定”误报成完整奖励闭环。

## 5. 内容拓扑

Pack 在 G1.4 的 68 个定义上新增 25 个定义：

| 类型 | 数量 | 说明 |
|---|---:|---|
| Enemy | 6 | 四种既有 MovementMode 的六个压力角色 |
| Skill | 9 | 六主技能、剑傀次级重斩、结界、震地 |
| Status | 3 | 剑傀架势、风铃护持、精英结界 |
| Trait | 1 | 狂奔修正输出 |
| Reward | 2 | 异相灵核、分裂灵核 |
| EliteAffix | 4 | 狂奔、结界、分裂、震地 |
| 合计新增 / Pack 总数 | 25 / 93 | Pack 0.4.0 |

所有玩家可见名称和描述已写入英文/简中 Localization Key；PresentationId 均是程序化 Placeholder
稳定 ID，不导入参考项目或第三方资源。

## 6. 固定 Seed 与性能验收

专项 Seed 为 `0x473135454E454D59`。测试覆盖：相同 RNG 输出相同两词缀组合；高速纸鹤排除狂奔；
Boss 无普通词缀；狂奔实际修改 MoveSpeed；结界附加技能产生 18 护盾；鸣风铃只护持最近六名友军；
分裂只产生一代两个 0.35×非 Elite 子体。

性能门禁使用 600 草灵、1,200 投射物、2,000 拾取物、100 VFX、900 Tick：Simulation p99
4.7683ms，Render p99 0.7472ms，热路径 0 B、0 GC，实体数量与确定性 Checksum 通过。首轮虽然热路径
0 B，但因测量场景在 GC 基线之后才分配，测量窗出现一次全代 GC，结果为 FAIL；性能 Harness 已改为
完成场景/数组/VFX 预分配后再收集和建立 GC 基线，复测 PASS。

## 7. 后续前置条件

- G1.6 创建 `qinglan.encounter.old_court.demo_12m`，按阶段接入六 Enemy 与四 Affix Pool，执行双实例
  21,600 Tick、精英/并发/停止生成和固定 Checksum 门禁。
- G1.7 完成完整 Pack、双语/Placeholder 与 Windows Development Build；不得提前声称正式表现完成。
- G2.3 消费 AfflictedCore Reward，完成独立 Reward RNG、三选一、暂停、回退与幂等提交。
- G2.6/G3 补齐轮廓、前摇、音频和可穿行 PlayMode；正式平衡仍未冻结。
