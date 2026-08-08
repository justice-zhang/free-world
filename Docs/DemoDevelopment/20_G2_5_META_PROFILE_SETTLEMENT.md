# 20 G2.5 局外成长、Profile 与原子结算

## 1. 工作包目标

G2.5 交付 M11/M14 的无 UI 真值层：12 个局外节点、3 个嵌片、4 个设施、3 篇故事、6 件藏品，
以及 Profile 3 的购买、装配、结算、Recovery 拒绝和平台提交门禁。实际页面、导航和输入属于 G2.6；
完整任意 Tick 恢复仍按 CR-2026-015 延期。

本阶段不提升 Content Schema 或 Save Schema：`qinglan.pack.demo` 升至 0.9.0，仍为 Content Schema 6；
Profile 仍为 Schema 3。所有持久状态只保存稳定 `ContentId`，Simulation 不读取文件、Profile 或平台。

## 2. 所有权与依赖

| 模块 | 所有权 | 输入 | 输出 |
|---|---|---|---|
| `QinglanMetaProgression` | 低频局外规则 | Profile、Registry、候选 Loadout | 校验结果、购买候选、设施快照、运行 Loadout |
| `QinglanProfileCoordinator` | 单一 Profile Owner | Storage、Flow、不可变 RunResult | 原子 Profile、Recovery 清理、Committed Event |
| `DemoRunCoordinator` | 页面与单局生命周期 | 结算提交确认 | 保存前 Result 门禁、保存后 Hub 转换 |
| `QinglanDemoRunFactory` | 一局组合根 | 已验证 MetaLoadout、唯一奖励快照 | Build 注入、Reward 唯一所有权初始化 |
| `PlatformApplicationEventRouter` | 平台边界 | `RunResultCommitted` | 胜利成就/平台输出 |

`Game.Application` 只依赖 Save/Content/Platform 抽象，不依赖 Unity Object。`BuildState.GrantMetaOutput`
只在开局低频装配期接受已验证 Trait、UpgradeOffer 或 Synergy；固定 Tick 没有文件访问或 Meta 查询。

## 3. 内容拓扑

### 3.1 局外节点与容量

三条分支均为四级链：

| 分支 | 稳定 ID 前缀 | 主题 | 费用 |
|---|---|---|---|
| 本命 | `qinglan.meta.lu_qingye.innate.01`—`.04` | 候选亲和、机制余裕、预览、显化资格 | 0 / 20 / 35 / 60 |
| 身法 | `qinglan.meta.lu_qingye.movement.01`—`.04` | 移速、回复、拾取、路线终端 | 0 / 20 / 35 / 60 |
| 心境 | `qinglan.meta.lu_qingye.mind.01`—`.04` | 信息、取舍、地标记录、风险终端 | 0 / 20 / 35 / 60 |

`.04` 是 Terminal，三者互斥。运行装配最多 6 个 Branch、1 个 Terminal、2 个 Insert；校验顺序为：
稳定类型、所有权、重复、前置、互斥、分类容量。免费重置只替换装配，不退款、不扣费。

永久数值只使用有限选择、信息和容错：小幅移速、冷却、回复、拾取范围、投射物速度或纯规则标签；
不提供无限伤害/生命成长。货币固定为 `qinglan.currency.spirit_sand`。

### 3.2 嵌片、设施、故事与藏品

- 嵌片：`qinglan.insert.qinglan_wind_pattern`、`qinglan.insert.herb_garden_spring_clasp`、
  `qinglan.insert.old_court_vein_needle`，单件费用 30，分别表达进攻、防御、探索槽位。
- 设施：问脉台、藏卷楼、百器阁、万象阁；条件由节点、故事或藏品稳定引用及通用标签解析。
- 故事：`hearing_sword`、`old_sword_and_gourd`、`refusing_inheritance`；第三篇带
  `story.victory_only`，只由首胜青岚脉印解锁。
- 藏品：`qinglan.collectible.old_court.01`—`.06`，按三个 Topic 分组，AcquireRule 指向地标或故事。

