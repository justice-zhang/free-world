# ADR 0002：固定 Tick、数据导向的专用模拟内核

- 状态：Accepted
- 日期：2026-07-24
- 决策人：待填写
- M2 落地日期：2026-07-25

## 背景

类幸存者游戏需要同时更新大量敌人、投射物、区域、拾取物和状态。逐实体 `MonoBehaviour.Update`、逐对象物理组件和频繁实例化会导致调度、GC 和渲染开销，并使核心规则难以测试和复现。

## 决策

- 模拟默认运行在 30 Hz 固定 Tick；实际值由项目变量和配置锁定。
- 渲染独立运行，并使用前后模拟快照插值。
- 核心模拟使用专用 Store：`ActorStore`、`ProjectileStore`、`AreaStore`、`PickupStore` 等。
- 实体句柄使用索引与 Generation，避免复用后的悬空引用。
- 系统执行顺序在单一 Pipeline 中明确声明。
- 模拟层不依赖 GameObject、Scene、Prefab、Sprite、AudioClip 或 Steam。
- 先实现正确、可测试的单线程版本，再依据 Profiler 结果迁移明确热点到 Jobs/Burst。
- 不开发通用 ECS，不直接将全部游戏迁移到 Unity Entities。

### M2 已落地约束

- `SimulationClock` 固定为 30 Hz。表现 Delta 累积为 `double`，一次 `Advance`
  最多执行配置的追赶 Tick；未消费的积压保留到后续调用，不用丢 Tick
  隐藏负载问题。
- 暂停期间忽略新增表现 Delta，但保留暂停前不足一个 Tick 的累积量。
  单步只允许在暂停状态执行，并且每次恰好推进一个 Tick。
- `EntityHandle` 是 Store 局部的 `(Index, Generation)`。有效 Generation 从
  `1` 开始；删除时递增，溢出时跳过 `0`。跨 Store 持有实体引用时必须同时保存
  `EntityKind`，不得只比较裸 Handle。
- M2 只提供 `ActorStore`、`ProjectileStore`、`AreaStore` 和 `PickupStore`。
  每个 Store 拥有独立的 Dense 数据、Slot 映射、Free List 和 Generation；
  内部共享固定运动列的实现代码，但不存在组件注册、Archetype、动态查询或通用 ECS API。
- 系统遍历期间只允许修改非结构数据。创建和删除进入
  `SimulationCommandBuffer`，由 `CleanupSystem` 按 FIFO 顺序应用；本 Tick 事件保留在
  `SimulationEventBuffer` 中。同一次 `FixedTickRunner.Advance` 的所有追赶 Tick 事件
  累积为一个批次，在下一次实际执行 Tick 的 Advance 或 Step 开始时清空。
- M2 实际 Pipeline 固定为：

```text
01 MovementSystem
02 LifetimeSystem
03 CleanupSystem
04 SnapshotBuildSystem
```

- 统一 `SpatialGrid` 使用 `EntityKind + EntityHandle` 作为键，保存单份位置和 Cell
  链接；查询结果写入调用方复用的 `SpatialQueryBuffer`。移动系统同步已注册实体，
  Cleanup 在删除 Store 数据前移除网格条目。
- `RandomStream` 是纯值类型确定性流。派生流只依赖 Root Seed 和 Stream ID，
  不依赖父流当前调用次数；复制流会从完全相同的当前状态分叉。模拟随机调用必须持有
  流或通过 `ref` 传递，禁止临时重建和使用 `UnityEngine.Random`。
- Tick 开始前捕获仍存活实体的位置、朝向和状态标记；Cleanup 后构建当前状态。
  `RenderSnapshot` 的每个条目同时保存前后状态，新创建实体以前后相同状态出现，
  已删除实体不进入当前快照并通过删除事件释放未来 View。
- Tick 耗时使用 `Stopwatch` 记录为诊断数据，不进入模拟真值、随机序列或快照 Hash。

## 被拒绝的方案

- 每个敌人一个 MonoBehaviour 和 Collider。
- 一开始全面使用 Jobs/Burst。
- 自研通用 ECS。
- 依赖 Script Execution Order 控制系统顺序。

## 后果

优点：规则可复现、易测试、可批处理并可替换表现层。  
代价：需要维护实体 Store、快照、View Binding 和专用调试工具。M2 的 Store
Handle 不是跨 Store 全局 ID；调用方必须保留 Store 种类。单线程 Dictionary
空间网格和快照索引先保证正确性，只有后续基准确认热点后才允许更换后端。
