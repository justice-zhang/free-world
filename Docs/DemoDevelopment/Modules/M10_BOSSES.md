# M10 中段 Boss 与最终 Boss

## 1. 通用 BossPhaseRuntime（CR-07）

Boss 仍是 Actor/EnemyDefinition＋Skill；阶段 Runtime 只附加通用状态：当前 Phase、进入条件、技能组、
抗性、空间规则、目标修正和阶段一次性事件。UI 只读快照，不按血量自行推算。

```text
BossDefinition
├─ EnemyId
├─ PhaseDefinition[]
│  ├─ EnterCondition: HealthRatio/Elapsed/Objective/PreviousComplete
│  ├─ SkillSet[] + Schedule
│  ├─ ResistanceProfile
│  ├─ ArenaRuleOutputs[]
│  └─ PresentationProfileId
└─ RewardRuleId
```

阶段切换在固定 Tick 结算，当前 Tick 已排队的致命伤害、无敌窗口和转换顺序必须明确定义。建议阶段
阈值不会重置生命，仅短暂无敌并清理/转换旧阶段专属危险实体；不清理玩家合法状态。

## 2. 试剑傀·折枝

定位：6:00 构筑检查和显化教学，掉落固定显化宝匣。

| 阶段 | 生命区间 | 招式 | 空间变化 |
|---|---|---|---|
| 试剑 | 100%—65% | 横枝试剑、短冲 | 中央练剑场基础横扫 |
| 落木 | 65%—30% | 落木剑影、组合横扫 | 预设落点迫使移动 |
| 演武 | 30%—0 | 演武木桩、加速组合 | 木桩成为可利用/需绕行的临时障碍 |

横枝试剑有方向锁定和扇/线预警；落木剑影从已验证锚点/随机点生成；演武木桩使用结构命令和数量
上限，不创建逐桩 Update。击败后宝匣资格由 M06/M05 处理。

## 3. 守庭剑傀·听风

定位：三座风脉台目标的最终反馈和“守护失去对象”的灵结承载者。

| 阶段 | 生命区间 | 招式 | 规则 |
|---|---|---|---|
| 守门 | 100%—70% | 剑气、定向冲锋 | 学习基础预警和路线 |
| 听风 | 70%—35% | 遮蔽风场、假剑鸣、残剑 | 目标完成度开始明显影响规则 |
| 旧誓不散 | 35%—0 | 交叉风痕、强化残剑、组合冲锋 | 高压终局；不得全屏无解 |

Boss 允许减速、标记、破甲；定身转为短减速/硬直预算；击退使用高抗性。每个高危技能都有稳定
TelegraphId、伤害延迟、颜色/形状/音频和可访问性替代。

## 4. 三风脉台修正

| 修正 | Phase 2 | Phase 3 |
|---|---|---|
| 听风台完成 | 假剑鸣减少/真实声源明确；遮蔽透明度降低 | 取消最强假预警组合 |
| 引风台完成 | 残剑最大数量降低；安全通道更宽 | 残剑复生/重定位频率降低 |
| 止衡台完成 | 衡律技能间隔增加 | 交叉风痕波次间隔增加、旧誓强化倍率降低 |

修正必须通过稳定 Rule 输出作用于通用参数。所有 8 种组合都可胜利；三台全完成明显更清晰但不应
直接跳阶段或扣除固定比例生命。

## 5. 阶段和危险实体清理

每个技能/Area/Projectile 保存 BossId、PhaseId、CleanupPolicy。阶段切换时：

- `ExpireOnPhaseExit`：停止生产并由 Cleanup 移除；
- `FinishCurrentTelegraph`：已开始预警完成一次后清除；
- `Persist`：仅无害地形/表现可保留；
- 不允许旧阶段不可见碰撞体继续伤害。

Boss 死亡先发一次 EntityDied/Reward 请求，再统一清理 Boss-owned 实体，生成不可变胜利结果。

## 6. 叙事与首通

首次击败听风后，不播放“彻底毁灭”结果：Boss View 转为守风灵演出；Simulation 中战斗 Actor 已死亡，
叙事形态是新的表现/Hub 内容，不复用已失效 Handle。固定奖励：青岚脉印、指定唯一藏品、第三篇故事、
旧庭外庭进度和宁照微登场标记。

## 7. 测试与验收

| 类型 | 必须覆盖 |
|---|---|
| Validation | 阶段连续/可达、技能/Profile/Reward/Rule 引用 |
| EditMode | 阈值越过多阶段、同 Tick 致命伤、无敌窗、控制递减 |
| EditMode | 8 组风脉台参数快照；Boss-owned 实体清理 |
| Headless | 固定 Seed 阶段/技能/死亡 Checksum；Boss 只生成/奖励一次 |
| PlayMode | 所有预警与实际伤害对应；三目标影响可感知 |
| Accessibility | 低闪光、色觉设置仍可辨识高危技能 |
| Performance | 最坏阶段 Projectile/Area/VFX 上限和无持续分配 |

退出条件：两 Boss 有规则变化而非纯血量膨胀，听风三阶段与风脉台联动真实、确定、可读且可清理。
