# 性能预算与优化规则

## 1. 初始目标

这些是框架工程目标，最终最低配置需在正式美术和目标硬件确认后重新锁定。

| **指标**                 | **初始目标** |
|--------------------------|--------------|
| 表现帧率                 | 60 FPS       |
| 模拟频率                 | 30 Hz        |
| 稳态托管分配             | 0 B/frame    |
| 高频 Instantiate/Destroy | 0            |
| Soak Test                | 30 分钟      |
| 活动敌人                 | 1,500        |
| 活动投射物               | 3,000        |
| 地面拾取物               | 5,000        |
| 短生命周期 VFX 请求      | 200 同时     |

## 2. 先测量，后优化

M2-M6 先使用可验证的单线程实现。只有 Profiler 或基准测试证明为热点后，才迁移：

- 敌人移动

- 空间网格构建

- 投射物更新

- 范围查询

- 批量状态 Tick

- 快照生成

M7 的四类实体 View、短生命周期 VFX、AudioSource 和共享 Canvas 伤害数字均使用持久池；
每帧只由 `M7RuntimeHost`、`PresentationCoordinator` 和 Camera Rig 集中推进，不添加逐实体
`Update`。这证明生命周期结构符合预算，不等同于目标规模性能通过；池命中率、扩容次数、
GC 和 1,500/3,000/5,000 实体结果仍必须由 M10 性能 JSON 给出。

## 3. 禁止行为

- 每个敌人独立 Update/FixedUpdate

- 每个技能全量扫描敌人

- 高频路径 LINQ 或反射

- 每帧创建集合、字符串或闭包

- 每个伤害数字独立 Canvas

- 每个敌人独立材质实例

- 通过降低实际命中次数掩盖表现层问题

## 4. 降级策略

超过视觉预算时按顺序降低：

1\. 远处敌人动画降频

2\. 远处受击闪烁降频

3\. 伤害数字聚合

4\. 非关键 VFX 采样

5\. 屏幕外 View 低频更新

6\. 同类渲染批处理或 GPU Instancing

模拟真值不得随视觉降级改变。

## 5. 监控指标

每个压力测试输出 JSON：

- 平均/95/99 分位模拟 Tick 时间

- 平均/95/99 分位渲染帧时间

- 活动实体峰值

- 托管与 Native 内存趋势

- GC 次数与分配量

- 对象池命中、扩容和失败次数

- 触发链截断次数

- 无效 EntityHandle 访问次数

- 丢弃的 VFX 请求数

## 6. 性能回归门禁

M10 后，任何改变模拟或大量内容的 PR 都必须运行固定种子基准。超过已批准基线的阈值时，必须解释或修复后才能合并。

## 7. M5 阶段证据边界

M5 的 EditMode Headless Harness 在 finite 与 chunked-infinite 地图上各推进五分钟（9000 Tick），
验证固定种子、并发上限、Boss 一次性、有限位置、清理后实体计数和无效 Handle。它使用小型
Placeholder Encounter，是正确性/泄漏门禁，不是 1,500 敌人目标规模性能基准。

30 分钟 Soak、1,500/3,000/5,000 实体压力和 Tick 分位 JSON 在 M5 均为 `NOT RUN`，继续固定
在 M10 执行。M5 不据五分钟正确性 Harness 宣称达到最终性能预算。

## 8. M6 阶段证据边界

M6 自动玩家在固定 30 Hz 下推进十分钟（18,000 Tick），自动移动、拾取和选择升级；同一
Seed 运行两次并比较 Run 统计与校验值。该测试证明小型 Placeholder Encounter 的成长链路、
确定性和显式清理，不输出 Tick 分位或内存趋势，不能用来宣称达到目标规模性能预算。

30 分钟 Soak、1,500 敌人、3,000 投射物、5,000 拾取物及性能 JSON 在 M6 仍为 `NOT RUN`，
继续固定在 M10 执行。

## 9. M10 固定基准与优化决定

