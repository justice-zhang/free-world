# 10 G1.2 陆青野、乘风与战斗状态实施切片

- 状态：`COMPLETE`
- 工作包：G1.2
- Owner：M02、M03
- 内容路径：`Assets/GameAssets/Placeholder/QinglanDemo`
- Pack：`qinglan.pack.demo` 0.1.0 / Content Schema 6

## 1. 范围边界

本包交付陆青野基础定义、乘风机制与三档通用输出、七个战斗状态、状态免伤消费者和实际 Catalog。
G1.3 才交付游风剑及隐藏技能并回填 Character Starting Skill；G1.5/G2.2 才交付敌人/Boss 与控制递减
内容；G2.6/M12 才交付输入/HUD/音效 PlayMode。G1.2 不导入正式美术、音频或字体。

## 2. 角色与乘风数值

| 项目 | G1.2 值 |
|---|---:|
| 最大生命 | 120 |
| 移动速度 | 6 units/s |
| 距离资源倍率 | 1 / resolved unit |
| 微风阈值 | 6 |
| 疾风阈值 | 16 |
| 乘风阈值 | 30 |
| 受伤基础损失 | 8 |
| 自然衰减 | 0 |

资源只消费 `MovementSource.PlayerCommand` 经 `IMapRuntime.ResolveMovement` 后的有限实际距离。传送、纠错、
击退、拉拽、脚本位移、硬边界零位移和暂停不积累。Shield 或 Health 实际减少时，同 Tick＋Target 最多
响应一次并严格降一档；0、免疫、通道冷却和仅屏障吸收不降档。

## 3. 档位输出

| 档 | 输出 ID | 标签 | G1.3 消费语义 |
|---|---|---|---|
| 微风 | `qinglan.trait.lu_qingye.riding_wind.breeze` | `mechanic.output.affinity_only` | 亲和 Delivery 速度 +10% |
| 疾风 | `qinglan.trait.lu_qingye.riding_wind.swift` | `mechanic.output.innate_only` | 移速 +5%；本命冷却 ×0.92 |
| 乘风 | `qinglan.trait.lu_qingye.riding_wind` | `mechanic.output.return_secondary` | 本命回返完成后触发弱风刃 |

Trait 只作为不可变通用输出数据，不能被当作普通升级候选；技能绑定必须验证标签，禁止比较陆青野 ID。

## 4. 七状态参数

| ID | 策略 | 时长/间隔 | 上限 | 行为 |
|---|---|---|---:|---|
| `qinglan.status.burning` | AddStacks | 4s / 1s | 5 | Fire 2.5/层，非暴击 |
| `qinglan.status.poisoned` | IndependentInstances | 6s / 1.5s | 4 | Poison 1.75/实例，非暴击 |
| `qinglan.status.slowed` | ReplaceIfStronger | 2.5s | 1 | MoveSpeed ×0.70 |
| `qinglan.status.rooted` | RefreshDuration | 1s | 1 | MoveSpeed Override 0；Boss 转换在 G2.2 |
| `qinglan.status.armor_broken` | ReplaceIfStronger | 5s | 1 | Armor ×0.70，Stat 下限 0 |
| `qinglan.status.marked` | AddStacks | 6s | 6 | 状态查询/消费/引爆操作数 |
| `qinglan.status.damage_immunity` | RefreshDuration | 1.5s | 1 | `base.damage_policy.immune.all` |

伤害免疫只通过活动状态标签进入集中 DamageResolution；不直接写 Health，也不判断 Qinglan StatusId。
接触伤害内容在 G1.5 使用 `contact` 通道和 18 Tick 冷却；Boss 高危使用独立 `boss_hazard`。

## 5. 事件、容量与生命周期

- 档位变化使用固定容量 pending/batch 缓冲，只在跨档或受伤降档时写一条纯值事件；
- 高频累积不分配，非有限累积结果拒绝并计数；
- Cleanup 删除 Actor 时同步解绑机制实例，代际复用不会继承旧资源；
- Character/Mechanic 绑定在 Run 构造期先解析所有稳定 ID，再原子附加 load-local index。

## 6. 自动化验收

| 证据 | 要求 |
|---|---|
| Catalog | 12 定义、Schema 6、跨引用/Hash/序列化 PASS |
| EditMode | 阈值、跨档、暂停、传送、贴墙、同 Tick 多伤害、0/免疫/屏障、状态堆叠 |
| Golden Seed | `0x4731325249444555` → `0xFD82A621E9E5AD8E` |
| Performance | 实际乘风定义 54,000 Tick 热路径 0 B |
| API | 五个冻结程序集 Hash 不变 |
| Validation | 全项目 Authoring/Baked/Addressables/治理 PASS |

PlayMode 的 WASD/摇杆、HUD/音效一致性明确属于 G2.6，因此在 G1.2 报告中必须记为 `NOT RUN`，不能
用 EditMode 代替。
