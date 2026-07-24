# M8：版本化存档、本地化与平台边界

## 目标

实现可靠本地存档、迁移、本地化和 Null 平台适配，为后续 Steam 集成建立稳定接口。

## 前置条件

- M7 完整本地流程已验收。

- Run Result 和内容 ID 稳定。

## 开始前

1\. 阅读 AGENTS.md、Docs/ARCHITECTURE.md、Docs/CODEX_WORKFLOW.md。

2\. 阅读本提示词列出的相关文档。

3\. 检查当前 Git diff，确保没有未说明的改动。

4\. 运行现有测试并记录基线。

5\. 输出不超过 10 条的实现计划，然后开始修改。

## 必须交付

1\. 实现 settings.json、profile.json、run_recovery.json 的独立模型。

2\. 实现 ISaveStorage、本地文件实现、原子写入、临时文件、备份、校验和和取消。

3\. 实现 Schema Version、迁移注册表和至少 v1-\>v2 的测试迁移样本。

4\. 存档只保存稳定 ContentId、Pack 版本和纯数据，不保存 RuntimeIndex 或 Unity Object。

5\. 实现缺失内容处理和诊断结果。

6\. 接入 Localization：简体中文、英文、伪本地化。所有用户可见文字迁移为 Key。

7\. 实现 IPlatformFacade 子服务接口和 NullPlatformFacade：Achievements、Stats、Cloud、RichPresence、Identity。

8\. 实现云同步状态模型和冲突策略接口，但不引入真实 Steam SDK。

9\. 成就和平台统计通过应用事件触发，不允许 Simulation 直接调用平台服务。

10\. 增加设置保存、档案保存、局内恢复和本地化流程测试。

## 必须测试

- 原子写入中断不破坏上一版本。

- 校验和失败能恢复备份或返回明确错误。

- v1 样本迁移到 v2。

- 缺失 ContentId 不导致未处理异常。

- RuntimeIndex 不进入 JSON。

- 简中、英文和伪本地化加载。

- UI 无硬编码用户文字。

- NullPlatform 下可完整运行。

- Simulation Assembly 不引用平台接口实现。

- 云冲突模型的本地较新、远端较新和分叉情况。

## 验收标准

- 旧存档可迁移。

- 存档失败有用户可理解的应用层结果。

- 无 Steam 环境仍可启动、保存和完成游戏。

- 平台服务可以替换而不修改 Simulation。

## 禁止

- 不集成真实 Steam SDK。

- 不把云端文件当作唯一存档。

- 不使用 BinaryFormatter。

- 不把本地化字符串直接存入内容定义作为唯一真值。

## 文档更新

- 更新 Docs/SAVE_FORMAT.md、本地化 Key 规范和 STEAM_INTEGRATION_BOUNDARY.md。

## 完成报告

使用 Templates/CODEX_RESULT_REPORT.md，必须包含：

1\. 修改文件清单。

2\. 关键设计和理由。

3\. 实际执行的命令。

4\. 编译、测试、验证和构建的真实结果。

5\. 未完成事项和已知限制。

6\. 下一里程碑前置条件。
