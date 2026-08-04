# M01 应用流程与单局生命周期

## 1. 目标与边界

把现有 `Bootstrap → MainMenu → CharacterSelect → MapSelect → Loading → InRun → RunResult` 扩展为
可发布 Demo 闭环，并保持 Application 只协调、Simulation 持有玩法真值。

不在本模块实现候选过滤、伤害、地图目标、Boss 规则或局外节点效果；它们只通过接口和不可变快照接入。

## 2. 页面与状态

```text
Bootstrap
├─ ContentError
└─ Title/MainMenu
   ├─ Settings
   ├─ Continue/Recovery（若 G3 启用）
   └─ CharacterSelect
      → MapSelect
      → LoadoutReview
      → Loading
      → InRun/RunHUD
          ├─ LevelUpChoice
          ├─ RelicChoice
          ├─ Pause/Settings
          ├─ StoryOverlay（Simulation 暂停）
          └─ Result
              → Hub
                  ├─ VeinInquiry
                  ├─ ScrollPavilion
                  ├─ ArtifactPavilion
                  ├─ MyriadPhenomena
                  └─ StartAgain
```

现有 `GameState` 不含 Title、Hub、RelicChoice、StoryOverlay、LoadoutReview。增加公开状态会触发 API
Freeze；优先评估用 `UiPageId` 的内部/兼容扩展承载低频子页。若公开契约必须变化，走 ADR。

## 3. Run 状态机

| 状态 | 入口动作 | 可接受命令 | 退出条件 |
|---|---|---|---|
| Preparing | 验证角色、地图、Pack、Meta Loadout | Cancel | Catalog/Scene/Run 装配成功 |
| Active | 启动 SimulationClock，写 RunRecovery | Move、Pause | 升级、奖励选择、胜负、错误 |
| UpgradePaused | 冻结 SimulationClock | Select/Reroll/Banish/Skip | 有效选择提交 |
| RewardPaused | 冻结 SimulationClock | SelectRelic | 有效奖励提交 |
| UserPaused | 冻结 SimulationClock | Resume/Settings/Quit | 恢复或确认退出 |
| StoryPaused | 冻结 SimulationClock | Advance/Skip | 叙事段结束 |
| Ending | 冻结输入和 SimulationClock | 无 | RunResult 与局外增量生成 |
| Committing | 原子保存 Profile/Recovery | RetrySave | 保存成功或用户确认未保存退出 |
| Result | 展示不可变结果 | Continue | Hub |
| Disposed | 释放 Scene/句柄/池/订阅 | 无 | 回到 Hub/MainMenu |

## 4. Run Outcome

建议不可变结果包含：

- outcome：Victory/Defeat/Abandoned/RecoveryRejected；
- Seed、Tick、模拟时长、角色、地图、难度；
- 等级、武器/心诀/奇物、显化、击杀、精英、Boss、拾取、经验；
- 三风脉台、事件、地标、故事、藏品完成集合；
- 灵砂增量、脉印/首通奖励、解锁和统计增量；
- 决策/Spawn/目标/Boss 阶段校验值；
- 内容 Pack 版本和 Hash。

运行时索引、EntityHandle 和 Unity Object 不得进入结果或存档。

## 5. 结算事务

```text
Freeze Run
→ Build immutable RunResult
→ Validate reward eligibility and idempotency
→ Merge Profile delta in memory
→ Save profile atomically
→ Delete/close recovery only after profile success
→ Publish RunCompleted
→ Release run-owned resources
→ Show Result/Hub
```

首通奖励以 `mapId + rewardId` 幂等；重复收到同一提交不增加脉印、故事或唯一藏品。

## 6. 错误处理

- Pack/引用/Hash/本地化失败：`ContentError`，不创建 Run；
- Scene/Addressables 失败：释放已取得句柄，返回选择页并保留结构化错误；
- 保存失败：保留内存结果和有效备份，提供重试；
- Recovery 缺必需内容：明确拒绝，不降级成新局或胜利；
- 控制器断开：暂停并切换输入提示，不修改模拟；
- 无可用升级候选：使用现有自动 Skip 规则并记录诊断。

## 7. 实施任务

1. 扩充 UI-safe 页面模型，审查是否触及公开 API。
2. 增加 Reward/Story/Hub 的低频协调接口。
3. 构建 Demo Run Factory，按稳定 ID 装配，不硬编码数值。
4. 实现 Outcome→Profile 幂等合并。
5. 实现 Run 资源 Owner 与统一 Dispose。
6. 添加完整页面流和失败路径 PlayMode。

## 8. 测试与验收

| 检查 | 断言 |
|---|---|
| EditMode | 状态非法跳转拒绝；结果不可变；奖励合并幂等 |
| PlayMode | 键鼠和手柄各完成标题→再次出发 |
| PlayMode | Upgrade/Reward/Pause 只停 SimulationClock |
| PlayMode | 胜利、失败、放弃和保存失败页面正确 |
| Lifecycle | 返回 Hub 后无 View、Pool Owner、Input 订阅、Addressables 句柄 |
| Build | Null Platform 下 Windows Player 完整运行 |

退出条件：DOD-01、DOD-02 的功能证据 PASS；正式表现和 Release 性能由 G3 完成。
