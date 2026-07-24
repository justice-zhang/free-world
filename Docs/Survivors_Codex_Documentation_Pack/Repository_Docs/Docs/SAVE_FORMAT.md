# 存档格式与迁移规范

## 1. 文件拆分

> settings.json  
> profile.json  
> run_recovery.json

设置、长期档案和局内恢复分离，避免任一文件损坏导致全部数据丢失。

## 2. Profile 示例

> {  
> "schemaVersion": 3,  
> "gameVersion": "0.1.0",  
> "contentPacks": {  
> "com.studio.base": "0.1.0"  
> },  
> "profileId": "...",  
> "currencies": {},  
> "unlockedContentIds": \[\],  
> "metaUpgrades": {},  
> "statistics": {},  
> "lastWriteUtc": "..."  
> }

## 3. 强制要求

- Schema 版本化

- 原子写入

- 临时文件

- 前一版本备份

- 校验和

- 显式迁移链

- 缺失内容容错

- 保存稳定 ContentId

- 不保存运行时索引

- 不保存 Unity Object 引用

- 本地保存不依赖 Steam

## 4. 接口

> public interface ISaveStorage  
> {  
> ValueTask\<SaveReadResult\> ReadAsync(  
> string slot,  
> CancellationToken cancellationToken);  
>   
> ValueTask\<SaveWriteResult\> WriteAtomicAsync(  
> string slot,  
> ReadOnlyMemory\<byte\> data,  
> CancellationToken cancellationToken);  
> }

## 5. 迁移

每次 Schema 变化添加一个单向迁移：

> v1 -\> v2  
> v2 -\> v3

禁止在加载器中堆积无法测试的条件分支。所有历史迁移必须有固定样本测试。

## 6. 内容缺失

当存档引用已删除内容：

- 保留原始 ID 供诊断。

- 使用明确的 Missing Content 结果，不抛出未处理异常。

- 对解锁项可忽略但记录警告。

- 对装备或局内恢复，按文档化规则替换、移除或终止恢复。

- 不静默映射到错误内容。

## 7. 云同步边界

Steam Cloud 是同步层，不是核心保存实现。平台层负责上传、下载、冲突检测和用户选择；ISaveStorage 仍然以本地原子文件为真值来源。
