# M14 存档、本地化与平台边界

## 1. 存档文件

沿用 Settings/Profile/RunRecovery 三文件、SHA-256 信封、temp flush、backup、同卷原子替换和连续迁移。

| 文件 | Demo 新需求 | Schema 影响 |
|---|---|---|
| settings.json | 字体大小、色觉、主/音乐/环境/音效音量、字幕 | 可能升级 Save Schema |
| profile.json | 行脉/嵌片 Loadout、设施、故事、藏品、首通、唯一奖励 | CR-09/可能升级 |
| run_recovery.json | 目标/Boss/构筑/奖励/随机流完整恢复 | CR-11，可延期 |

局中恢复若未完整定义，Demo 可只提供“检测到未完成记录并开始新局/清理”的明确路径，不能宣称继续本局。

## 2. Profile 增量

Run 结束只提交不可变 `ProfileDelta`：货币增量、解锁 ID、等级、Loadout 修正、首通/唯一 ID 和统计。
合并前验证 ContentRegistry，缺失可选收藏保留 ID 并告警；缺必需角色/地图/Meta 输出时拒绝装配新局。

幂等键建议：`runId + outcomeSequence`，而非 RuntimeIndex。重复回调/保存重试不能重复奖励。

## 3. 迁移

任何新版本必须提供：旧 v1/v2 固定 JSON、逐版本 migrator、主/备份校验、未知字段策略、缺内容策略、
降级/回滚说明。不能直接把 CurrentVersion 改大并依赖默认值静默迁移。

分期边界：G1.1 交付三文档独立版本、Profile 3 纯数据、Codec、固定 Fixture 和连续 Migrator；G2.5
交付 ContentRegistry/Meta Validator、主备份恢复、取消/中断处理、不可变 `ProfileDelta` 合并和稳定事务
ID 幂等提交。基础迁移提前不表示局外结算流程已经完成。

## 4. 本地化

建议 Collection：

- `UI`：通用页面和设置；
- `QinglanContent`：角色/武器/心诀/敌人/奖励/地图；
- `QinglanNarrative`：故事、地标、Boss/目标文本。

Locale：`zh-Hans`、`en`、Pseudo。Presenter/内容只传 Key；语言正文不进入存档或 Simulation。

## 5. 文本规范

- 玩家可见术语使用山河脉系、风脉台、衡律、古誓、脉印等世界内语言；
- “节点/系统/权限/网络”等只用于开发文档，不直接写进叙事 UI；
- 升级卡说明行为变化和条件，不只显示百分比；
- Boss/目标提示短句在 1080p、三种 UI Scale 下不裁切；
- 正式字体有商业嵌入许可和中英 fallback，不依赖目标机系统字体。

## 6. 平台

Demo Release 必须在 `NullPlatformFacade` 下完整离线运行。Steam 成就/云/Rich Presence 可延后；如接入，
只消费 ApplicationEvent，不进入 Simulation。云冲突分叉必须要求用户选择，本地文件始终是真值。

建议成就 ID 仅作为后续草案，不在 G2 锁定：首次听风、三风脉台、三构筑等。平台不可用不阻止奖励、
存档或主线。

## 7. 安全与隐私

存档只含本地 Profile ID 和玩法数据，不记录无关个人信息。错误日志不输出用户路径中的敏感信息、
平台令牌或完整云 payload；Hash 用于完整性而非加密声明。

## 8. 测试与验收

| 类型 | 必须覆盖 |
|---|---|
| EditMode | 三文档 round-trip、原子取消、损坏主文件→备份、迁移链 |
| EditMode | 首通/唯一奖励幂等、缺内容、Loadout 容量/互斥 |
| Localization | 所有 Key 双语非空、Pseudo、字体字符、术语扫描 |
| PlayMode | 设置保存、语言即时切换、胜败/首通/重复结算 |
| Platform | Null 五子服务完整流程；Steam 不可用不报错 |
| Release | 用户目录可写、重启加载、Manifest 记录 Save/Content Schema |

退出条件：永久进度无 RuntimeIndex/Unity Object，存档可迁移可恢复，正式双语和字体通过 Release 门禁。