新增内容不需要修改 Profile Owner；设施派生、故事和藏品都从 Registry 定义解析。Schema 6 只扩展
MetaFacility/Story 已有引用字段的合法目标类型，不增加 DTO 字段。

## 4. 缺失内容与运行快照

Profile 中缺失或非法的 Loadout ID 原样保留，并产生本地化警告；运行投影降级为 `MetaLoadout.Empty`。
未经用户确认不把安全默认写回 Profile，避免临时缺包造成永久数据丢失。

`RunDescriptor` 冻结 MetaLoadout 与已领取唯一奖励集合。Factory 再次校验输出类型，把通用 Meta 输出
注入 Build，并把唯一奖励快照交给 `RewardRuntime`；重复听风首通在局内走既有 fallback，不重复生成
唯一进度。

## 5. 原子结算协议

唯一合法顺序：

```text
冻结 RunResult
→ 校验稳定 ID、类型、Outcome 与事务
→ 只在内存合并候选 Profile
→ 原子写 profile.json
→ 清 run_recovery.json
→ 发布 RunResultCommitted / 平台输出
→ ConfirmResultCommitted，允许离开 Result
```

- Profile 写失败：当前 Profile、Recovery、事件和页面状态都不变，原结果可重试。
- Profile 已写而 Recovery 清理失败：事务已持久化，页面仍不可离开；同进程保存一次待发布事件。
- 重试命中 `AlreadyCommitted`：只补清 Recovery，不再发奖；同进程仅补发一次待发布事件。
- 进程重启后命中 `AlreadyCommitted`：可补清 Recovery，但不重发平台输出，以重复安全优先。
- 保存完成前 UI 不得显示“已保存”，也不得调用 Hub/Title/StartAgain 清掉结果。

Victory 写合法灵砂/藏品/故事/统计、地图首通、唯一青岚脉印及胜利限定故事；Defeat/Abandoned 保留
合法灵砂、藏品、非胜利故事和统计，但过滤首通、唯一奖励和第三篇故事。

## 6. RecoveryRejected

启动发现不完整 Recovery 时只产生本地化拒绝提示；没有 Continue，也不尝试重建 World。用户明确开始
新局才删除 Recovery；删除失败时仍停在可重试的拒绝结果。`RecoveryRejected` 固定空 Delta，不写
Profile、不统计胜利、不授予首通或平台成就。

## 7. API 与兼容

ADR 0023 接受 Simulation 1 条、Application 73 条公开签名追加，删除 0。旧 RunDescriptor、
MetaLoadout、DemoRunCoordinator 构造和 M8 `RunCompleted` 路径保留；G2.5 使用独立
`RunResultCommitted`，避免旧处理器再次写 Profile。Core、Content Runtime、Platform Abstractions 的
规范签名逐字节不变。

## 8. 验证矩阵

| 检查 | 覆盖 |
|---|---|
| G2.5 EditMode | 内容拓扑、购买/设施、容量/终端、缺失 ID、胜负、幂等、双失败重试、Recovery 拒绝、Factory 注入 |
| G2.5 PlayMode | 真实 Title→Run→Result；Profile 保存和 Recovery 清理前禁止离开，提交后才进入 Hub |
| 全量 EditMode/PlayMode | 276/276、13/13，覆盖 M0—G2.4 回归 |
| Project/API | 治理、内容、本地化、冻结 Hash 和 74/0 签名差异 |
| 内容双构建 | 两份 0.9.0 Catalog 逐文件 SHA-256 相同 |
| 性能短测 | 600 Enemy / 1200 Projectile / 2000 Pickup / 100 VFX，900 Tick，0 B/0 GC |
| Windows Build | x64 Development Build、BuildManifest 四项证据 pass |

## 9. G2.6 边界

G2.6 只能消费本阶段公开投影和命令，实现实际标题、选择、结算、据点、故事/收藏、键鼠/手柄和可访问性
页面。UI 不复制 Profile 合并逻辑，不直接删除 Recovery，不在保存完成前显示成功，也不把设施状态写回
Scene Object。正式美术、音频、字体和 Release Player 仍属于 G3。