M10 正式配置为 30 Hz、54,000 Tick、预热 300 Tick、1,500 Enemy、3,000 Projectile、5,000
Pickup 和 200 活动 VFX。JSON 必须报告：

- Tick 与渲染 CPU average/p95/p99/max；
- EnemyDecision、Movement、Lifetime、Cleanup、SnapshotBuild 的累计/平均/最大值；
- 每模拟分钟的 Mono、Native 和 GC Heap 样本，以及持续增长判断；
- 热路径分配、GC collection、实体峰值、对象池命中/扩容/失败/丢弃；
- 触发链截断、无效句柄、最终实体数量与确定性 Checksum。

实现后短测（相同实体规模、300 个测量 Tick）的 Tick p99 为 7.0635 ms，渲染 CPU p99 为
1.2974 ms，热路径分配 0 B、GC collection 0。EnemyDecision 平均约 2.5470 ms，是已测最热系统；
Movement 与 SnapshotBuild 依次约 1.4100 ms、0.9700 ms。该证据没有显示迁移 Jobs/Burst 的必要性，
因此 M10 保持现有稠密批处理后端。若正式 54,000 Tick 失败，此决定必须重新审查，不能以短测覆盖。

正式 30 分钟模拟时间结果为 PASS：54,000 Tick 的平均/p95/p99/max 分别为 8.9125/10.2952/
10.9851/20.2094 ms；108,000 个渲染 CPU 探针样本为 0.6014/0.9809/1.2482/3.4024 ms。
31 个内存样本中托管段增长 0 B，Native 分段趋势为 -2,894 B；热路径分配 0 B，三代 GC 均为 0，
无无效句柄、触发截断或 VFX 丢弃。最终 Checksum 为 `13193d7c4cc3251a`，机器与完整数据见
`TestResults/M10Final/performance.json` 和 M10 结果报告。

## 10. Qinglan Demo 新 Runtime 预算

G0.3 不改变 30 Hz、1,500 Enemy、3,000 Projectile、5,000 Pickup、200 VFX 和 0 B 稳态分配目标。
G1.1 先在单线程稠密结构上实现；没有基准证据不得提前迁移 Jobs/Burst/ECS。

| 数据 | 预算/容量策略 | 超限行为 |
|---|---|---|
| Character Mechanic | 每 Actor 最多 4 个紧凑实例；Demo 玩家实际 1 | 拒绝 Run 装配并报告定义 ID |
| DamageChannel 状态 | 每 Actor 最多 8 个活动通道槽 | 稳定回收最早到期槽并记录诊断；不得分配字典 |
| Boss Phase | 每 Boss 最多 8 阶段 | Content Validation 阻断 |
| Elite Affix | 每 Enemy 最多 3 个，Demo 代数最多 1 | Spawn 前拒绝非法组合 |
| Map 状态 | 32 Objective、16 Event、32 Landmark | Map 装配失败，不在 Tick 中扩容 |
| Reward Choice | 同时最多 1 个阻塞选择；历史初始 128 | 排队到下一 Tick；容量由 Run 装配预估 |
| Reward Request | 初始容量至少等于 World actor capacity | 超容量增长计数非零即性能门禁失败 |

G1.1 性能步骤：

1. 在同一提交、同一机器、同一配置先重跑 M10 300-Tick 短基线，再运行启用空 Demo Runtime 的配对
   短测；各跑三次，报告中位 p99、分系统时间、分配、GC、容量增长和 Checksum。
2. 空 Demo Runtime 的 Tick p99 相对配对基线退化不得超过 15%，稳态分配必须 0 B，GC 为 0，所有
   预估容量增长为 0；超出必须修复或新增性能 ADR，不能只提高阈值。
3. Character Mechanic 单玩家推进 54,000 Tick，热路径 0 B、数值有限、无持续容量增长。
4. G1.5/G1.6/G2.8 加入真实 Enemy/Affix/Encounter/Map/Boss/Reward 内容后分别重跑对应短测；G3.5
   在目标硬件重跑正式 54,000 Tick 和 GPU/1% Low。

