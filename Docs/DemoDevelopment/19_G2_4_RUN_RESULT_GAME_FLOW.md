# 19 G2.4 RunResult 与 Demo 游戏流程

## 1. 工作包目标

G2.4 交付 M01 的无 UI 运行流程和不可变结算真值：从已验证 Pack 选择青岚角色与旧演武场，装配一局，
处理暂停/选择/胜负，冻结 G2.1—G2.3 数据，进入结算、据点并再次出发。本包不写 Profile、不清
Recovery、不发布平台完成、不实现实际页面/输入或 Windows Player；分别由 G2.5、G2.6、G2.8 关闭。

## 2. 模块结构

| 模块 | 所有权 | 输入 | 输出 |
|---|---|---|---|
| `GameApplication` | 已加载内容与 Pack 元数据 | Baked Catalog | 依赖排序的 `RunPackSnapshot[]` |
| `QinglanDemoRunFactory` | 一局组合根 | Descriptor、ContentRegistry | World、Player、Starting Skill/Mechanic、Handle |
| `RunSession` | SimulationClock 与局内命令 | elapsed、暂停、选择、结束原因 | 不可变 `RunResult` |
| `RunResultBuilder` | 结束边界低频复制 | World、Descriptor | Build、探索、统计、Delta、Checksum |
| `DemoRunCoordinator` | 页面/运行生命周期 | 页面命令、Factory | Stage、Session、LatestResult、ContentError |
| G2.5（未实现） | Profile/Recovery/平台事务 | `LatestResult` | 保存成功、事件与重试状态 |

Simulation 不引用 Application；Infrastructure 只装配稳定内容。Coordinator 不读取 Entity Store，结果
Builder 不访问文件、平台、Localization 或 Unity Object。

## 3. RunDescriptor

| 字段 | 来源 | 约束 |
|---|---|---|
| RunId / Seed | 新局命令 | RunId 非 0；Reward 事务使用 RunId，随机使用 Seed |
| CharacterId | `qinglan.character.lu_qingye` | Registry 中必须存在 |
| MapId | `qinglan.map.old_court` | 必须是可执行 M5+ Map |
| DifficultyId | `base.difficulty.normal` | 稳定 ID，不持久化 RuntimeIndex |
| RequiredBossDefeats | Encounter BossRules | 当前内容为 2 |
| VictoryBossId | 最后声明的 BossRule | 当前为 `qinglan.boss.tingfeng` |
| LoadedPacks | Registry 依赖顺序 | PackId、Version、64 字符十六进制 SHA-256 字符串 |

Descriptor 在 `Preparing` 前冻结。调用者原数组后续修改不能影响本局或结果。

## 4. Outcome 与胜利条件

| Outcome | 入口 | 永久增量 |
|---|---|---|
| Victory | Boss 计数达到 2 且听风奖励事务提交 | 保留全部已合法产生的 Delta |
| Defeat | Player Actor 不再存活 | 保留全部已合法产生的 Delta |
| Abandoned | 玩家明确结束本局 | 保留全部已合法产生的 Delta |
| RecoveryRejected | 启动记录不完整/不可恢复 | 固定空 Delta，不创建 World、不算胜利 |

最终 Boss 事务为 `(RunId, qinglan.boss.tingfeng, 0)`。只满足击杀计数不能胜利，避免结果页早于首通奖励
进入；Choice 尚未提交时 `RunSession` 仍停在 RewardPaused。

## 5. 不可变结果字段

`RunResult` 保存 Descriptor、Outcome、Tick/时长/等级、Build、探索、统计和永久 Delta。Build 复制 Skill、
Passive、Relic 的稳定 ID/等级及已应用 Evolution ID；探索复制已完成目标/事件、发现/领取地标。G2.3
结果条目按类型合并：Currency 同 ID 求和，Unlock/Unique/Story 排序去重。

三个诊断 Checksum 用于测试和问题定位：Spawn 直接取 EnemyRuntime，Objective 混合全部目标/事件/地标
状态，Boss 混合运行完成数与 Boss 击杀数。它们不是持久化主键，也不代替稳定 ID。

