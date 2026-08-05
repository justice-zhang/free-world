# G1.6 十二分钟 Encounter 时间轴实施切片

## 1. 范围与边界

本工作包实现 M09 的普通敌群时间轴、两次固定精英、并发保护、确定性回放和 12:00 停止边界。
`qinglan.encounter.old_court.demo_12m` 已进入 `qinglan.pack.demo` 0.5.0 / Content Schema 6；Pack 共
94 个定义，Baked Content Hash 为
`798dbb302dda57b9f0158e83010ee89392ffdc291cc629715ba357b691ebd5ad`。

G1.6 不创建 Boss。6:00 折枝和 12:00 听风的 Enemy/BossDefinition、Boss Phase 与 BossRule 由
G2.2 实现，因此本包的“两 Boss 一次”明确为 `NOT RUN`，不以普通敌人或精英冒充 Boss。地图风脉台、
事件、公平出生点 PlayMode 和完整 Reward 也分别保留给 G2.1、G2.3、G2.6/G2.8。

## 2. 九段时间轴

| 阶段 | 时间 | Budget/s | Interval/s | Cap | 敌人数 | Pattern | 固定规则 |
|---|---|---:|---:|---:|---:|---|---|
| P0 | 0:00—1:30 | 2→3 | 1.10→0.90 | 120 | 1 | Ring | 草灵教学 |
| P1 | 1:30—3:00 | 3→4.5 | 0.95→0.75 | 180 | 2 | Edge | 加入纸鹤 |
| P2 | 3:00—4:30 | 4.5→6 | 0.80→0.65 | 240 | 3 | Ambush | 3:00 剑傀精英 |
| P3 | 4:30—6:00 | 6→7.5 | 0.70→0.55 | 320 | 4 | Line | 加入石灯 |
| P4 | 6:00—6:30 | 1→1 | 1.25→1.25 | 80 | 2 | Ring | 为 G2.2 折枝预留低压窗 |
| P5 | 6:30—7:30 | 7→9 | 0.60→0.50 | 360 | 5 | Cluster | 加入风铃 |
| P6 | 7:30—9:00 | 9→11 | 0.52→0.43 | 440 | 6 | Edge | 7:30 石灯精英、加入种囊 |
| P7 | 9:00—10:30 | 11→14 | 0.46→0.38 | 520 | 6 | Cluster | 全池复合 |
| P8 | 10:30—12:00 | 14→18 | 0.40→0.32 | 600 | 6 | OffscreenRandom | 高压全池 |

Encounter 全局上限为 720，生成距离为 14—24。每个普通 Entry 都携带四词缀池，供难度快照的
概率精英使用；两个固定精英各从同一 canonical Pool 中最多选择两个合法词缀。P4 的低压窗只冻结
普通预算，不把尚未存在的 Boss 计入本包结果。

## 3. 一次性精英通用契约

现有 Entry Elite 标志和概率均不能表达固定时点一次触发。CR-2026-016 与 ADR 0017 因而批准
`RuntimeEncounterEliteRule`：EnemyId、绝对 SpawnTime、Pattern、可选 AnchorId 与 AffixPoolIds。

- Schema 6 DTO/Authoring/Bake/Validator/Hash 支持可选 `elites`；旧 Schema 1—5 不读取。
- 旧 Phase 构造函数保留，EliteRules 默认为空；非空数组才参与 Hash，既有 Fixture 不漂移。
- Scheduler 顺序固定为一次性精英、Boss、普通组，并为未触发一次性规则预留并发槽。
- 容量暂满时在所属 Phase 内重试；实际排队后才消耗 Encounter RNG，精英保持 `Boss=false`。
- 最后 Phase 结束后预算与冷却立即清零，不允许积压预算在 12:00 后恢复生成。

Content Runtime 公共 API 从 923 增至 940 条，仅追加 17 条批准签名；Core、Simulation、Application、
Platform 四个冻结程序集签名逐字节不变。

## 4. 无头验证

固定 Seed 为 `0x473136454E434E54`。每次实际运行 21,600 Tick，依次执行 Spawn、EnemyDecision、
Movement、Damage、Death、Lifetime、Cleanup、EventFlush 和 Snapshot。测试每两秒通过真实 Damage/
Death/Cleanup 管线清理敌人，以持续验证完整预算曲线；它不替代 G1.5 已覆盖的敌人技能行为。

双实例结果完全一致：2,582 Spawn、2,571 Death、2 Elite、2 Affixed、0 Boss、Peak 16、0 非法句柄，
Spawn Checksum `ee195d6e87a8a4c7`、Death Checksum `102ba2372b96243c`、Combined Checksum
`e86df634f50d29e8`。位置有限且可行走，并发上限、停止生成、预算清零和显式清理均通过。

## 5. 性能与失败证据

600 草灵、1,200 投射物、2,000 拾取物、100 VFX、900 测量 Tick 的首轮运行热路径为 0 B，Tick p99
4.2761 ms，但脚本重编译后的首次运行在测量窗发生 1/1/1 次 GC，因此结果为 `FAIL`。不修改阈值，
按既有正式基准配置恢复 300 Tick 预热后复测为 `PASS`：Tick p99 4.1759 ms、Render p99 0.7682 ms、
热路径 0 B、三代 GC 0/0/0，Checksum `b455f50ce958d212`。

## 6. 后续前置条件

- G1.7 完成 Reward Choice Context、完整 Pack/双语 Placeholder 验证和 Windows Development Build。
- G2.2 把折枝与听风的通用 Boss 内容和一次性 BossRule 接入本 Encounter，再关闭“两 Boss 一次”。
- G2.6/G2.8 在真实地图、相机和输入下验证出生公平、阶段可读与 Boss 过渡。
- G3.4/G3.5 才能冻结正式平衡和目标硬件/GPU 证据；本包结果不得外推为正式画面性能。
