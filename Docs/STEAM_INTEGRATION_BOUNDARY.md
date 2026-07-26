# Steam 集成边界

## 目标

Steam 是可替换平台适配器，不是游戏核心依赖。

## M8 稳定接口

- Achievements

- Stats

- Cloud Sync

- Rich Presence

- User Identity

- Optional Leaderboards

`IPlatformFacade` 组合 `IAchievementService`、`IPlatformStatsService`、`ICloudSyncService`、
`IRichPresenceService` 和 `IUserIdentityService`。操作返回 `Success`、`Unavailable`、`Failed` 或
`Cancelled` 及本地化诊断 Key；无 SDK 时 `NullPlatformFacade` 完整实现同一边界。

## 规则

- M8 只实现接口、Null 实现和数据模型，不引入真实 Steam SDK。

- 真实 SDK 集成在框架冻结后单独执行。

- 本地存档始终可用。

- 云冲突必须有明确策略，不静默覆盖更新文件。

- 成就触发来自应用事件或统计，不允许战斗系统直接调用 SDK。

- Steam Assembly 不得被 Simulation、Content.Runtime 或 Core 依赖。

## 应用事件

设置、本局开始和本局完成由 `ApplicationEventStream` 发布。存档服务消费设置/局事件；平台路由只
消费应用层完成事件并更新统计或成就。Simulation 不引用平台 Assembly，也不直接触发 SDK。

## Cloud Revision 与冲突

每个云文件 Revision 包含存在标记、SHA-256、UTC、设备 ID 和单调 Generation。冲突策略比较
Local、Remote 与 `LastSynchronizedChecksum`：

| 状态 | 默认决定 |
|---|---|
| 校验和相同 | NoAction |
| 仅本地 / 远端仍为同步基线 | UploadLocal |
| 仅远端 / 本地仍为同步基线 | DownloadRemote |
| 双方都偏离基线或无可证明基线 | RequireUserChoice |

M8 只交付状态模型、保守策略和 Null 服务，不下载 SDK、不联网、不实现远端文件作为唯一副本。
