# G2.8 Placeholder 垂直切片统一门禁

- 工作包：G2.8
- 对应模块：M01—M16 集成门禁
- 分支：`codex/qinglan-demo-implementation`
- 输入：G1.1—G2.7 已冻结内容、运行时、UI 与程序化表现
- 结论：`COMPLETE`

## 1. 范围与非范围

本工作包不增加正式内容或第二套运行时，而是以真实 `QinglanDemoRunFactory`、`RunSession`、旧演武场、
Encounter、Build、Reward、Boss、Profile 和单一 Host 验证 12 分钟 Placeholder 垂直切片。门禁同时覆盖
三条目标/构筑路线、实际地图出生保护、十次 Host 生命周期、600 敌人可读性压力、完整性能 Soak、
Windows Development Build 和独立 Player 闭环。

正式 Sprite、动画、Shader、音频、字体、目标 GPU、1% Low、Release 和商业合规仍属于 G3，不能由本
工作包的程序化图形或 Null Device CPU 数据替代。

## 2. 集成审计与修复

| 缺口 | 修复 | 保持的边界 |
|---|---|---|
| Map Event 有内容但真实 Run 未显式 Armed | Composition Root 按已选 Map 的 EventIds 初始化时统一 Arm | Map Runtime 不读取具体 Event ID |
| 风脉台已完成但最终 Boss 未消费 Rule Mask | Boss 系统每 Tick 从通用 `MapObjectiveRuntime` 同步已完成规则 | 无听风/风脉台具体 ID 分支 |
| 障碍修正后的出生点可能回落到玩家保护圈 | 修正点必须再次满足 MinimumSpawnDistance，否则使用已采样安全点 | Encounter 预算、随机流和地图真值不变 |
| Actor 移除后，Area/Projectile 仍可能读取旧 Owner | 所有 Owner 读取增加 generation-safe `Contains`；Cleanup 消费同阶段追加命令 | 不隐藏 InvalidHandle 诊断，不延迟到下一 Tick |
| P0 只有语义优先级，没有落实 Sprite 遮挡顺序 | Critical/Mechanic/Combat/Decoration 映射到 40/30/20/10 排序 | 只改表现，不改命中和伤害 |

上述变化没有修改 Content/Save Schema、30 Hz、冻结公共 API 或 Assembly 依赖方向，因此不新增 ADR。

## 3. 真实 12 分钟自动玩家

Editor 门禁使用真实生产装配，只由确定性驱动提供移动、交互、升级、奖励选择和节奏化 Boss 伤害。驱动
不替换 Map、Encounter、Progression、Reward、Boss 或 RunResult 系统。每局至少推进 21,600 Tick；最终
结果在 21,784 Tick、约 726.13 秒完成。

| 路线 | Seed | 完成目标 | 听风 Rule Mask | 等级 | 击杀 | 奖励选择 | 结果 |
|---|---|---:|---:|---:|---:|---:|---|
| 综合 | `0x4732385645525441` | 3 | 7 | 38 | 2,636 | 4 | Victory |
| 移动御剑 | `0x4732384D4F42494C` | 1 | 1 | 38 | 2,609 | 3 | Victory |
| 符阵/草木 | `0x4732384649454C44` | 2 | 6 | 39 | 2,639 | 3 | Victory |

综合路线以同 Seed 重放一次，Spawn、Objective、Boss、Decision 和 Combined Checksum 全部一致。三条路线
的 Decision Checksum 互不相同；每局均完成 3 Event、发现并领取 5 Landmark、击败 2 Boss、观察两只
Boss 的三个阶段、取得 Relic/Evolution，且 InvalidHandle 为 0。

## 4. 实际地图出生公平

旧演武场生产 `RuntimeMapDefinition` 和 `EncounterScheduler` 独立推进 21,600 Tick：产生 2,552 个普通
出生请求、2 个一次性 Boss 请求；所有位置可行走，普通出生距离下界为 14、实测上界为 60，时间轴结束
后预算停止。Boss 固定锚点不冒充普通出生保护样本。

## 5. 生命周期、输入与 Player

- 全量 PlayMode 保留 G2.6 的键盘/手柄闭环，并新增十次真实 Bootstrap Host：Run→Result→Hub→再次
  出发；每次 Hub 后 Active View 为 0，输入 Owner 恒为 1，VFX≤200、AudioSource≤32。
- 独立 Development Player 通过公共 UI/Application 命令完成标题、选角、地图/Loadout、Run、暂停/
  恢复、可访问性、升级、Result、Profile 提交、Hub 清理和再次出发；退出码为 0。
- Player 冒烟使用隔离 Save Root，不读取或覆盖用户存档。

## 6. Placeholder P0 可读性

可读性命令使用生产 Profile/Coordinator/Map 和 600 个真实 Enemy 实例，再加入 18 个通用 P0 Area；一个
生产 Tick 后实际出现 915 个 View、318 个 CriticalDanger View。自动检查确认玩家 Triangle、听风
Hexagon、危险 Ring 均存在，最低 P0 排序 40 高于最高普通 Combat 排序 20。

标准与 High Contrast 1920×1080 截图均完成人工复核：标准模式使用色相＋形状＋轮廓，高对比模式即使
敌我色相压缩，玩家三角、Boss 六边形和危险圆环仍可辨。该结论只覆盖程序化 Placeholder，不是正式
美术的 GPU/Overdraw 或视听质量签字。

## 7. 性能、确定性与构建

完整目标规模门禁为 54,000 Tick / 1,800 秒、1,500 Enemy、3,000 Projectile、5,000 Pickup、200 VFX：
峰值 9,501 实体，Tick p99 11.4069 ms、Render CPU p99 1.2213 ms、热路径 0 B、GC 0/0/0、无效句柄 0，
Checksum `13193d7c4cc3251a`，预算判定 `PASS`。热点仍是 EnemyDecision，但 p99 保有充分余量，不迁移
Jobs/Burst。

Windows x64 Development Build 与独立 Player 均 `PASS`。Manifest 记录 Unity `6000.3.20f1`、Content
Schema 6、Save 3、Settings 3、Profile 3、Recovery 2，EditMode/PlayMode/Validation/Soak 均为 `pass`，
Placeholder 210、未批准资产 0。

## 8. G2.8 退出门禁

| 检查 | 结果 |
|---|---|
| 真实 Factory 三路线＋同 Seed 重放 | PASS |
| 21,600 Tick 实际地图出生保护 | PASS |
| 全量 EditMode 292/292 | PASS |
| 全量 PlayMode 17/17 | PASS |
| 十次 Host 生命周期 | PASS |
| 600 敌人 P0 自动＋人工可读性 | PASS |
| Validation / API Freeze / Pack 双构建 | PASS |
| 54,000 Tick 目标规模性能 | PASS |
| Development Build / 独立 Player | PASS |
| 正式资产、目标 GPU、Release | NOT RUN；G3 范围 |

## 9. G3.1 输入

G3.1 必须先把 provenance/Hash 扫描扩展到全部实际 Release 输入，再按 G0.4 Manifest 小批量导入正式
角色、敌人、Boss、地图、UI 和 VFX Profile。每批保持来源、生成参数/条款日期、许可、Hash、审查和
Addressables 同提交；任何缺失不得取得 `release` 标签。
