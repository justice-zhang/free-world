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

## 2. Schema 2 纯数据模型

所有文档包含 `schemaVersion: 2` 和 `gameVersion: "0.1.0"`。Profile 和 RunRecovery 只保存：

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
RunRecovery 都注册 `v1 -> v2` 固定样本；未来变化必须新增 `v2 -> v3`，不得修改已有迁移语义或
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
