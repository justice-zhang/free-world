# M2 模拟内核契约

## 1. 时间与调用入口

表现层只向 `FixedTickRunner.Advance(renderDeltaSeconds)` 提交非负 Delta。
`SimulationClock` 固定为 30 Hz，不读取 Unity `Time`，也不依赖 `timeScale`。

- 每次 Advance 最多执行 `MaxCatchUpTicks`。
- 超出上限的积压保留，不静默丢弃。
- Pause 忽略暂停期间的新 Delta，保留暂停前的部分 Tick。
- Step 只在暂停状态可用，每次执行一个完整 Pipeline。
- `InterpolationAlpha` 只描述未消费时间比例并夹紧到 `[0, 1]`。

`SimulationWorld.RunTick` 不是静态全局入口；World 和 Runner 由未来 Composition Root
显式创建并持有。

## 2. Store 与 Handle 生命周期

M2 有四个独立 Store：

```text
ActorStore
ProjectileStore
AreaStore
PickupStore
```

每个 Store 保存自己的 Dense 状态数组、Dense → Slot、Slot → Dense、Generation 和
Free List。`EntityHandle(Index, Generation)` 只对创建它的 Store 有效；跨 Store
数据必须使用 `SpatialEntity(EntityKind, EntityHandle)`。

创建流程：

```text
Free List 有 Slot ──> 复用 Slot 和当前 Generation
Free List 为空 ─────> 扩展 Slot，初始 Generation = 1
                   └> 将 Slot 映射到 Dense 尾部
```

删除流程：

```text
验证 Index + Generation
  ├─ 失败：拒绝访问，InvalidHandleAccesses + 1
  └─ 成功：最后一个 Dense 元素覆盖删除空洞
           更新被移动元素的 Slot → Dense
           删除 Slot 的 Generation + 1（跳过 0）
           Slot 放回 Free List
```

`Contains` 是无副作用有效性查询。`TryRead`、`TryWrite` 和 `Remove` 收到失效 Handle
时返回 `false` 并记录诊断，绝不会降级为仅按 Index 访问。

系统遍历中禁止直接改变 Store 结构。Create/Remove 写入
`SimulationCommandBuffer`，由 Cleanup 按 FIFO 应用。Headless 测试装配可在 Tick
外通过 World 的显式 Create 方法建立初始夹具。

## 3. M2 系统执行顺序

```text
01 MovementSystem
02 LifetimeSystem
03 CleanupSystem
04 SnapshotBuildSystem
```

- Movement：积分四个 Store 的任意非零速度，更新朝向/Moving 标记并同步 Spatial Grid。
- Lifetime：递减有限生命周期，只写入删除命令。
- Cleanup：应用创建和删除，维护网格，并输出 Created/Removed 事件。
- SnapshotBuild：在结构稳定后生成本 Tick 快照。

同一次 `FixedTickRunner.Advance` 的全部追赶 Tick 事件累积在一个批次中，防止调用者
获得控制前丢失前序 Tick 事件。批次在下一次实际执行至少一个 Tick 的 Advance 或
Step 开始时清空；零 Tick 的 Advance 不清空尚未消费的最新批次。

## 4. 随机调用规则

`RandomStream` 是模拟层唯一的 M2 随机来源。

- 相同 Seed 和相同调用顺序必须产生相同序列。
- `Derive(streamId)` 只依赖 Root Seed 与 Stream ID。
- 父流已调用多少次不改变同 ID 派生流。
- 复制值类型 Stream 会复制当前状态；之后两个副本产生相同序列，直到调用顺序分叉。
- 拥有随机状态的系统应长期持有 Stream，跨方法调用使用 `ref`，不得每 Tick 用相同
  Seed 重建。
- 禁止 Simulation 使用 `UnityEngine.Random`。

## 5. Spatial Grid

网格以 `floor(position / cellSize)` 计算有符号 Cell 坐标，以
`EntityKind + EntityHandle` 唯一索引条目。支持：

- Insert
- Update（含跨 Cell 移动）
- Remove
- QueryRadius
- QueryNearby（排除源实体）

查询结果写入调用方持有并复用的 `SpatialQueryBuffer`。当前结果顺序不稳定，不能作为
随机选择或结算优先级；后续需要稳定优先级的系统必须用明确键排序或选择。

## 6. Render Snapshot 格式

每个 `RenderEntitySnapshot` 保存：

| 字段 | 含义 |
|---|---|
| Entity | EntityKind + Generation-safe Handle |
| PreviousPosition | Tick 系统执行前的位置 |
| CurrentPosition | Cleanup 后的位置 |
| PreviousFacingRadians | Tick 前朝向 |
| CurrentFacingRadians | Tick 后朝向 |
| PreviousStateFlags | Tick 前状态标记 |
| CurrentStateFlags | Tick 后状态标记 |

新建实体在 Tick 前不存在，因此 Previous 使用 Current，防止从原点飞入。本 Tick 删除
的实体不进入当前快照；未来 View 根据 Removed 事件释放。表现层可使用 Clock 的
InterpolationAlpha 插值，但 M2 不创建 View。

## 7. 诊断与 Headless Harness

`SimulationDiagnostics` 记录活动实体、累计创建/删除、失效 Handle 访问、最近 Tick
耗时、累计 Tick 耗时和计时 Tick 数。耗时不参与模拟决定。

`HeadlessSimulationHarness` 直接创建纯 C# World，按固定 Tick 移动测试 Actor，并输出
结构化 `HeadlessSimulationSummary` 或 invariant 文本摘要。它不加载 Scene、不创建
GameObject，也不访问表现资源。
