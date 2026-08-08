# ADR 0020：G2.2 内容驱动 Boss 阶段运行时

- 状态：Accepted
- 日期：2026-08-08
- 决策人：依据用户当前连续 Demo 开发与自行决策授权
- 关联里程碑：G2.2、M10
- 关联 CR：CR-2026-014

## 背景

Schema 6 已提供 `BossDefinition`、血量阈值、阶段接受规则和清理策略，但 G1.1 的
`BossPhaseRuntime` 只有占位骨架，不能把阶段技能装配到真实 Enemy Actor，也不能处理跨多阈值、同 Tick
致命伤、三风脉台参数、Boss-owned 危险实体清理或一次性死亡奖励事务。G1.6 的 Encounter 也只预留了
Boss 窗口，没有生成可执行 Boss。

## 决策

### 通用阶段所有者

`BossPhaseRuntime` 以固定容量数组保存 Boss Actor、编译后的 `RuntimeBossDefinition`、阶段、三条稳定
Map Objective 规则、阶段技能句柄和 Boss-owned Effect。运行时不分支具体青岚 ID，也不引用
UnityEngine、Scene、Prefab 或表现对象。

Boss Spawn 时一次性预加载定义中所有唯一阶段技能，阶段外技能标记为 Suppressed；固定 Tick 的
`BossPhaseSystem` 在技能触发前读取 Actor Health，按定义阈值切换启用集合。一次伤害跨多个阈值时直接
进入最终可达阶段；致命状态优先进入阶段终点并只允许一次死亡结算。加载/装配失败会回收本次已创建
的全部技能实例。

### 三风脉台规则

Boss 定义中出现的前三个 `MapObjectiveDefinition` 按稳定声明顺序绑定到三位 Mask。听风的顺序固定为
引风、听风、止衡；只在当前阶段接受该规则时生效：

| 规则 | 通用输出 |
|---|---|
| 引风完成 | SpatialLoadMultiplier = 0.70 |
| 听风完成 | DeceptionMultiplier = 0.65 |
| 止衡完成 | CadenceIntervalMultiplier = 1.25 |
| 三台完成 | BonusOutputEligible = true |

规则只改变参数，不跳过血量阶段或直接扣血；8 种组合均保持有限且不低于安全下限。

### 清理、控制与奖励

阶段离开时，`ExpireOnPhaseExit` 立即禁伤并回收旧阶段 Delivery；`FinishCurrentTelegraph` 只允许已开始
预警完成表现，伤害立即关闭；`Persist` 只保留声明为无害的效果。Boss 死亡无条件禁用所有阶段技能，
将其 Delivery 从伤害表解绑并进入集中 Cleanup；旧阶段不可见碰撞体不能继续伤害。

带 `status.control` 标签的状态时长乘 BossDefinition 的 ResistanceMultiplier，最短保留 0.1 秒；其他
状态不改。死亡奖励使用 `RunId + BossDefinitionId + 0` 生成稳定 `RewardTransactionId`，同一 Boss
Actor 只提交一次。实际 RewardDefinition 和消费者由 G2.3 接入，G2.2 不越过该边界。

### Encounter 时钟

Encounter Scheduler 将累计 `float` 秒改为累计整数 Tick，`ElapsedSeconds` 只由 Tick×TickDuration
投影。这样 30 Hz、21,600 Tick 的 719.9 秒最终 Boss 规则不会因浮点累计漂移漏触发，同时保持既有
公开属性和调度语义。

## 兼容与影响

- 不改变程序集引用方向、30 Hz Tick、Content Schema 6、Save Schema 3 或稳定 ID 规则。
- `Game.Simulation` 公开 API 追加 58 条规范签名、删除 0；其他四个冻结程序集逐行不变。
- 保留隐式兼容所需的公开无参 `BossPhaseRuntime()`，并追加可配置容量构造函数。
- 旧 Encounter 调用者继续读取 `ElapsedSeconds`；内部精度提高，不改变公开类型。
- 新公开快照只包含稳定 ContentId、值类型状态和事务，不暴露 Registry Index 或 Unity Object。

## 被拒绝的方案

- 在 Boss MonoBehaviour 中自行按血量切阶段：破坏 Headless、确定性和 Simulation 真值。
- 为折枝/听风编写具体 ID 分支：违反内容扩展不修改核心程序集的约束。
- 切阶段时只隐藏表现、不解绑 Delivery：可能产生不可见伤害。
- 每次切阶段重新创建技能集合：增加阶段尖峰分配与生命周期风险。
- 继续累计 float 时间：12 分钟边界已经实际复现最终 Boss 漏生成。

## 迁移、回滚与测试

迁移：Enemy Spawn 从编译 Catalog 取得可选 BossDefinition，先创建 Actor 与基础攻击技能，再让
Boss Runtime 完成阶段技能装配；Map Runtime 把三个 Objective 完成状态投影为稳定规则命令。

回滚：移除 0.7.0 Boss 内容和 Encounter BossRule 后运行时恢复无 Boss 空操作；新增 API 保留以维持
二进制/源码兼容，不回退为具体 ID 或 Scene 逻辑。

测试覆盖两 Boss 三阶段内容、8 组合 Golden、跨多阈值与致命优先、三种清理策略、控制时长、阶段技能
预加载/启停、54,000 次阶段解析 0 B，以及双实例 21,600 Tick 两 Boss 各一次生成和确定性校验。
