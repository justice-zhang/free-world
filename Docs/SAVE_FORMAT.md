# 存档格式与迁移规范

## 1. M8 文件拆分

| 文件 | 文档种类 | 生命周期 |
|---|---|---|
| `settings.json` | Settings | 语言、输入覆盖和可访问性设置改变后保存 |
| `profile.json` | Profile | 完成本局后保存长期档案和统计 |
| `run_recovery.json` | RunRecovery | 开局时创建，正常结算后删除 |

三个文档独立编码和迁移；任一文件损坏不会让其他文档失效。本地目录在 Player 中为
`Application.persistentDataPath/Saves`；Editor 测试使用进程隔离的临时目录。测试可用
`AZURESWORD_SAVE_ROOT` 显式覆盖。

## 2. 按文档种类独立版本的纯数据模型

所有文档包含各自 `schemaVersion` 和 `gameVersion: "0.1.0"`。当前 Settings/Profile/RunRecovery
分别为 3/3/2。Profile 和 RunRecovery 只保存：

- canonical `ContentId` 字符串；
- `packId` 与语义化 Pack 版本；
- 数值、布尔、UTC 字符串、输入 Action 名和 Control Path 等纯数据。

禁止保存 `RuntimeContentIndex`、运行时数组下标、`EntityHandle`、Unity Object、Scene、Prefab 或
平台 SDK 对象。Run Seed 以无符号十进制字符串编码，避免 JSON 数值精度差异。

## 3. 校验信封

文件外层是 UTF-8 JSON 信封：

```json
{
  "documentKind": "profile",
  "schemaVersion": 2,
  "checksumSha256": "<payload bytes SHA-256 lowercase hex>",
  "payloadBase64": "<UTF-8 payload JSON>"
}
```

加载顺序为：解析信封 → 核验种类和 Schema → Base64 解码 → 固定时间式比较 SHA-256 → 执行
显式迁移链 → 构造不可变应用模型。失败通过本地化 Key 的 `SaveDiagnostic` 返回，不抛出到 UI。

## 4. 原子写入与恢复

`LocalFileSaveStorage` 在同一目录执行：

1. 写入 `<slot>.tmp`，异步 flush；
2. 若主文件存在，复制为 `<slot>.bak`；
3. 用文件系统 replace 原子替换主文件；首写使用同卷 move；
4. 成功或取消后清理临时文件。

取消、I/O 失败或进程在 replace 前中断都不破坏上一主版本。主文件校验或格式失败时尝试
`.bak`；备份有效则返回 `SaveReadSource.Backup` 和 `save.warning.recovered_backup`，两者均无效则
保留明确失败码。

## 5. 迁移

`SaveMigrationRegistry` 只接受连续、单向、每次恰好加一的迁移。M8 为 Settings、Profile 和
RunRecovery 都注册 `v1 -> v2` 固定样本；Settings 与 Profile 另有独立 `v2 -> v3`，不得修改已有迁移语义或
在加载器中堆积隐式条件分支。高于当前 Schema 的文档返回 `UnsupportedSchema`。

## 6. 内容缺失

- Profile 的缺失解锁项和局外升级保留原始 ID，并返回 `save.warning.missing_unlock`。
- RunRecovery 的角色、地图或已拥有内容缺失时，返回 `MissingContent` 和具体 `ContentId`，拒绝
  恢复，不静默映射到其他内容。
- 所有诊断向上提供本地化 Key，调用方决定提示或恢复 UI。

## 7. 云同步边界

本地原子文件始终是真值来源。平台层只上传、下载和比较 Revision；本地较新可上传、远端较新可
下载，双方偏离最后同步校验和时必须要求用户选择，禁止静默覆盖。Steam Cloud 不实现或替代
`ISaveStorage`。

## 8. Qinglan Demo Profile Schema 3

ADR 0015 批准三个物理文档按 kind 独立演进：

| 文档 | 当前版本 | G0.3 变化 |
|---|---:|---|
| Settings | 3 | 字体缩放、色觉、四路音量和字幕 |
| Profile | 3 | Loadout、首通、唯一奖励、故事、藏品和幂等事务 |
| RunRecovery | 2 | 仍只作启动/未完成标记，不支持 Continue |

`SaveSchema.CurrentVersion` 保留一个弃用周期并表示当前最高版本 3；新代码必须使用
`GetCurrentVersion(SaveDocumentKind)` 或对应 kind 常量，不能再假设三个文档版本相同。

Profile 3 在 v2 既有字段后追加：

```text
activeMetaLoadoutIds[]
firstClearMapIds[]
claimedUniqueRewardIds[]
completedStoryIds[]
collectedCollectibleIds[]
committedTransactionIds[]
```

所有集合写入前按 canonical ContentId 排序/去重。`activeMetaLoadoutIds` 的 6 普通节点＋1 终端＋2
嵌片容量、互斥、前置和解锁由 Meta Coordinator 对 Schema 6 定义验证；存档不保存槽位运行时索引。
缺失内容保留原 ID 并返回本地化警告，当前 Run 使用安全默认 Loadout，未经用户确认不覆盖原文件。

Profile v2→v3 迁移逐字段保留 v2 值，新集合全部初始化为空；不得从统计、解锁或货币猜测首通和唯一
领取。v1 文件必须连续执行 v1→v2→v3。固定 Fixture 同时覆盖主文件、备份、取消、损坏、未来版本、
重复迁移和写入中断。

结算事务 ID 是 canonical ContentId，由 RunId、结果规则和稳定序号确定。内存合并先检查
`committedTransactionIds`；已存在返回 `AlreadyCommitted`，不重复改变任何集合/货币/统计。只有原子
Profile 写成功后才删除 Recovery、发布平台事件或显示“已保存”。

若 Profile 已原子写入但 Recovery 删除失败，当前 Profile 立即以持久化事务为真值，Result 页面保持
可重试；重试返回 `AlreadyCommitted` 并只补删 Recovery，不再合并奖励。为避免平台重复，进程重启后的
AlreadyCommitted 不重新发布完成事件；同一进程可在补删成功后发布一次尚未发布的待处理事件。

CR-2026-015 完整局内恢复延期。检测到 `run_recovery.json` 时只显示本地化提示，用户明确开始新局
后清理；不得显示 Continue，也不得把标记内容提交为胜利或首通。

## 9. Qinglan Demo Settings Schema 3

ADR 0024 批准 Settings 3 在 v2 字段后追加：

```text
fontScale: 1.0 / 1.25 / 1.5
colorVision: standard / protanopia / deuteranopia / tritanopia / highContrast
masterVolume / musicVolume / ambienceVolume / effectsVolume: [0,1]
subtitlesEnabled: bool
```

wire 中 `colorVision` 保存稳定枚举整数 0—4；所有浮点必须有限且在范围内。v2→v3 默认字体 1.0、
标准色觉、四路音量 1.0、字幕开启，并逐字段保留 v2 的 Locale、死区、震动、屏幕震动、闪光、伤害
数字、自动瞄准和重绑。旧 Settings 构造函数继续产生相同默认新字段；v1 文件连续执行
v1→v2→v3。UI 修改设置后仍通过 `SettingsChanged` 事件和 `SaveCoordinator` 原子保存，不直接写文件。
