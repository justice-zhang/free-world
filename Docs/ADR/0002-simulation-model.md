# ADR 0002：固定 Tick、数据导向的专用模拟内核

- 状态：Accepted
- 日期：2026-07-24
- 决策人：待填写

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

## 被拒绝的方案

- 每个敌人一个 MonoBehaviour 和 Collider。
- 一开始全面使用 Jobs/Burst。
- 自研通用 ECS。
- 依赖 Script Execution Order 控制系统顺序。

## 后果

优点：规则可复现、易测试、可批处理并可替换表现层。  
代价：需要维护实体 Store、快照、View Binding 和专用调试工具。
