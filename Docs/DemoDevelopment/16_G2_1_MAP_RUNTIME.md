# 16 G2.1 旧演武场地图运行时

## 1. 工作包目标

G2.1 交付 M08 的地图基础切片：五区有限地图、三座风脉台目标、三个动态事件、五个地标、稳定锚点、
纯模拟状态机与程序化 Placeholder Scene。Pack 从 0.5.0 / 94 项升级到 0.6.0 / 107 项。

本工作包不实现 Boss 实体和三目标参数修正、正式奖励消费、RunResult/Profile 合并、故事文本页面、HUD
提示或正式美术。它们依次由 G2.2、G2.3、G2.4/G2.5、G2.6 和 G3 关闭。

## 2. 五区地图与锚点

`qinglan.map.old_court` 使用 Finite Bounds `[-48,-36]—[48,36]`，运行提供者为
`qinglan.runtime.map.finite`，Scene Address 为 `maps/qinglan.demo/old_court`。九块矩形障碍形成可绕行
的断墙与横向隔断，不创建隐形墙。

| 区域 | 中心锚点 | 位置 |
|---|---|---:|
| 中央练剑场 | `qinglan.anchor.old_court.zone.central` | `(0,0)` |
| 西侧药圃 | `qinglan.anchor.old_court.zone.west` | `(-32,4)` |
| 东侧藏剑廊 | `qinglan.anchor.old_court.zone.east` | `(32,4)` |
| 北侧旧山门 | `qinglan.anchor.old_court.zone.north` | `(0,27)` |
| 南侧迎客庭 | `qinglan.anchor.old_court.zone.south` | `(0,-27)` |

地图共 13 个锚点：5 个区域中心、3 个目标、5 个地标。事件复用区域/地标锚点，不复制位置真值。
`MapAnchorBinding` 只把稳定 ID 绑定到 Scene Transform；Simulation 不引用 GameObject、Scene 或 Transform。

## 3. 目标运行时

三座风脉台使用已经冻结的稳定 ID：

| 目标 | ContentId | 激活锚点 |
|---|---|---|
| 听风台 | `qinglan.objective.wind_altar.listen` | `...objective.listen` |
| 引风台 | `qinglan.objective.wind_altar.guide` | `...objective.guide` |
| 止衡台 | `qinglan.objective.wind_altar.stop_balance` | `...objective.stop_balance` |

状态图为 Hidden→Revealed→Available→Activating→Defending→Completed，Activating/Defending 可中断回
Available。激活校验有效 `SpatialEntity`、有限坐标、距离和可行走锚点；中断清除进度，重试从零开始。
Defending 不设置移动锁，玩家仍由同一 30 Hz Pipeline 移动和战斗。

完成输出 `qinglan.reward.map.exploration_token` 的幂等事务；三座目标自身的 ContentId 是 G2.2
`BossPhaseRuntime` 接受的稳定规则输入，不能在本阶段硬编码 Boss 私有字段。`CompletedObjectiveMask` 按
Map 定义规范顺序投影三目标的 8 种组合。

## 4. 事件运行时

| 事件 | 触发窗 | 候选锚点 | 完成输出 |
|---|---:|---|---|
| 风脉暴动 | 390—450 s | 中央、北侧 | 解锁听风台 |
| 药圃复苏 | 120—600 s | 西区、药圃异种 | 解锁引风台 |
| 旧剑共鸣 | 180—660 s | 东区、藏剑封存匣 | 解锁止衡台 |

事件必须显式 Arm。没有活动事件时，系统在命中时间窗的候选中选择一个事件，再选择其锚点。两次抽取
只使用由 RunId 派生的 MapEvent RandomStream；World、Encounter、Offer 和 Reward 的随机调用次数不
影响结果。事件指向本地图目标时只推进目标到 Available，不伪造奖励输出。

## 5. 地标与输出事务

| 地标 | ContentId | Claim |
|---|---|---|
| 风脉旧碑 | `qinglan.landmark.wind_vein_stele` | 一次 |
| 藏剑封存匣 | `qinglan.landmark.sealed_sword_cache` | 一次 |
| 药圃异种 | `qinglan.landmark.herb_garden_variant` | 一次 |
| 断墙剑痕 | `qinglan.landmark.broken_wall_sword_mark` | 一次 |
| 迎客亭旧信 | `qinglan.landmark.guest_pavilion_letter` | 一次 |

玩家进入默认 2.5 m 半径后，地标从 Undiscovered 进入 Discovered；Claim 后进入 Claimed。重复 Claim
返回 AlreadyApplied，不增加计数或重复排队。Reward/Story 输出分别占用
`RunId + SourceStableId + Sequence`；G2.3/G2.5 消费时必须复用该事务键。

## 6. 容量、校验与错误语义

默认固定容量为 32 Objective、16 Event、32 Landmark、64 Output。初始化后 Tick 路径不创建临时托管
集合。输出容量不足时完成命令返回 CapacityExceeded 并保持原状态，清空输出后可重试。

内容验证除引用类型外，还阻止：

- 地图超过固定容量；
- 有限地图锚点越界或落入障碍；
- Objective/Event 引用不属于当前地图或不可行走的锚点；
- Landmark 引用不属于当前地图或不可行走的锚点；
- Scene Binding 缺失、重复或不能解析为稳定 ContentId。

## 7. Scene、Addressables 与本地化

`QinglanOldCourtPlaceholder.unity` 包含五个区域代理、九个障碍代理、13 个绑定和程序化 Camera/Light，
只使用 Unity Primitive。Scene、107 个定义和 Baked Catalog 均带 `pack.qinglan.demo`、`placeholder`、
`development-only` 标签。新增名称/描述同时写入 `en` 与 `zh-Hans`。

## 8. API 与兼容

- Game.Core：168 / `25766747...d7e176`，不变；
- Game.Content.Runtime：940 / `cd72d779...e35b00`，不变；
- Game.Simulation：1273 / `fd387bc6...1a92b8`，批准追加 81 条、删除 0；
- Game.Application：355 / `f57fe00c...f8a6`，不变；
- Game.Platform.Abstractions：73 / `8eb5f2cc...a51738`，不变。

旧 `MapObjectiveRuntime(int)`、`TryAdd`、`TryTransition` 和 `TryGetState` 保留；新增契约只追加命令、
快照和容量配置。详见 ADR 0019。

## 9. 实际冻结值与门禁

- Content Hash：`fbb58777702837b2730be64e515ef4b2386254089bb109e4c8c6e926ab2ca67c`；
- Pack Catalog SHA-256：`01195cf04c0f1668ebb7384594a77f0e6ca0485b088e00fca1eb74e4b647d86c`；
- Focused EditMode：6/6；全量 EditMode：245/245；全量 PlayMode：10/10；
- Project Validation 与 API Freeze：PASS；API diff 追加 81、删除 0；
- 性能短测：600/1,200/2,000/100，900 Tick＋300 预热；Tick p99 `4.6635 ms`，Render p99
  `0.6965 ms`，0 B，GC 0/0/0，Checksum `b455f50ce958d212`；
- 两次全 Pack Build 各 7 Pack，Qinglan Catalog 字节一致。

Windows Development Build 在 G2.1 为 `NOT RUN`：路线固定由 G2.8 对完整垂直切片重跑；G1.7 的最近
Development Build 保持有效但不能替代 G2.8。Boss 参数 8 组合 Golden、真实奖励、RunResult、UI 提示
和事件表现清理均为 `NOT RUN`，分别由 G2.2—G2.8 关闭。

退出条件：本文件的自动门禁全部 PASS，并明确保留后续模块的 `NOT RUN`，方可进入 G2.2。
