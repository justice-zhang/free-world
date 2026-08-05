# ADR 0019：G2.1 旧演武场地图运行时与稳定锚点

- 状态：Accepted
- 日期：2026-08-06
- 决策人：依据用户当前连续 Demo 开发与自行决策授权
- 关联里程碑：G2.1、M08
- 关联 CR：CR-2026-009

## 背景

Schema 6 已定义 MapObjective、MapEvent 与 Landmark，但 G1.1 的 `MapObjectiveRuntime` 只保留一个
兼容性的 ID→状态容器，不能执行 M08 要求的距离激活、防守中断、事件时间窗、独立随机流、地标发现
或幂等奖励输出。旧演武场也尚无可交付的五区 Placeholder Scene 与运行时锚点绑定。

## 决策

### 纯模拟地图所有者

`MapObjectiveRuntime` 在初始化时从 `ContentRegistry` 解析一张 Map 的目标、事件、地标与稳定锚点，
之后只使用预分配数组。默认上限为 32 个目标、16 个事件、32 个地标、64 个待消费输出；固定 Tick
路径不创建临时托管集合。

目标严格执行 Hidden→Revealed→Available→Activating→Defending→Completed，并支持 Activating 或
Defending 回到 Available。激活命令校验 `SpatialEntity`、有限坐标、最大距离与可行走锚点；中断清除
进度和激活者，重试从零开始。完成只排队一次带 `RunId + SourceStableId + Sequence` 的
`RewardTransactionId`，实际奖励消费仍由 G2.3 接入。

事件由 `RunId` 派生专用 MapEvent 随机流，在已 Arm、时间窗命中且无其他活动事件时，从候选事件和
候选锚点各抽取一次。它不读取 World/Offer/Reward 随机流。事件输出若指向本地图目标，只将目标推进
到 Available；其他合法输出进入同一固定输出缓冲。

地标根据玩家位置与发现半径从 Undiscovered 进入 Discovered。非重复地标 Claim 后永久保持 Claimed，
重复请求返回 AlreadyApplied；Reward 与 Story 分别占用序列并输出稳定事务。

### 内容和 Scene 边界

Pack 0.6.0 新增一张有限旧演武场地图、3 个目标、3 个事件、5 个地标和 1 个地图占位奖励，总计
107 definitions。地图作者数据声明 13 个稳定锚点和障碍；ContentValidator 阻止容量超限、越界、落入
障碍或不属于该地图的目标/事件/地标锚点。

`QinglanOldCourtPlaceholder.unity` 只提供五区、障碍和 `MapAnchorBinding`。Binding 属于 Presentation，
模拟层不读取 GameObject、Scene 或 Transform。Scene 与全部新增内容继续带 `placeholder`、
`development-only` 和 Pack 标签。

## 兼容与影响

- 不改变 Assembly 引用方向、30 Hz Tick、Content Schema 6、Save Schema 3 或稳定 ID 规则。
- `Game.Simulation` 公共 API 只追加 81 条规范签名，零删除；其他冻结程序集无变化。
- 保留旧 `MapObjectiveRuntime(int)`、`TryAdd`、`TryTransition`、`TryGetState`，既有调用者无需迁移。
- 新调用者应在 Run 组合根创建 Runtime，加载 Registry 后调用 `Initialize`，并只通过命令和快照访问。
- 输出缓冲满时完成操作保持原状态并返回 CapacityExceeded，消费者清空后可安全重试。
- MapEvent 随机调用次数是诊断数据，不进入存档；Run 重放从同一 RunId 和相同命令序列恢复。

## 被拒绝的方案

- 在 Scene MonoBehaviour 中保存目标真值：会破坏无头重放、测试和 Scene 解耦。
- 复用 World.Random：地图事件会被战斗、掉落或 Offer 调用顺序污染。
- 目标完成时直接写 Profile/货币：越过 Application 事务边界并提前实现 G2.3。
- 为每个目标和地标创建逐对象 Update：违反集中固定 Tick 与高频路径约束。

## 迁移、回滚与测试

迁移步骤：旧调用者可保持不变；Demo 组合根在内容 Registry 加载后初始化地图 Runtime，Presentation
根据 Scene Binding 显示锚点。G2.3 消费 `MapOutputRequest` 前必须保持事务幂等。

回滚方案：移除 Pack 0.6 新内容和 Scene Address，组合根不调用 Initialize 时新 System 为无操作；旧
兼容 API 仍可继续工作。不得回滚已持久化稳定 ID 为运行时索引。

测试覆盖双次 Bake/Checked-in Catalog、13 个 Scene Binding、锚点可行走校验、三类目标的距离/中断/
恢复/一次输出、事件 RNG 隔离、地标幂等、8 种目标完成子集、Defending 时移动、完整 EditMode/
PlayMode、Project Validation、API diff 与性能短测。
