# 15 G1.7 完整 Pack 与受控显化奖励门禁

## 1. 工作包目标

G1.7 关闭 G1 数据切片，交付两个彼此独立但在同一门禁验证的能力：

1. 为显化宝匣提供独立于普通升级的受控 Evolution Reward Choice 适配器；
2. 将 `qinglan.pack.demo` 的 94 个程序化 Placeholder 定义纳入稳定 Addressables、双语、本地 Catalog、
   两次 Pack Build 和 Windows x64 Development Build 审计。

本工作包不创建显化宝匣、Boss、精英奖励消费者、拾取物、奇物卡牌或选择 UI。实际奖励内容与消费来源
由 G2.2/G2.3 实现，页面与输入由 G2.6 实现；正式资源仍只允许在 G3 经 provenance 门禁导入。

## 2. 决策与所有权

- CR-2026-007 / ADR 0018 批准 `RewardChoiceRuntime` 作为受控 Evolution 选择的唯一 Simulation 真值。
- 普通 `OfferGenerator` 继续负责 Level-up Reroll/Banish/Skip；显化选择不复用该暂停上下文。
- `RewardRuntime` 继续拥有 `RewardTransactionId` 的唯一提交记录；选择适配器不能建立第二套幂等表。
- `BuildState` 负责最后一刻重新检查资格并应用 Evolution；Application 只展示只读投影与提交 ContentId。
- Reward RNG 使用从 Run Seed 派生的独立流，不受普通 Offer、Combat、Encounter 或 Map 调用顺序影响。

## 3. 运行时结构

```text
Reward source (G2)
  → RewardTransactionId(run, source, sequence)
  → RewardChoiceRuntime.RequestEvolutionChoice
      ├─ 已提交：AlreadyCommitted，不重复发奖
      ├─ 无资格：提交事务并记录 fallback，不暂停、不消耗 Reward RNG
      └─ 有资格：从锁定 Evolution Offer 加权不放回取 1—3 项
          → ProgressionRuntime.PauseRequested
          → RunSession / GameState.RewardChoice
          → UI-safe RewardChoice 投影
          → SelectReward(ContentId)
          → BuildState 再验证并原子转换
          → RewardRuntime 提交事务
          → 恢复固定时钟与 InRun
```

同一时刻只允许一个阻塞选择。相同事务重复请求返回 `AlreadyPending` 或 `AlreadyCommitted`；不同事务在
已有选择时返回 `Busy`。非法候选不会提交事务、不会改变 Build，也不会恢复时钟。

## 4. 候选与回退规则

| 规则 | 实现 |
|---|---|
| 候选来源 | `BuildRuntimeCatalog.Offers` 中 `TargetKind=Evolution` 且 `InitiallyUnlocked=false` |
| 资格 | 源技能/心诀等级、前置、互斥、目标未拥有和容量均由 `BuildState` 判断 |
| 数量 | 调用方请求 1—3，实际数量为 `min(请求数, 合格数)` |
| 抽取 | Reward 独立随机流，按 Weight 加权不放回 |
| 空池 | 直接提交同一事务并记录稳定 fallback ContentId；RandomCalls 不增加 |
| 选择 | 只接受当前 Snapshot 中的 OfferId；提交前重新检查资格 |
| 重放 | 已提交事务永不再次转换或回退 |

G1.7 的 fallback 只完成“选择事务已决定为哪个稳定奖励 ID”的记录；治疗、货币、奇物或其他 Reward
操作的实际执行属于 CR-2026-008 / G2.3，不能把本阶段记录误报为已发放完整奖励。

## 5. Application 契约

- `GameState.RewardChoice` 以追加枚举值进入状态机，旧枚举数值保持不变。
- `RunSession.Advance` 在 Reward Choice 期间返回 0；固定模拟时钟保持暂停。
- `RewardChoice` 投影包含 RunId、SourceId、Sequence、候选和 FallbackId，不暴露可变 BuildState。
- `SelectReward`/`SelectRewardAt` 只有 Simulation 返回 `Committed` 才清空投影并恢复运行。
- Reward Choice 优先于同 Tick 形成的普通 Level-up Choice；完成后普通升级流程保持原行为。

## 6. 完整 Placeholder Pack 门禁

`QinglanG17PackSetup` 对 Pack 和每个定义执行以下检查/配置：

- Pack 必须为 `qinglan.pack.demo` 0.5.0、Content Schema 6、精确 94 个定义；
- 所有定义必须位于 `Assets/GameAssets/Placeholder/QinglanDemo/`；
- 每项 Address 为 `qinglan/demo/content/<canonical id path>`；
- 每项与 Baked Catalog 均包含 `pack.qinglan.demo`、`placeholder`、`development-only`；
- Baked Catalog Address 固定为 `packs/qinglan.demo/catalog`；
- 每项名称/描述必须在 `zh-Hans` 和 `en` 中均为非空；
- 配置前后两次 Bake 的 Definition Count 与 Content Hash 必须一致。

CLI 连续两次 Pack Build 的 Qinglan `catalog.json` 必须字节相同。Development Build 清单必须声明
`StandaloneWindows64`、Development、测试证据全 pass、Pack includedInPlayer 且无未批准资产。

## 7. API 与兼容

- Game.Core：168 / `25766747...d7e176`，不变；
- Game.Content.Runtime：940 / `cd72d779...e35b00`，不变；
- Game.Simulation：1192 / `57e2944c...87875`，批准追加 32 条，无删除；
- Game.Application：355 / `f57fe00c...f8a6`，批准追加 9 条，无删除；
- Game.Platform.Abstractions：73 / `8eb5f2cc...a51738`，不变。

新增接口只追加受控选择与只读投影；旧构造、Level-up Offer、枚举数值和已发布 ContentId 均保持兼容。

## 8. 测试矩阵

| 场景 | 最低断言 |
|---|---|
| 完整 Pack | 0.5.0 / Schema 6 / 94 / 固定 Content Hash；Catalog、双语、Address/Label 完整 |
| 随机隔离 | 普通 Offer 预先抽取不改变 Reward Choice 候选；同 Seed 顺序一致 |
| 合格选择 | 只出现锁定且合格的 Evolution；非法选择不提交；合法选择只提交一次 |
| 空池 | fallback 提交、无暂停、0 Reward RNG 调用、重放不重复 |
| Application | 选择时暂停，提交后恢复，随后普通升级仍可进入/选择 |
| 回归 | 全量 EditMode、PlayMode、Project Validation、API Freeze |
| 性能 | 600/1,200/2,000/100，900 Tick＋300 预热，0 B 固定 Tick、0 GC |
| 交付 | 两次 Pack CLI 字节一致；Windows x64 Development Build 与 Player 启动冒烟 |

## 9. G1.7 实际冻结值

- Content Hash：`798dbb302dda57b9f0158e83010ee89392ffdc291cc629715ba357b691ebd5ad`；
- 两次 Pack Catalog SHA-256：`9d3979964418cecfda875e5e2dba9d1f067f4c3daafeebe0f7b63db71de200cb`；
- 性能短测：Tick p99 `4.2112 ms`，Render p99 `0.7268 ms`，0 B，GC 0/0/0；
- Development Player SHA-256：`5d7eeb5359c2e35e4eb1f6a5844b25c3d7556795bd2f15ec234a2011406bc9c6`。

退出条件：本文件第 8 节自动门禁全部 PASS，实际消费者与 UI 明确保持 `NOT RUN`，才允许进入 G2.1。