历史 M10 JSON 是框架比较基线，不代表新增内容已通过。每个新报告必须同时保留实际配置和
`TestResults/M10Final/performance.json` 对比，不得用 Preview 或小型正确性 Harness 替代。

## 11. Qinglan G1.6 Encounter 短测

G1.6 的 12 分钟双实例 Harness 是 Encounter 正确性证据，不是目标实体压力基准。它各推进 21,600 Tick，
Peak Enemy 为 16，验证两固定精英、并发、停止边界、有限坐标、0 非法句柄和显式清理。

另以 600 Enemy、1,200 Projectile、2,000 Pickup、100 VFX、900 测量 Tick 运行真实内容短测。脚本重编译
后的首轮虽然热路径 0 B、Tick p99 4.2761 ms，但测量窗出现 1/1/1 次 GC，结果为 `FAIL`。使用与正式
M10 基准一致的 300 Tick 预热后复测 `PASS`：Tick p99 4.1759 ms、Render p99 0.7682 ms、热路径 0 B、
GC 0/0/0，Checksum `b455f50ce958d212`。阈值未放宽；首轮与复测 JSON 均保留。

该 Null Device 渲染探针不能证明正式 GPU 表现。G3.5 仍必须在正式内容和目标硬件运行 54,000 Tick、
GPU/1% Low 与内存趋势；本节不能替代该门禁。

## 12. Qinglan G1.7 Reward Choice / Pack 短测

Reward Choice 只在外部奖励请求时扫描 Offer Catalog、分配只读候选投影和写低频历史，不进入无请求的
固定 Tick 热路径。同一时刻最多一个选择；历史初始容量由 Run 装配提供，正常 Demo 路径不应增长。

G1.7 以 600 Enemy、1,200 Projectile、2,000 Pickup、100 VFX、900 测量 Tick和 300 Tick 预热重跑
真实内容短测，结果 `PASS`：Tick p99 `4.2112 ms`、Render p99 `0.7268 ms`、固定 Tick 分配 0 B、
GC 0/0/0、无效句柄/Proc 截断/VFX 丢弃均为 0，Checksum `b455f50ce958d212`。该结果与 G1.6
Checksum 一致，未出现由低频适配器导致的确定性漂移。

候选请求本身是低频路径，G1.7 没有把其临时数组分配宣称为 0 B 热路径。目标硬件 GPU、完整地图、
Boss、拾取/奇物和选择 UI 仍未进入本短测，必须由 G2.8/G3.5 的真实可玩切片与正式内容证据关闭。

## 13. Qinglan G2.2 Boss Runtime 短测

Boss Spawn 的唯一技能预加载属于低频装配；固定 Tick 阶段解析、规则 Mask 和技能抑制使用预分配数组。
专项测试循环解析听风 Phase 54,000 次，当前线程分配为 0 B。12 分钟 Headless 双实例各 21,600 Tick，
两 Boss 各一次，Peak Enemy 16、0 InvalidHandle，并在结束后清空所有 Boss 技能和 Delivery。

目标规模短测继续使用 600 Enemy、1,200 Projectile、2,000 Pickup、100 VFX、900 测量 Tick和 300 Tick
预热，结果 `PASS`：Tick p99 `5.2451 ms`、Render p99 `0.8541 ms`、热路径 0 B、GC 0/0/0、无效句柄/
Proc 截断/VFX 丢弃均为 0，Checksum `b455f50ce958d212`。相对 G2.1 的 4.6635 ms p99 增加约 12.5%，
仍低于 15% 工作包回归阈值和 33.33 ms Tick 预算，因此不迁移 Jobs/Burst。

此场景以 600 个草灵覆盖框架规模，不会同时模拟最终 Boss 的完整视觉压力；完整地图、Boss Telegraph、
GPU/1% Low 和 54,000 Tick 正式内容基准仍由 G2.8/G3.5 执行，本节不能替代对应门禁。
