# ADR 0015：Qinglan Demo Profile Save Schema 3 与局外事务

- 状态：Accepted
- 日期：2026-08-04
- 决策人：依据用户当前连续 Demo 开发指令
- 关联里程碑：G0.3、G2.4、G2.5、G3.2、G3.6
- 取代：无

## 背景

CR-2026-012 需要保存行脉/嵌片 Loadout、首通、唯一奖励、故事/藏品和幂等事务。Profile Schema 2
只有解锁、升级、货币和统计，无法区分“已解锁”与“已装配”，也无法证明结算重试不会重复发放。
Settings、Profile、RunRecovery 物理独立，但现有代码用一个共享 `SaveSchema.CurrentVersion`；继续共享会
迫使未变的 Settings/Recovery 做无意义迁移。

## 决策

### 独立当前版本

新增 `SettingsCurrentVersion = 2`、`ProfileCurrentVersion = 3`、
`RunRecoveryCurrentVersion = 2` 和 `GetCurrentVersion(SaveDocumentKind)`。旧
`SaveSchema.CurrentVersion` 保留一个弃用周期，值为当前最高版本 3；内部不得再用它选择某个文档的
迁移目标。Build Manifest 分别报告三种版本。

### Profile 3 纯数据

Profile 3 在 v2 字段后追加：

```text
activeMetaLoadoutIds[]
firstClearMapIds[]
claimedUniqueRewardIds[]
completedStoryIds[]
collectedCollectibleIds[]
committedTransactionIds[]
```

全部为 canonical ContentId，排序/去重后写入；不得保存 RuntimeContentIndex、EntityHandle、场景、
Unity Object 或平台对象。Loadout 由 Meta 定义验证 6 普通节点＋1 终端＋2 嵌片、互斥和解锁状态；
缺失内容保留稳定 ID 并产生本地化警告，运行时使用安全降级快照。

### v2→v3 与结算

迁移保留所有 v2 字段，新集合初始化为空；不会根据统计或解锁项猜测首通/唯一领取。迁移仍在校验
信封后执行，写入继续 temp/flush/backup/atomic replace。Run 结束先冻结并生成不可变结果，再按稳定
事务 ID 合并：同 ID 已存在即返回 `AlreadyCommitted`，不得重复货币、唯一物或平台输出。Profile
保存成功后才清理 Recovery 并展示“已保存”。

CR-2026-015 继续延期：RunRecovery 仍为 Schema 2 启动标记，不提供 Continue，不提交胜利。

## 备选方案

### 方案 A：把新状态编码进现有 unlocked/counter 字段

- 优点：不升级版本。
- 缺点：类型语义丢失，Loadout/幂等无法严格验证，Key 易冲突。
- 未采用原因：不能提供可靠迁移和审计。

### 方案 B：把三个文档全部升级到 3

- 优点：继续共享一个版本常量。
- 缺点：Settings/Recovery 需要无意义迁移，违背独立文档设计。
- 未采用原因：扩大风险而无产品收益。

## 影响

### 正面影响

- 永久进度、Loadout 和结算重试拥有明确真值与幂等边界。
- 三类存档真正独立演进。
- 旧 Profile 可无猜测地连续迁移。

### 负面影响与成本

- `Game.Application`、Infrastructure Codec、Editor Manifest 和测试公开 API 需扩展。
- `CurrentVersion` 的旧消费者必须迁移到按 kind 查询。

### 对兼容性的影响

- Content Schema：Meta 引用依赖 ADR 0013 Schema 6。
- Save Schema：Settings 2、Profile 3、RunRecovery 2；Profile 1→2→3 连续读取。
- API：追加版本/模型/事务结果，旧成员保留并标记弃用。
- 性能：仅启动、选择和结算低频执行，不进入固定 Tick。
- 构建：Manifest/Release 报告三种版本；迁移 Fixture 是门禁。
- 资产：无影响。

## 实施与迁移

1. G1.1 可先加入版本查询与纯模型；G2.5 实现 Codec、Migration、Meta Validator 和 Coordinator。
2. 固定 v2 Envelope/Payload Fixture，执行 v1→2→3 与 v2→3，验证重复迁移结果一致。
3. 写 v3 前保留有效 v2 主/备份；失败不覆盖旧文件。
4. 所有 Outcome 使用稳定事务 ID 合并，保存成功后再触发平台/页面完成事件。

## 测试与验收证据

- 测试：v1→2→3、v2→3、主/备份、取消/中断、未知 ID、Loadout、首通/失败/重复提交幂等。
- 构建：G2.5 PlayMode/Development；G3.6 Release Player 重启验证。
- 性能：大集合编解码/原子写低频预算；不得出现在 Simulation 性能采样中。
- 日志或产物位置：实施后写入 `TestResults/QinglanDemo/G2.5/` 与 Save Fixture 目录。

## 回滚方案

保留 v2 备份和 v2→v3 迁移。若 v3 写入实现回滚，程序只能只读/隔离 v3 并提示升级，不能把 v3
静默降写成 v2 或重复发放事务；修复后从有效备份或 v3 再迁移。