## 6. 流程状态与命令

| 当前 Stage | 合法命令 | 下一 Stage |
|---|---|---|
| Title | ShowCharacterSelect / RejectRecovery | CharacterSelect / Ending |
| CharacterSelect | ShowMapSelect | MapSelect |
| MapSelect | BeginRun | Preparing |
| Preparing | Tick | Active / ContentError |
| Active | Tick / Pause / EndRun | Active、Choice、UserPaused、Ending |
| UpgradePaused | SelectUpgrade / SkipUpgrade / EndRun | Active / Ending |
| RewardPaused | SelectReward / EndRun | Active / Ending |
| UserPaused | Resume / EndRun | Active / Ending |
| Ending | Tick | Result |
| Result | ContinueToHub | Hub |
| Hub | StartAgain / ReturnToTitle | CharacterSelect / Title |

非法命令不改变 State/Stage。只有 Active 推进 SimulationClock；页面、用户暂停、升级、奖励和 Ending 均
返回 0 Tick。ContentError 使用 `ui.content_error.run_assembly`，实际本地化页面由 G2.6 实现。

## 7. 装配与释放

Factory 从 Registry 构建 Skill/Enemy/Build Catalog、有限地图、Encounter、Qinglan Runtime Hub 和完整
Demo Pipeline；按角色内容创建 Player，安装起始御风剑和乘风机制。任何部分失败都返回结构化 Error，
不得把半构造 Handle 交给 Coordinator。

`IRunSessionHandle.Dispose` 幂等执行集中 Cleanup，先消费待处理命令，再为 Projectile、Area、Pickup、Actor
排队删除并再次 Cleanup。Result 已深复制，因此 Handle 释放后仍可展示。G2.6 接入 Scene/View 时必须把
它们纳入同一 Handle 或上层 Owner，不建立全局 Service Locator。

## 8. G2.5 边界

Result 阶段的 `HasUncommittedResult=true` 表示结果只在内存冻结。G2.4 明确不会：

- 调用 `ProfileCommitService` 或 Storage；
- 删除/更新 RunRecovery；
- 发布 `ApplicationEvent.RunCompleted`；
- 解锁平台成就、统计或云同步；
- 显示“已保存”。

G2.5 必须使用 `Delta.TransactionId` 原子合并 Profile 3，成功保存后清 Recovery，再发布完成事件。重复提交
返回 AlreadyCommitted，不能重复货币、唯一奖励或平台输出。

G2.4 为证明完整页面往返，`StartAgain/ReturnToTitle` 会清除上一局内存结果。G2.5 接入后必须在结果成功
提交前阻止或接管这两个命令；不能让未提交结果在真实玩家路径中被清除。

## 9. API 与兼容

ADR 0022 接受 Simulation 6 条、Application 95 条公开签名追加，删除 0。原 M6 `RunSession` 构造、
RunResult 原字段和 RunEndReason 数值 1—3 保留；旧 GameState 不增加 Title/Hub。Content Schema 6、Profile
Schema 3、Pack 0.8.0 / 150 definitions 均不变。

## 10. 验证矩阵

| 检查 | 覆盖 |
|---|---|
| Focused EditMode | Descriptor/Pack、真实 Factory、聚合、四 Outcome、自动胜利、不可变性、释放 |
| Focused PlayMode | Title→Preparing→Active→Pause→Ending→Result→Hub→StartAgain |
| 全量 EditMode/PlayMode | M0—G2.3 兼容回归 |
| API Freeze | Simulation +6、Application +95、0 删除，其他程序集逐字节不变 |
| 12 分钟 Headless | Encounter/Reward 热路径和确定性未回归 |
| 性能短测 | 600 Enemy / 1200 Projectile / 2000 Pickup，结束逻辑不进入 Tick 热路径 |
| Windows Build | `NOT RUN`；G2.8 完整垂直切片门禁 |
