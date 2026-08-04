# M02 陆青野与“乘风”角色机制

## 1. 角色定义

| 字段 | 设计 |
|---|---|
| CharacterId | `qinglan.character.lu_qingye` |
| Starting Skill | `qinglan.skill.weapon.yufeng_sword` |
| Trait | `qinglan.trait.lu_qingye.riding_wind` |
| Visual/Audio Profile | `qinglan.presentation.character.lu_qingye` / `qinglan.audio.character.lu_qingye` |
| 定位 | 真实移动驱动的均衡御剑角色 |
| 设计约束 | 贴墙、暂停、升级、剧情不积累；受伤降一档 |

基础数值在 G1 平衡表中确定。本文件不提前锁死生命、速度和伤害数值；初始值须与普通敌人接触伤害、
第一分钟升级节奏和目标硬件输入手感联合校准。

## 2. 乘风状态机

```text
StaticWind (tier 0)
  --distance >= T1--> Breeze (tier 1)
  --distance >= T2--> SwiftWind (tier 2)
  --distance >= T3--> RidingWind (tier 3)

Any tier --resolved health/shield damage--> max(tier - 1, 0)
RunPaused/Upgrade/Story/Result --freeze--> same tier and progress
```

积累输入是 `IMapRuntime.ResolveMovement` 后玩家当前与上一 Tick 的有效位置差。以下位移不计入：

- 期望移动被硬边界/障碍完全阻挡；
- Presentation 插值、Camera、Root Motion；
- 传送/纠错/恢复加载；
- 暂停时 Transform 变化；
- 小于数值噪声阈值的漂移。

阈值、是否随时间缓慢衰减、受伤后当前档内进度保留比例属于 G1 数值参数；默认建议不自然衰减，
突出路线与持续移动。

## 3. 档位输出

| 档位 | 通用输出 |
|---|---|
| 静风 | 无 |
| 微风 | 只对带 `mechanic.riding_wind_affinity` 的 Delivery 提高速度 |
| 疾风 | 本命器攻击间隔缩短；玩家移动速度小幅提高 |
| 乘风 | 本命器完成回返后触发弱风刃 Secondary Skill |

不能把输出写成“如果 CharacterId == 陆青野”。角色机制应通过已绑定 Trait/Mechanic 实例、标签和
通用输出操作工作。非本命武器不自动获得全部加成；具体亲和由内容标签验证。

## 4. 数据建议（CR-01 评审输入）

```text
CharacterMechanicDefinition
├─ ResourceId: qinglan.resource.riding_wind
├─ GainSource: resolved_distance
├─ FreezeStates[]
├─ LossSource: resolved_damage
├─ Tiers[]: threshold + outputs[]
└─ PresentationProfileId
```

高频实例只保存 `currentValue`、`tier`、`previousResolvedPosition` 和必要冷却；阈值/输出从不可变编译
定义读取。档位变化才发事件，不能每 Tick 发字符串或 UI 更新。

## 5. 输入与表现

- 键盘与摇杆均归一到同一 Move Command；摇杆幅度影响速度但不改变积累公式；
- HUD 显示四段风势环和当前档位，不显示内部浮点；
- 受伤降档使用明确的收束音和一段颜色变化，不遮盖 Boss 危险预警；
- 乘风档风刃只由 Simulation 创建，View 只消费 PresentationId；
- 设置中可降低闪光与屏幕震动，不能关闭关键档位音/形提示的全部通道。

## 6. 与其他模块协作

| 模块 | 契约 |
|---|---|
| M03 | 只响应已结算的实际伤害事件；0 伤害/免疫不降档 |
| M04 | 读取通用机制输出或标签，不读取 CharacterId |
| M06 | 乘风羽、风脉铜片通过通用资源/输出修改 |
| M08 | 地图硬边界和障碍后的实际位移是积累源 |
| M12 | HUD 读取 tier/progress 的 UI-safe 快照 |
| M13 | 档位变化、风痕和风刃表现池化 |
| M14 | RunRecovery 若支持必须保存稳定 ResourceId 与纯值 |

## 7. 边界情况

- 同 Tick 多段伤害：按 DamageApplied 批次只降一档，避免多弹瞬间归零；具体去重键为 Tick＋Target；
- 护盾全吸收但发生实际 Shield 损失：视为受伤，降一档；
- 伤害被完全免疫/0：不降档；
- 击退造成的被动位移：默认不积累，只有玩家 Move Command 贡献的 resolved displacement 计入；
- 地图传送：显式 `MovementSource.Teleport` 排除；
- 浮点异常：拒绝非有限距离，计入诊断，不污染资源。

## 8. 测试与验收

| 类型 | 必须覆盖 |
|---|---|
| EditMode | 四档阈值、跨多阈值、贴墙、暂停、传送、击退、非有限输入 |
| EditMode | 同 Tick 多伤害只降一档；0 伤害不降；固定 Seed 校验值 |
| Integration | 微风/疾风/乘风只作用于合法标签与本命器 |
| PlayMode | WASD/摇杆都能积累；HUD/音效与 Simulation 档位一致 |
| Performance | 1 玩家机制 54,000 Tick 热路径 0 B 分配 |

退出条件：角色无需专用 MonoBehaviour Update，核心程序集没有陆青野 ID 分支，乘风全部行为与文案一致。
