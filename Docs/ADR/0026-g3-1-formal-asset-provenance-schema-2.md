# ADR 0026：G3.1 正式资产 Provenance Schema 2 与实际 Release 输入门禁

- 状态：Accepted
- 日期：2026-08-09
- 决策人：依据用户连续完成 Demo、全权自行决策与逐步骤提交 Push 授权
- 关联里程碑：G3.1、M13、M15
- 关联 CR：无需新增；执行已批准 G0.4 正式资产生产计划
- 承接：ADR 0004、0011、0012、0025

## 背景

G2.8 已通过程序化 Placeholder 垂直切片门禁。旧 `AssetProvenanceValidator` 只扫描 AI 目录，Schema 1
只强制少量来源、权利和输出 Hash 字段；FirstParty 程序化正式资产以及带 `release` 标签的实际
Addressables 输入可能绕过同等级审查。G0.4 已明确要求 G3.1 在导入任何正式视觉前关闭该缺口。

同时，`Docs/CODEX_WORKFLOW.md` 的“不得生成正式美术”来自框架阶段，但当前已进入批准的 G3.1 生产包。
继续按未限定阶段的旧句执行会与当前里程碑直接冲突。

## 决策

- “不得生成正式美术”限定为 G0—G2 框架阶段；G3 只允许按批准 Manifest 逐批生产并完成审查。
- 正式资产记录升级为 Schema 2，强制 Owner、全部相对路径、工具/模型或脚本版本、生成时间、操作者、
  Prompt/规格、Seed、显式引用列表、人工编辑、条款 URL/生成日复核、允许平台/用途、披露分类、三类
  审核人与时间以及 `approved-for-release`。
- `source/` 与 `prompt.txt` 使用 `sourceSha256`；`working/`、`final/` 使用 `outputSha256`。文件变化会使
  Hash 门禁失败，必须重新执行后续审核。
- Project Validation 主动扫描 AI 与 FirstParty 正式目录，并验证所有带 `release` 或类别 Release 标签的
  实际 Addressables 文件。
- `source/`、`working/`、`prompt.txt` 与 `provenance.json` 永不允许成为正式 Addressables 输入；
  `visual.release` 固定进入 `QinglanDemo-Visual`，并同时带 `pack.qinglan_demo` 与 `release`。
- 第三方字体的固定版本、许可文件和派生字体专用校验仍归 G3.3；本 ADR 不提前导入字体。

## 兼容与回滚

不改变 Runtime Content Schema 6、存档版本、程序集依赖、30 Hz 模拟或稳定 ContentId。旧 Schema 1
测试样例仍可被解析，但不能作为新的 G3 正式批准记录。回滚只能移除尚未发布的正式批次；不得降低
Schema 或跳过 Hash/审核来恢复构建。

## 测试

EditMode 覆盖 FirstParty 缺记录、Schema 2 正向记录、source/prompt/final Hash、商业审核缺失、Hash
漂移、source/working 发布阻断以及视觉 Group/Label 路由。完成实现后运行全量 EditMode、PlayMode、
Project Validation；实际视觉批次还必须分别执行导入设置和可读性检查。
