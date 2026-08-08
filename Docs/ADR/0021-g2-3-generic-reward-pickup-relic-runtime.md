# ADR 0021：G2.3 通用 Reward、Pickup 与 Relic 运行时

- 状态：Accepted
- 日期：2026-08-08
- 决策人：依据用户当前连续 Demo 开发与自行决策授权
- 关联里程碑：G2.3、M06、M10
- 关联 CR：CR-2026-007、CR-2026-008

## 背景

Schema 6 已有 Reward/Pickup/Relic 定义和 10 类 RewardOperation，G1.7 也已有受控 Evolution Choice，
但 G1.1 的 `RewardRuntime` 仍只有固定事务提交骨架。普通敌人、G1.5 精英词缀和 G2.2 Boss 无法产生
真实非 XP 拾取物；Relic 没有三槽库存、独立随机候选、输出安装或满槽回退；永久奖励也没有可交给
Application 的局内结果投影。

## 决策

### 单一通用奖励所有者

`RewardRuntime` 在 Simulation 中成为 RewardDefinition 的唯一执行者。它在 Run 装配期绑定全部
Reward、Pickup、Relic 和输出 Definition，固定 Tick 只使用加载期索引、固定数组和预分配查询缓冲。
运行时不分支任何 `qinglan.*` ID；具体规则由 RewardOperation、Definition 引用和通用 ContentTag 表达。

奖励事务继续使用 `RunId + SourceStableId + Sequence`。事务在直接结算、选择待定、地面拾取存活和最终
提交四个阶段均保持唯一；活动地面拾取的事务进入预分配 Dictionary，重复来源不能在拾取前再次生成，
领取或 Cleanup 后移除活动记录。已提交事务重放不产生效果。

### 来源、拾取与 Cleanup

- 普通敌人死亡按其 `LootReward` 使用 Reward RNG 选择一个即时 Pickup；XP 仍由原 M6 路径负责。
- 精英词缀死亡把 Affix DeathReward 生成为地面异相灵核，同一 Actor＋Affix 使用稳定 Sequence。
- 折枝死亡生成显化宝匣；听风死亡生成固定首通奖励，事务沿用 BossDefinition ID。
- Map Objective 的 Reward 输出由 RewardResolution 捕获，不在 Map Runtime 内直接修改构筑或 Profile。
- 所有地面实体只在 Cleanup 创建/删除；PickupSystem 只更新吸附、可领取条件和删除命令。

六类即时 Pickup 共用生命周期和吸附逻辑。纯治疗 Pickup 在满血且没有可接收的过量治疗护盾时不消耗；
聚灵葫芦可强制吸附普通 Pickup，但明确排除选择、唯一和 Objective 锁定来源。

### Relic 与选择

战斗 Relic 固定三槽，G2.3 内容的 `MaximumLevel=1`，因此重复获取被禁止。精英灵核从当前合格集合用
独立 Reward RNG 无放回选择最多三个候选；Reward RNG 与普通升级 Offer RNG 分离。候选在请求时冻结，
Application 只投影稳定 ContentId 并暂停 SimulationClock；提交时再次检查槽位、前置和互斥。

Relic 输出只能安装通用 Skill、Passive、Trait 或继续排队 Reward。断剑穗/听风木芯/旧庭残钟复用
M4 Skill Runtime；风脉铜片/药圃种囊/无字试剑牌复用 Build Trait。过量治疗屏障、Boss 专属增伤和承伤
风险由 `relic.rule.*` 标签驱动，不出现具体 Relic ID 分支。三槽已满或无候选时固定发放灵砂 fallback。

### 显化、唯一与永久输出边界

显化宝匣复用 G1.7 的 `RewardChoiceRuntime`，只在当前构筑存在合格 Evolution 时暂停并提供最多三个候选；
空池不滚随机，直接执行固定 fallback。听风首通按固定顺序输出唯一青岚山河脉印与灵砂，不参与随机。

Currency、Unlock、Unique 和 Story 只写入不可变 `RewardResultEntry` 局内增量，不在 Simulation 写 Profile、
文件或平台。`SetOwnedUniqueRewards` 是 Run 装配时的只读 Profile 快照输入；G2.5 负责原子合并和持久化。

### 容量与公开 API

既有公开 `RewardRuntime(int transactionCapacity = 128)` 签名与语义保留。Demo 组合根使用内部明确容量：
整局 4096 个已提交事务、同帧/活动结构 512，足以覆盖现有 12 分钟 2,571 次死亡基准及 320 并发上限。
显式压力测试可在测试友元中提供更大双容量。容量不足记录 `RejectedCapacity`，不静默扩容关键请求表。

`Game.Simulation` 公开 API 追加 65 条规范签名、删除 0，新增范围为奖励结果、Relic 库存/选择、Pickup
快照、运行时只读诊断和 `RewardResolutionSystem.Execute`。Core、Content Runtime、Application 与
Platform Abstractions 的签名逐字节不变。

## 兼容与影响

- 不改变程序集引用方向、30 Hz Tick、Content Schema 6、Save Schema 3 或稳定 ID 规则。
- 旧 XP Pickup、旧 M2—M6 Pipeline 和既有 RewardRuntime 构造调用保持原行为。
- Pack 升为 0.8.0 / Schema 6 / 150 definitions；没有引入第三方包或正式资产。
- 高频 Pickup 扫描不使用 LINQ、反射、字符串格式化或临时托管集合。
- `RunSession` 复用既有 RewardChoice 页面状态处理 Relic Choice；G2.6 才实现实际 UI/输入页面。

## 被拒绝的方案

- 每种灵物或奇物各写一个系统/MonoBehaviour：重复随机、生命周期和幂等逻辑。
- 把 Profile 写入放进 Simulation：破坏程序集方向和原子保存边界。
- 用普通 Offer RNG 生成 Relic：会让升级操作改变精英奖励结果。
- 满槽后覆盖旧 Relic 或允许重复升级：没有锁定替换 UX，且会使三槽契约不稳定。
- 在 DeathSystem 直接执行奖励：结构创建越过 Cleanup，并使重放和地面生命周期不可审计。

## 迁移、回滚与测试

迁移：Run 初始化时由 Build Catalog 向 RewardRuntime 提供完整 Definition 视图和 Player；Death、Affix、
Boss、Map 只排队稳定奖励请求；Application 在 Tick 边界投影并提交选择。Profile 所有权快照可为空，
G2.5 接入真实 Profile 后不改变 Simulation 协议。

回滚：移除 0.8.0 新内容和 Boss RewardId 后，RewardResolution 回到空操作，旧 XP/Progression 流继续；
新增公开 API 保留以维持兼容，不回退为具体内容分支。已形成的永久增量不得重复提交或猜测撤销。

测试覆盖六种即时操作、满血不消费、葫芦排除、活动/已提交事务重放、Relic RNG 隔离、三槽 fallback、
三个通用输出族、Boss 专属风险、显化空池、首通唯一快照、Application 暂停/恢复、默认容量，以及
5,000 Pickup 连续 120 次扫描 0 B。
