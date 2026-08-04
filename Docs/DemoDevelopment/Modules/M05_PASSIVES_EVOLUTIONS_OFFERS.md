# M05 六心诀、候选与器法合鸣

## 1. 槽位与等级

- Demo 公共武器容量 3；本命器固定 1；心诀槽 4；奇物槽 3。
- 武器 8 级、心诀 5 级；升级默认 3 选 1。
- 候选过滤、满级、满槽、前置、互斥、Banish、Reroll 和 Skip 全部由 BuildState/OfferGenerator 处理。
- UI 只显示候选和关系，不自行增权或强塞显化。

## 2. 心诀设计

| 心诀 | 主要 Stat/规则 | 关联构筑 | 风险 |
|---|---|---|---|
| 踏风步 | MoveSpeed、移动宽容、乘风亲和 | 游风剑 | 移速过高导致碰撞/相机问题 |
| 清心诀 | Cooldown/AttackSpeed、标记稳定 | 黄符 | 与两个攻速 Stat 双乘 |
| 御器篇 | ProjectileCount、Pierce、未来 ProjectileSpeed | 飞轮 | 实体数指数增长 |
| 开域法 | Range、Duration、控制稳定 | 听潮珠 | 领域覆盖过大 |
| 长息功 | Health、Armor、Regeneration/Shield | 震岳印 | 形成无风险常驻 |
| 采灵诀 | Duration、PickupRange、DOT/治疗联动 | 灵藤种 | 藤丛和状态高水位 |

每级 Modifier 必须说明对哪个 Stat、Operation、Value、Priority、StackingGroup 生效。缺失 Stat 不得
用近似 Stat 伪装；依赖 CR-10 的等级项在 G0 决策前标为阻塞。

## 3. 显化资格

标准条件：武器满级＋指定心诀达到批准等级＋本 Run 获得显化宝匣。宝匣是资格/选择上下文，不是
永久解锁，也不受 Luck 影响。

```text
MidBoss defeated
→ RewardDefinition(manifestation_chest)
→ compute eligible Evolution IDs from BuildState
→ 1—3 eligible choices / fallback reward
→ select Evolution
→ transform source Skill atomically
→ record offer history and presentation event
```

现有 Evolution 可表达技能转换，但现有升级候选只由 LevelUpRequest 触发；宝匣路径依赖 CR-04。

## 4. 六条显化

| 显化 | 行为变化 | 验收焦点 |
|---|---|---|
| 青岚流影剑 | 移动轨迹风痕；满风势分裂剑影 | 只用真实位移，风痕数量有上限 |
| 太一镇灵符阵 | 单发转符阵；同步引爆合法符印 | 标记原子消费、Boss 控制递减 |
| 赤炉百工轮 | 数量增加；回返触发有限连锁爆裂 | 链上限、VFX/实体预算 |
| 镜海潮生轮 | 涨退潮交替吸/推并切换爆发 | 相位确定、危险区可读 |
| 山河镇界印 | 短时安全领域与反震 | 不等于无敌；反震冷却 |
| 地脉生春枝 | 藤丛连接并向邻区扩张 | 传播代数/数量/生命周期上限 |

## 5. Synergy 与亲和

通用联动优先使用 HasTagCount、OwnsContent、SkillLevelAtLeast 和输出 AddModifier/AddEffectOp。
“角色亲和”和“地域权重”属于 Offer 权重层，不能修改候选资格，也不能保证特定内容出现。

建议为三条目标构筑至少定义可解释 Synergy：

- `qinglan.synergy.moving_sword_path`：移动/剑器标签，强化移动御剑；
- `qinglan.synergy.talisman_detonation`：符箓/标记标签，改善叠印窗口；
- `qinglan.synergy.living_garden`：草木/DOT/领域标签，控制传播效率。

若 Synergy 输出需要修改 CR-01/03 资源，必须使用通用 Output，不扩展成按 Synergy ID 分支。

## 6. 随机池

| 规则 | Demo 建议 |
|---|---|
| 基础候选 | 所有已解锁且合格的 Skill/Passive Offer |
| 角色亲和 | 乘风/剑器标签小幅权重提升 |
| 地域权重 | 青岚来源内容小幅提升，但 Demo 全池仍可见 |
| 流派倾向 | G2 可延期；若启用只加权 1—2 标签 |
| 新内容保护 | Demo 单角色阶段不需要 |
| 封存 | 可在据点解锁后启用，数量上限固定 |
| Reroll/Banish/Skip | 有限次数，来源可追踪 |

Offer 随机流必须独立于战斗、Encounter 和地图事件；历史保存调用前后计数和候选 ID。

## 7. 极端情况

- 武器满级但无对应心诀：宝匣提供其他合格显化或确定性替代奖励；
- 没有任何显化资格：绝不展示空选择，走 fallback；
- 武器槽满：已有武器升级仍可出现，新武器过滤；
- 心诀槽满：同理；
- Evolution 转换后标签和 Synergy 资格在同一事务重算；
- Synergy 现有规则是一次激活锁存，设计不得假设条件失效后自动撤销。

## 8. 测试与验收

| 类型 | 必须覆盖 |
|---|---|
| Validation | 6 心诀 5 级、6 Evolution 可达、引用/标签/互斥 |
| EditMode | 槽位、满级、宝匣合格/空池/fallback、转换原子性 |
| Determinism | 同 Seed 候选/宝匣选择一致，且不受战斗调用顺序影响 |
| Build Matrix | 目标三构筑在批准 Seed 集可形成；其他三线无死路 |
| UI PlayMode | 卡牌显示行为变化、标签、当前关系和不可选原因 |

退出条件：六条显化均通过真实资格链获得，候选真值只在 Simulation，三条目标构筑可复现但不被保证。
