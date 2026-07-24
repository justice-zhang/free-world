# 类幸存者游戏 Codex 交付文档包

版本：1.0  
日期：2026-07-24

本包用于从零搭建一个可商业化、可持续扩展的 Unity 类幸存者游戏框架。它不包含任何参考开源项目的美术、音效、Prefab、Scene、动画或品牌资源；框架阶段只使用程序化占位资源。

## 总入口

- `00_文档包下载说明与完整执行顺序.md`：汇总文档包说明、文件入口及从人工初始化到候选版本审计的完整执行顺序。

## 目录

- `Human_Docs/`：供负责人审阅、决策和项目管理使用的 DOCX 文档。
- `Repository_Docs/`：可直接复制到新 Unity 仓库根目录的 Markdown、CSV 与治理文件。
- `Prompts/`：Codex 总控提示词、M0-M10 分步提示词、审查与修复提示词。
- `Templates/`：项目变量、ADR、里程碑验收、资产来源和构建清单模板。

## 推荐使用方式

1. 阅读 `Human_Docs/01_类幸存者游戏完整落地方案与技术架构.docx`，确认技术路线和范围。
2. 将 `Repository_Docs/` 中的内容复制到干净的 Unity 仓库根目录。
3. 填写 `Templates/PROJECT_VARIABLES.md`，确定项目名、命名空间、Unity 版本和目标硬件。
4. 按 `Human_Docs/02_Codex完整执行顺序与里程碑验收.docx` 执行。
5. 先向 Codex 提供 `Prompts/00_MASTER_CONTROL.md`，再逐个提供 M0-M10 提示词。
6. 每个里程碑完成后运行 `Prompts/13_MILESTONE_REVIEW_GATE.md`，验收通过后再合并和进入下一步。
7. 正式内容生产前，必须满足 `Repository_Docs/Docs/DEFINITION_OF_DONE.md`。

## 关键原则

- 新建干净工程，不 fork 任何参考游戏工程。
- 模拟层与 Unity 表现层分离。
- 内容使用稳定 ID、内容包、作者数据与运行时烘焙数据。
- 新角色、新技能、新构筑、新地图优先通过配置新增。
- 新底层机制通过注册式模块扩展，不修改已有内容。
- 所有正式 AI 资源必须保留来源、生成和人工处理记录。
- 未通过测试、验证和构建，不得声明里程碑完成。

## 版本说明

文档将 Unity 固定为“团队选定并写入 `ProjectVersion.txt` 的 Unity 6 LTS 补丁版”，避免文档随编辑器补丁更新失效。项目创建后，应在 ADR 0001 中填入准确版本。
