# M06 灵物、奇物与奖励

## 1. 目标

把现有仅承载 XP float 的 Pickup 扩展为可验证、可池化、可结算的通用 Reward 流，支持即时灵物、
精英三选一奇物、显化宝匣、灵砂、藏品、故事和山河脉印。依赖 CR-04/05。

## 2. 奖励分类

| 类别 | 触发 | 生命周期 | 真值 |
|---|---|---|---|
| XP | 普通敌人死亡 | 地面 Pickup | ProgressionRuntime |
| 即时灵物 | 敌人/事件/地标 | 地面 Pickup，接触消费 | RewardRuntime→Combat/Skill 请求 |
| 战场奇物 | 精英死亡 | 异相灵核，选择后销毁 | BuildState/RelicInventory |
| 显化宝匣 | 中段 Boss | 受控 Evolution 选择 | BuildState |
| 灵砂 | 战斗/探索 | Run-local 计数，结算合并 | RunResult/Profile |
| 唯一藏品/故事 | 地标/首通 | 幂等 ContentId | Profile |
| 山河脉印 | 首次最终 Boss 胜利 | 固定、唯一 | Profile/主线进度 |

## 3. 建议 Reward 操作码

以下为 CR 评审输入：Heal、ApplyStatus、DamageArea、CollectEligiblePickups、GrantRelicChoice、
GrantEvolutionChoice、AddCurrency、UnlockContent、GrantUnique、TriggerStory。每项只携带纯值和稳定 ID；
不能把委托、Unity Object 或任意脚本引用写入 Definition。

## 4. 六个即时灵物

| 灵物 | 行为 | 硬边界 |
|---|---|---|
| 青木露 | 恢复固定值＋最大生命比例上限 | 只走受控 Heal；满血可不消费或转小护盾，需锁定 |
| 定界符 | 对范围普通敌人定身/强减速 | Boss 转为短减速；不冻结阶段 |
| 震霄雷玉 | 范围高伤＋击退 | 不击杀无敌阶段、不跳过 Boss 阶段 |
| 聚灵葫芦 | 吸取合格地面 Pickup | 排除唯一藏品、未完成地标奖励和未解锁宝匣 |
| 护心玉 | 短时免伤/伤害屏障 | 依赖 CR-10；不可与 Pause 计时混淆 |
| 乘风羽 | 短时移速/穿行宽容 | 不穿地图硬边界，不由 View 改 Transform |

## 5. 六个战场奇物

| 奇物 | 通用输出 | 约束 |
|---|---|---|
| 断剑穗 | 有冷却的 Repeat/SecondarySkill | 不递归重复自身；ProcDepth 上限 |
| 风脉铜片 | 移动资源获取/阈值 Modifier | 复用 CR-01；不按陆青野 ID |
| 药圃种囊 | 治疗溢出转护盾/生长资源 | 单次/总容量上限 |
| 听风木芯 | Targeting 数量/重定向规则 | 不每 Tick 全量扫描 |
| 旧庭残钟 | 定时范围控制 | Boss 递减；VFX/Audio 明确前摇 |
| 无字试剑牌 | Boss 标签增伤＋承伤/限制代价 | 风险必须在 UI 明示；不能影响非 Boss |

奇物最多 3 槽。重复奇物是否升级到 3 级或禁止重复在 G0 数值规则中锁定；无论哪种都必须由内容
验证防止无限叠层。

## 6. 精英与 Boss 奖励

```text
Elite death
→ create one AfflictedCore reward request
→ Cleanup creates choice pickup/prompt
→ SimulationClock pause
→ generate 3 eligible relic offers from independent Reward stream
→ select one, apply to BuildState, record history
→ resume

MidBoss death
→ guaranteed ManifestationChest
→ eligible Evolution choices or deterministic fallback
```

同一 Elite/Boss 的死亡事件用 EntityHandle Generation＋奖励规则 ID 去重。Catch-up Tick 批次中也只能
创建一次。掉落请求和结构创建必须分离。

## 7. 唯一和首通规则

- `Unique` 奖励先检查 Profile 快照，再生成确定性 fallback；
- 首通脉印、指定藏品、第三篇故事固定发放，不 Roll Luck；
- 结算合并幂等，保存失败重试不会重复；
- 放弃/失败只提交已明确可保留的拾取/发现；
- UI 动画完成不是奖励真值，关闭动画不能丢奖励。

## 8. 表现与可访问性

不同奖励类别至少用形状＋颜色＋音效三重区分。精英灵核/宝匣出现时暂停前先确保危险伤害结算完成；
选择界面必须显示当前奇物槽、冲突、等级和行为变化。闪光强度降低时仍保留轮廓和音频。

## 9. 测试与验收

| 类型 | 必须覆盖 |
|---|---|
| EditMode | 六灵物效果边界、六奇物冷却/上限、奖励去重 |
| EditMode | 聚灵葫芦排除唯一物；Luck 不影响固定奖励 |
| Determinism | Elite/宝匣候选流独立于 Combat/Offer |
| PlayMode | 拾取、三选一、暂停/恢复、满槽/空池 fallback |
| Save | 首通/重复/失败/重试幂等 |
| Performance | 目标 Pickup 数、池命中、0 高频分配 |

退出条件：十二个局内道具都通过通用 Reward/Build 契约工作，固定进度奖励无法被随机、吸取或重复提交破坏。
