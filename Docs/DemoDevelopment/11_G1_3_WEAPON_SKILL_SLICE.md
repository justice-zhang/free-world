# 11 G1.3 六武器与技能运行时实施切片

- 状态：`COMPLETE`
- 工作包：G1.3
- Owner：M04
- 内容路径：`Assets/GameAssets/Placeholder/QinglanDemo`
- Pack：`qinglan.pack.demo` 0.2.0 / Content Schema 6

## 1. 范围边界

本包交付六把 8 级主武器、十个隐藏辅助技能、陆青野 Starting Skill 回填、固定 Seed Preview Golden、
ProcDepth 截断和生命周期清理。G1.4 才把六心诀、六显化、Offer 和现有隐藏技能组合成完整构筑；本包
不创建敌人、Boss、Encounter、正式美术、音效或字体。

## 2. 内容图

| 主武器 | 触发/Delivery | 直接引用 | 等级主轴 |
|---|---|---|---|
| 游风剑 | Timer / OutboundReturn | 回返完成、三阶乘风输出、乘风刃 | 伤害、冷却、命中额度、速度、回程距离 |
| 镇邪黄符 | Timer / Projectile | `marked`、符印引爆 | 伤害、冷却、投射数量、速度、目标数 |
| 离火飞轮 | Timer / OutboundReturn | 显化预留回爆 | 火伤、速度、数量、回程、冷却 |
| 听潮珠 | Timer / Instant | 涨潮、退潮交替 | 周期冷却；辅助等级随主技能传播 |
| 震岳印 | Timer / Area | 显化预留护域、反震 | 范围伤害、半径、冷却、击退、搜索范围 |
| 灵藤种 | OnKill / Area | 中毒；显化预留生长、传播 | DOT、时长、半径、冷却、Tick 间隔 |

十个隐藏技能全部是可执行 Skill，但带 `skill.hidden` 标签且不进入候选池。飞轮回爆、震岳护域/反震、
藤丛生长/传播在本包只冻结为可组合输出，由 G1.4 Evolution 装配，避免基础武器提前获得显化行为。

## 3. 通用运行时约束

- `SpawnSecondarySkill` 保留原命中目标，状态阈值和引爆因此作用于实际被命中的 Actor。
- 一个 Secondary Effect 可声明两个稳定 Skill 引用；`Int0=1` 时按实例 `ActivationSequence` 确定性交替。
- 主技能创建时预注册完整隐藏依赖闭包；升级沿闭包传播并按各辅助技能最高等级钳制，递归深度上限 16。
- `OutboundReturn` 可声明回收完成辅助技能和可选机制输出 Gate；Gate 只读取当前通用机制输出。
- 出程和回程分别维护命中集合；回程额度归零后继续回 Owner，不再造成额外命中，回收后统一清理。
- Secondary 在 `MaximumProcDepth` 处记录截断，不生成下一层事件；Actor 清理后实例与 Sidecar 最终归零。
- Preview 对 Timer 使用自然 Tick，对 OnKill/OnHit/OnDamageTaken/OnPickup/OnStatusApplied 使用匹配声明的
  合成上下文；固定 Tick 段不得产生托管分配。

## 4. 固定 Seed Preview Golden

参数：Seed `0x473133574541504F`、5 秒、16 个静止目标、Damage 倍率 1、暴击率 0。

| 武器 | L1 DPS/Hit/Trigger | L4 DPS/Hit/Trigger | L8 DPS/Hit/Trigger |
|---|---:|---:|---:|
| 游风剑 | 19.1999989 / 6 / 3 | 27.9999981 / 7 / 4 | 47.5999947 / 7 / 4 |
| 镇邪黄符 | 10.999999 / 6 / 6 | 14.7999983 / 6 / 6 | 59.9999962 / 16 / 10 |
| 离火飞轮 | 11.999999 / 5 / 3 | 29.9999981 / 10 / 3 | 43.9999962 / 10 / 3 |
| 听潮珠 | 31.9999962 / 16 / 6 | 46.3999939 / 16 / 6 | 81.99999 / 20 / 8 |
| 震岳印 | 43.1999969 / 12 / 2 | 57.1999931 / 13 / 2 | 89.59999 / 16 / 2 |
| 灵藤种 | 165.999985 / 248 / 7 | 251.499969 / 310 / 7 | 491.399963 / 432 / 8 |

上述数据只用于相同输入回归，不是 G3 正式平衡或目标硬件性能结论。18 个 Preview 的固定 Tick 段均为
0 B；最终平衡仍由 G3.4 固定。

## 5. 自动化验收

| 证据 | 要求 |
|---|---|
| Catalog | 28 定义、Pack 0.2.0、Schema 6、Content Hash 固定 |
| Validation | 等级连续、模块/引用类型、Presentation、本地化、Baked 往返 |
| EditMode | 回返 Gate/去重、标记原子消费、潮汐交替、灵藤生命周期、ProcDepth、Owner Cleanup |
| Preview | 六武器 1/4/8 级固定 Seed 精确 Golden、两次运行一致、0 B |
| API | 五个冻结程序集签名数和 Hash 不变 |
| Regression | 全量 EditMode、全量 PlayMode、900 Tick 性能短测 |

Build 按路线留到 G1.7 完整 Placeholder Pack；G1.3 不以旧 Player 代替新构建证据。
