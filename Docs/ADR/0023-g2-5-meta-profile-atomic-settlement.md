# ADR 0023：G2.5 数据驱动局外成长与 Profile 原子结算

- 状态：Accepted
- 日期：2026-08-08
- 决策人：依据用户当前连续 Demo 开发与自行决策授权
- 关联里程碑：G2.5、M11、M14
- 关联 CR：CR-2026-012、CR-2026-015
- 承接：ADR 0013、ADR 0015、ADR 0022

## 背景

G2.4 已冻结稳定 `RunDescriptor/RunResult`，但 Result 页面仍可在 Profile 保存前离开；旧 M8
`RunCompleted` 还会独立累加一次统计并直接清 Recovery，不能表达首通、唯一奖励、故事、藏品和事务
幂等。G2.5 必须让一个 Application Owner 消费同一不可变结果，且任何保存失败都不能丢失结果或显示
“已保存”。

Schema 6 已有 MetaNode、MetaInsert、MetaFacility、Story、Collectible 类型，但设施条件只接受节点/目标，
故事条件只接受节点/目标/设施，不能数据驱动表达“首次故事后开放藏卷楼、首次藏品后开放万象阁、地标
触发故事、首胜脉印触发第三篇故事”。字段本身足够，只需批准引用目标类型的兼容扩展。

## 决策

### 单一 Profile Owner 与提交顺序

新增低频 `QinglanProfileCoordinator`，它独占当前 Profile 快照并串行化购买、免费装配重置和 RunResult
提交。唯一合法顺序为：

```text
冻结 RunResult
→ 校验全部稳定 ID、类型、货币与 Outcome 资格
→ 在内存构建候选 Profile
→ 原子写 profile.json
→ 清 run_recovery.json
→ 发布 RunResultCommitted
→ 确认 Result 页面可离开
```

Profile 写失败时不替换当前快照、不清 Recovery、不发布事件；原结果仍可重试。Profile 已写而 Recovery
清理失败时，持久化 `TransactionId` 阻止第二次发奖；同一进程保留一次待发布事件，清理重试成功后只
发布一次。进程重启后遇到 `AlreadyCommitted` 只补清 Recovery，不重新发布平台输出，以重复安全优先。

`RecoveryRejected` 固定空增量，只在用户明确开始新局时清标记，不写 Profile、不算胜利，也不提供
Continue。无效/缺失 Recovery 内容仍显示本地化拒绝提示，不能尝试重建 Simulation。

### Outcome、首通和推导内容

- Victory 保留合法货币/故事/藏品，并写首通地图、唯一脉印与带 `story.victory_only` 的第三故事。
- Defeat/Abandoned 保留合法货币、地标推导藏品、已触发非胜利故事和统计；过滤唯一进度、首通和第三故事。
- 重复 Transaction 返回 `AlreadyCommitted`，不重复货币、统计、唯一物或事件。
- 地标的 `StoryId` 与 Collectible 的 `AcquireRuleId` 由 Registry 泛型解析；不在 Application 按具体
  地标、故事或藏品 ID 分支。
- 设施条件使用定义标签表达 `meta.initial`、`meta.condition.any_upgrade` 和
  `meta.condition.any_collectible`；新增角色/节点/藏品不修改核心程序集。

### Meta 装配与运行快照

`QinglanMetaProgression` 校验最多 6 个 Branch、1 个 Terminal、2 个 Insert、所有权、前置、重复和互斥。
购买只消费 `qinglan.currency.spirit_sand`；免费重置只替换稳定 ID，不退款也不扣费。缺失/非法存档 ID
保留在原 Profile，并投影为安全空 `MetaLoadout`，未经用户确认不静默重写文件。

RunDescriptor 追加不可变 MetaLoadout 与已领取唯一奖励快照。Factory 二次结构校验后，把每个定义的通用
Trait/UpgradeOffer/Synergy 输出注入 Build，并把唯一奖励快照交给 RewardRuntime，使重复听风胜利在局内
走 RewardDefinition fallback。Simulation 不读取 Profile、文件或平台。

### Content Schema 6 兼容扩展

不增加字段、不提升 Schema 版本，只扩大既有 `UnlockConditionId` 的允许目标：

- MetaFacility：MetaNode、MapObjective、Story、Collectible；
- Story：MetaNode、MapObjective、Landmark、Story、MetaFacility、Trait。

所有引用仍须存在并通过类型检查；该扩展只让已批准的五类 Meta 定义表达 M11 规则，不允许任意类型引用。

## 兼容与影响

- 保留旧 M8 `RunCompleted` 路径；新 G2.5 只发布独立 `RunResultCommitted`，避免旧保存处理器二次写 Profile。
- 保留 `DemoRunCoordinator` 两参数构造，其行为仍允许 G2.4 测试路径；实际 Demo 使用三参数构造启用持久化门禁。
- 保留原 RunDescriptor、MetaLoadout 构造；新增无 Terminal、安全空装配、Meta/唯一快照重载。
- 不改变程序集依赖方向、30 Hz Tick、Save Schema 3、Content Schema 6 编号或稳定 ID 规则。
- API Freeze 审计确认 `Game.Simulation` 追加 1 条、`Game.Application` 追加 73 条，删除均为 0；Core、
  Content Runtime、Platform Abstractions 逐字节不变。具体规范签名记录在 `PUBLIC_API_FREEZE.md`。
- 购买、装配、保存、推导和事件均为页面/结算低频路径；固定 Tick 不做文件访问或集合推导。

## 被拒绝的方案

- 复用旧 `RunCompleted`：M8 会再次增加统计并绕过新的候选 Profile/幂等事务。
- 保存成功前先清 Recovery 或发布平台：写失败会形成奖励丢失或平台与本地不一致。
- 把设施/第三故事 ID 写进 Coordinator 分支：新内容必须改核心程序集，违反内容扩展规则。
- 缺失 Loadout ID 时立刻覆盖 Profile：会丢失可诊断数据，并可能在临时缺包后永久破坏装配。
- 失败局完全丢弃 Delta：与 M11 的合法灵砂、藏品、故事和统计保留规则冲突。

## 迁移、回滚与测试

Profile 仍使用已实现的 v1→v2→v3 连续迁移；G2.5 不改 Codec 字段。旧 Profile 的未知稳定 ID 继续读取并
报警，Meta 运行快照使用安全降级。回滚可停止使用新 Owner，但已提交 TransactionId 必须继续保留；新增
公开 API 不得删除，Content 0.9.0 可回退到 0.8.0 且 Profile 的未知 Meta ID 仍可被旧缺失内容策略隔离。

测试必须覆盖 12/3/4/3/6 内容拓扑、容量/互斥/前置/免费重置、缺失 ID 降级、Victory/Defeat、重复提交、
保存失败重试、Recovery 清理失败重试、事件顺序、RecoveryRejected、真实 Factory Meta 注入和 Result 页面
门禁；并运行完整 EditMode、PlayMode、Project Validation、API Freeze、内容双构建和 Windows x64
Development Build。
