# ADR 0018：G1.7 受控显化奖励选择与 Pack Gate

- 状态：Accepted
- 日期：2026-08-06
- 决策人：依据用户当前连续 Demo 开发指令
- 关联里程碑：G1.7、G2.3、G2.6
- 关联 CR：CR-2026-007、CR-2026-008

## 背景

G1.4 已创建六个锁定 Evolution Offer；它们不能进入普通 Level-up 的 Reroll/Banish/Skip 流。G2.3 的
显化宝匣需要一个已验证的 Run-local 选择所有者，而 G1.1 的 `RewardRuntime` 只提供事务去重骨架。
同时 G1.7 必须证明 G1 数据 Pack 能以双语 Placeholder 元数据稳定 Bake 并生成 Development Build。

## 决策

### RewardChoiceRuntime

`ProgressionRuntime` 组合一个 `RewardChoiceRuntime`，复用 `RewardRuntime` 的
`RunId + SourceStableId + Sequence` 去重键。请求只枚举：TargetKind 为 Evolution、初始锁定、当前
BuildState 资格/前置/互斥均满足的 Offer。最多三个候选从独立 Reward 随机流按权重无放回选择；普通
Offer 流调用次数不影响结果。

候选冻结后请求暂停下一 Tick。Application 只投影稳定 ID，并新增 `GameState.RewardChoice` 与
SelectReward 命令；UI 不计算资格。选择先复核资格和事务容量，再执行原子 Evolution Transform 并提交
事务。无候选时不打开空页面，直接提交定义提供的确定性 Fallback；关键奖励没有普通 Skip。

G1.7 只实现适配器和测试来源，不创建显化宝匣、Boss/精英实际消费者或 fallback 奖励内容。G2.3 通过
已有 Reward Operation 把真实来源接入；G2.6 才提供选择页面。

### 完整 G1 Pack Gate

Pack 保持 0.5.0 / Schema 6 / 94 definitions，不因纯运行时代码虚增内容版本。所有 94 个作者定义和
Baked Catalog 必须位于 Placeholder 目录，并带 Pack、`placeholder`、`development-only` Addressables
标签；英文/简中名称和描述均非空。相同输入两次 Pack Build 的 Content/Catalog Hash 必须相同。

## 兼容与影响

- 不改变 Assembly 引用方向、Content/Save Schema、Tick 频率或既有枚举值；GameState 只在末尾追加。
- `Game.Simulation` 追加 RewardChoice 命令/快照/结果及 Progression 属性；`Game.Application` 追加投影、
  页面状态和选择命令。旧构造函数和成员不删除。
- 候选计算只在低频奖励请求发生；固定 Tick 未请求时只读取一个布尔状态，不分配。
- Fallback 只记录稳定输出 ID；永久写入仍必须由 Application/Profile 事务完成。

## 被拒绝的方案

- 解锁 Evolution Offer 后复用 Level-up：会允许 Reroll/Banish/Skip 丢失关键奖励并污染 Offer RNG。
- UI 直接读取六显化并筛选：复制 BuildState 真值，重放不可验证。
- 没候选仍显示空页面：无法恢复且违反确定性 fallback 契约。
- 在 G1.7 创建宝匣/Boss 内容：越过 G2.2/G2.3 单里程碑边界。

## 测试与迁移

1. 保留 G1.6 API Hash，导出规范 diff；只允许 Simulation 32、Application 9 条追加，零删除。
2. 覆盖资格、锁定 Offer、Reward/Offer RNG 隔离、非法选择、fallback、重放、暂停/恢复和普通升级回归。
3. 双次 Bake/Pack Build、双语/Addressables 标签、完整 EditMode/PlayMode/Validation/API/性能短测。
4. 生成 Windows x64 Development Build；Release 和正式资产门禁保持未执行。
