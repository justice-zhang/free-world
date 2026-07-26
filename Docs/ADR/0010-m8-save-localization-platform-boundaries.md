# ADR 0010：M8 存档、本地化与平台边界

- 状态：Accepted
- 日期：2026-07-26
- 决策人：依据当前用户 M8 指令

## 背景

M7 已完成本地 UI 与一局流程，但没有可靠文件持久化、真实语言表或可替换的平台子服务。M8 必须
在不让 Unity Object、RuntimeIndex 或 Steam SDK 进入纯应用/模拟层的前提下建立这些长期边界。

## 决策

### 存档

- Settings、Profile、RunRecovery 是 `Game.Application` 的三个独立不可变纯数据模型，当前 Schema
  为 2；只保存稳定 ContentId、Pack 版本和纯值。
- `ISaveStorage` 属于 Application，`LocalFileSaveStorage` 和显式 JsonUtility DTO/校验信封属于
  Infrastructure。写入采用同目录 temp、flush、上一版 backup 和原子 replace。
- SHA-256 保护 payload；加载优先主文件、再尝试备份。迁移通过按文档种类注册的连续单向链执行，
  M8 固定覆盖 v1→v2。
- 本地文件始终是真值；云平台只同步文件和 Revision。

### 本地化

- Unity Localization 的 `UI` Collection 是用户文字真值，包含 `en`、`zh-Hans` 和 Pseudo Locale。
- Presenter、内容和诊断继续只传稳定 Key；`Game.UI` 的适配器负责解析，设置只保存 Locale Code。
- Project Validation 阻止缺 Locale、缺表、缺 Key 或空翻译；Windows Placeholder 运行时使用系统
  CJK 动态字体候选，不提交第三方字体文件。

### 平台

- `IPlatformFacade` 明确拆分 Achievements、Stats、Cloud、RichPresence、Identity。Null 实现支持无
  SDK 启动和完整本地流程。
- 应用事件是存档与平台更新的唯一触发边界；Simulation 不引用或调用平台服务。
- 云冲突依据 Local、Remote 与最后同步校验和分类；分叉必须要求用户选择。

### Assembly 影响

- `Game.UI` 增加 Unity Localization/ResourceManager；`Game.Infrastructure` 增加 Unity Localization；
  `Game.Platform.Null` 增加 Game.Core 以实现稳定平台 ID。
- 方向保持单向，无程序集循环；Core、Simulation、Application、Platform Abstractions 和 Null 继续
  `noEngineReferences: true`。

## 被拒绝的方案

- BinaryFormatter 或直接序列化运行时对象：不稳定、不安全且无法显式迁移。
- 覆盖主文件后才生成备份：中断会丢失最后有效版本。
- 云文件作为唯一存档：无平台/离线时无法完成本地游戏。
- UI 直接保存翻译正文：语言切换和 Key 兼容性无法治理。
- Simulation 直接调用成就/统计：会污染确定性模拟并锁定 SDK。
- 在 M8 引入真实 Steam SDK：超出当前里程碑并增加未经审批的第三方依赖。

## 后果

无 Steam 环境可以加载语言、保存设置、创建/删除局内恢复并保存档案；损坏主文件可从校验通过的
备份恢复。代价是每次 Schema、平台后端或 Assembly 方向变化都必须同步迁移、ADR 和门禁。真实
Steam I/O、用户冲突 UI 和更频繁的局中恢复快照留给对应后续任务。
