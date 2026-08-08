# ADR 0022：G2.4 不可变 RunResult 与 Demo 流程所有权

- 状态：Accepted
- 日期：2026-08-08
- 决策人：依据用户当前连续 Demo 开发与自行决策授权
- 关联里程碑：G2.4、M01、M10
- 关联 CR：CR-2026-008、CR-2026-015

## 背景

M6 的 `RunSession` 只能冻结 Tick、等级和少量统计，不能证明一局使用了哪些 Pack/角色/地图，也不能
携带 G2.1—G2.3 的目标、Boss、Relic、Evolution 和永久奖励增量。既有 M7 流程会在 `RunCompleted`
发布后立即进入 M8 保存；若直接复用，会让 G2.4 越过 G2.5 的 Profile 原子事务边界。

同时，Demo 需要 Title→角色→地图→Run→Result→Hub→再次出发的唯一生命周期 Owner，并在离开 Result
时回收 Actor、Projectile、Area 和 Pickup。页面和加载阶段不应为了 Demo 扩大原有 `GameState` 枚举。

## 决策

### 运行身份与结果冻结

Run 开始前创建不可变 `RunDescriptor`，保存 RunId、Seed、角色/地图/难度稳定 ID、Boss 胜利条件以及按
依赖顺序复制的 Pack ID、版本和 Content Hash。`GameApplication` 只在成功加载 Catalog 后生成 Pack
快照；运行期间不再读取可变 Authoring 或 Unity Object。

`RunResult` 在 `RunSession.End` 的单一冻结点复制：

- `Victory`、`Defeat`、`Abandoned`、`RecoveryRejected` 四种稳定 Outcome；
- Tick、时长、等级、敌人/精英/Boss 击杀和既有升级统计；
- Skill、Passive、Relic、Evolution 的稳定 ID/等级；
- 已完成目标/事件、已发现/领取地标；
- G2.3 Currency/Unlock/Unique/Story 合并后的 `RunResultDelta`；
- Spawn、Objective、Boss 三类只读 Checksum 和 Pack 快照。

所有数组在构造时复制并以只读视图暴露。结果事务 ID 固定为 `run.result.<RunId 十六进制>`；货币用
`long` 聚合，不把运行时索引、EntityHandle、Scene 或 Unity Object 写入结果。

### 胜利与异常恢复

正常胜利必须同时满足 Encounter 声明的 Boss 击杀数，以及最终 Boss 的
`RunId + VictoryBossId + 0` 奖励事务已提交。这样不会在最终奖励、选择或死亡清理完成前提前进入结果。
玩家死亡和主动退出分别冻结 Defeat/Abandoned。

CR-2026-015 继续延期；损坏或不完整 Recovery 只生成空增量 `RecoveryRejected` 结果，不能装配 World、
不能显示 Continue，也不能计作胜利。活动 `RunSession.End` 不接受 RecoveryRejected。

### 流程与资源所有权

新增低频 `DemoRunCoordinator` 和独立 `DemoFlowStage`。它拥有一个 `IRunSessionHandle`，允许的主路径为：

```text
Title → CharacterSelect → MapSelect → Preparing → Active
Active/Paused/Choice → Ending → Result → Hub → CharacterSelect
```

`Preparing` 和 `Ending` 各至少跨一个 Coordinator Tick，避免同一调用既装配又显示结果。非法转换返回
false；内容装配失败进入带本地化 Key 的 ContentError。离开 Result 到 Hub 或 Dispose 时，Handle
幂等回收全部运行 Entity；Scene、Addressables 和 View 的实际 Owner 留给 G2.6/G2.8 组合根接入。

G2.4 只标记 `HasUncommittedResult`，不写 Profile、不清 Recovery、不发布 `RunCompleted`、不调用平台。
G2.5 必须在允许“已保存”或平台事件前消费同一不可变结果并完成原子事务。

## 兼容与影响

- 不改变程序集引用方向、30 Hz Tick、Content Schema 6、Save Schema 3 或稳定 ID 规则。
- 保留旧 `RunSession(world, player, stateMachine, clock)`、`RunEndReason` 三个原值和 `RunResult` 原公开属性。
- 不扩大旧 `GameState`；Demo 页面阶段使用独立枚举。
- `Game.Simulation` 公开 API 追加 6 条，`Game.Application` 追加 95 条，均删除 0；Core、Content Runtime、
  Platform Abstractions 逐字节不变。
- 结果组装只在 Run 结束低频执行；Death 热路径只增加两个布尔计数，不使用 LINQ、反射、格式化或临时集合。

## 被拒绝的方案

- 在 UI Controller 读取 Simulation Store 现场拼结果：会让结果随清理顺序变化并泄漏运行时句柄。
- G2.4 直接发布旧 `RunCompleted`：会触发 M8 保存，绕过 G2.5 的 Profile 3 幂等合并。
- 把 Title/Hub 全部加入旧 `GameState`：扩大既有冻结 API，且重复页面状态和 Simulation 暂停状态。
- 只用 Boss 计数判胜：可能丢失最终首通奖励事务。
- 结果持有 `BuildState`/`MapObjectiveRuntime` 引用：释放 World 后结果会失效或发生别名修改。

## 迁移、回滚与测试

迁移：新 Demo 组合根先从已加载 Catalog 创建 Descriptor，再由 `QinglanDemoRunFactory` 装配运行；旧 M6/M7
调用可继续使用兼容构造。G2.5 从 `HasUncommittedResult/LatestResult` 接入事务，不重算 Simulation 数据。

回滚：可停止使用 Demo Coordinator 并回到旧 M7 流；新增公开 API 必须保留，不能删除以恢复旧 Hash。
已冻结结果不得因重试重新读取 World；未提交结果只能保留在可重试状态或明确丢弃，不能部分发放。

测试覆盖 Descriptor/Pack 快照、四种 Outcome、最终 Boss 双条件、Build/Map/Reward/统计聚合、输入数组
别名隔离、非法转换、真实青岚装配、幂等资源释放，以及 Title→Result→Hub→再次出发 PlayMode。
