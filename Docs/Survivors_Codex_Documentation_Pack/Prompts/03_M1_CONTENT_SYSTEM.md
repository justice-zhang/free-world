# M1：核心类型与内容系统

## 目标

实现稳定内容 ID、内容包、作者数据、烘焙运行时定义、注册表和构建前验证。不要实现战斗。

## 前置条件

- M0 已验收并打标签。

- asmdef、Bootstrap、测试脚本可用。

## 开始前

1\. 阅读 AGENTS.md、Docs/ARCHITECTURE.md、Docs/CODEX_WORKFLOW.md。

2\. 阅读本提示词列出的相关文档。

3\. 检查当前 Git diff，确保没有未说明的改动。

4\. 运行现有测试并记录基线。

5\. 输出不超过 10 条的实现计划，然后开始修改。

## 必须交付

1\. 在 Core 实现：

- ContentId

- ContentTag

- RuntimeContentIndex

- ContentVersion

- 明确的 Result/Error 类型

2\. ContentId 必须：规范化、验证字符、保持原始字符串、可比较、可序列化；不只保存 Hash。

3\. 实现内容包 Manifest、依赖模型、版本检查和拓扑排序。

4\. 实现最小作者数据：

- ContentPackAuthoring

- CharacterAuthoring

- SkillAuthoring

- EnemyAuthoring

- MapAuthoring

本里程碑字段可以最小化，但要为后续扩展保留清晰边界。

5\. 实现对应纯运行时定义和 Baked Catalog。运行时定义不得含 Unity Object。

6\. 实现 ContentBaker：从作者数据生成可验证的运行时 Catalog，并输出内容 Hash。

7\. 实现 ContentRegistry：

- 按稳定 ID 查找

- 加载后分配 RuntimeContentIndex

- 拒绝重复 ID

- 记录来源 Pack

- 不允许静默覆盖

8\. 实现 ContentValidator：ID 格式、重复 ID、缺失引用、依赖缺失、依赖循环、版本不兼容。

9\. 创建一个最小测试内容包，只使用程序化占位资源。

10\. 在 Bootstrap 中加载测试内容包并输出摘要，不进入战斗。

## 必须测试

- ContentId 有效/无效样本、大小写和序列化测试。

- Hash 碰撞不影响稳定 ID 比较。

- 内容包拓扑排序正确。

- 缺失依赖和循环依赖失败。

- 重复 ID 失败且指出两个来源。

- 作者数据可烘焙为不含 Unity Object 的运行时定义。

- 同一输入产生相同内容 Hash。

- Registry 运行时索引稳定于同一次加载顺序。

- Bootstrap 能加载测试包并输出条目数量。

## 验收标准

- 新内容定义不需要修改 Registry 的硬编码列表。

- Baked Catalog 中没有 ScriptableObject、GameObject、Sprite 或 AudioClip 引用。

- 所有错误有可定位的 ID、Pack 和作者资产路径。

- 内容验证可以从命令行运行。

- M0 测试仍全部通过。

## 禁止

- 不实现实体、战斗、技能执行或刷怪。

- 不使用反射扫描自动发现所有类型。

- 不使用 enum 固定所有内容 ID。

- 不允许重复 ID 后按最后加载覆盖。

- 不把 Addressables AssetReference 放入 Simulation Runtime Definition。

## 文档更新

- 更新 Docs/CONTENT_SCHEMA.md。

- 创建或更新内容包 ADR。

- 记录测试包结构和新增内容流程。

## 完成报告

使用 Templates/CODEX_RESULT_REPORT.md，必须包含：

1\. 修改文件清单。

2\. 关键设计和理由。

3\. 实际执行的命令。

4\. 编译、测试、验证和构建的真实结果。

5\. 未完成事项和已知限制。

6\. 下一里程碑前置条件。
