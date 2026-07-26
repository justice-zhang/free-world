# Change Request：M8 版本化存档 Schema 2

- 编号：CR-2026-002
- 状态：Implemented
- 提交日期：2026-07-26
- 提交人：Codex
- 目标里程碑：M8
- 关联 ADR：ADR 0010

## 1. 变更摘要

首次建立 Settings、Profile、RunRecovery 三种独立存档文档、Schema 2、校验信封、显式迁移链和
缺失内容结果，使 M7 完整本地流程可可靠持久化，并为后续云同步提供本地真值。

## 2. 现有模块为何不足

M7 只有内存中的设置、构筑和结算，没有存档模型、文件事务或版本迁移。直接保存运行时 Registry
索引、Unity 对象或整个 RunSession 会绑定加载顺序和场景生命周期，无法跨版本可靠恢复。

## 3. 提议方案

- Application 新增不可变纯数据模型、存储/Codec 合约、协调器、诊断和迁移注册表。
- Infrastructure 实现同目录原子文件写入和 SHA-256 JsonUtility 信封。
- 所有内容引用保存 canonical ContentId 和 Pack 版本；局内恢复缺内容时明确失败，Profile 缺解锁
  时保留 ID 并告警。
- 设置变化、本局开始和完成通过应用事件触发三个文件的独立生命周期。

## 4. 影响分析

| 领域 | 影响 |
|---|---|
| Assembly 依赖 | Infrastructure 新增 Unity Localization；Null 新增 Core；方向不反转 |
| Content Schema | 不改变内容 Schema 5 |
| Save Schema | 首次建立 Schema 2，并提供三类 v1→v2 固定迁移 |
| Addressables | Unity Localization 自动登记 Locale 和 String Table 组 |
| 性能 | 文件 I/O 只发生在低频应用事件，不进入固定 Tick 热路径 |
| 测试 | 原子中断、备份校验、迁移、缺失内容、稳定 ID 和全流程 |
| 平台 | 本地存档独立于 Null/Steam；云只作为同步层 |
| 资产与许可 | 仅 Unity 序列化语言表和系统字体运行时选择，无外部资产 |

## 5. 迁移与回滚

- v1 固定样本按文档种类迁移到 v2；未来必须追加连续迁移。
- 新版存档高于当前 Schema 时拒绝加载，不降级写回。
- 回滚代码前应保留用户文件；旧程序不能假定能读取 v2，不自动删除或覆盖。

## 6. 验收标准

- [x] 三种文档独立且只含稳定纯数据
- [x] 原子写入、取消、备份和 SHA-256 有自动测试
- [x] 三类 v1→v2 迁移已注册，Settings 固定样本有测试
- [x] 缺失 ContentId 返回诊断而非未处理异常
- [x] Null 平台完整流程可保存
- [x] 文档、ADR 和门禁同步

## 7. 审批

- 技术负责人：由当前用户 M8 指令授权实施
- 内容负责人：只涉及 Placeholder 内容引用
- 制作人：由当前用户 M8 指令授权实施
- 结论：Accepted / Implemented
