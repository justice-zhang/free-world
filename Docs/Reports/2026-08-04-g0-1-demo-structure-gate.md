# Codex 结果报告

- 任务：关闭《剑起青岚》Demo G0.1 文档集门禁并建立连续实现基线
- 里程碑：G0.1 Demo Structure Gate
- 分支：`codex/qinglan-demo-implementation`
- Git Commit：本报告所在提交
- 日期：2026-08-04

## 1. 实现范围

复核已提交的 24 份 `Docs/DemoDevelopment/` 文档、V2.0 源文件 Hash、G0—G3 路线、11 项 CR、
10 条 DOD 和 20 条需求追踪；修正 2026-08-02 报告中的分支、提交、文件字节数、源文件状态和
ContentId 统计口径。没有修改运行时代码、Schema、公共 API、场景、内容资产或正式资源。

G0.1 原始文档提交为 `31afc1f`，原始报告提交为 `1d6ceba`，连续实现分支基线为 `91961da`。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Docs/Reports/2026-08-02-qinglan-demo-development-structure.md` | 校正已提交后的审计事实和具体 ID 统计口径 |
| `Docs/Reports/2026-08-04-g0-1-demo-structure-gate.md` | G0.1 当前环境门禁报告 |
| `Docs/EXECUTION_LOG.md` | 增加 Qinglan Demo G0.1 执行记录 |
| `Docs/KNOWN_ISSUES.md` | 登记 G0.2、分支集成和正式资产前置风险 |

## 3. 关键架构决定

- `qinglan.*` 统计只计算完整、具体、canonical 的稳定 ID；Pack 子串、命名空间前缀和含
  `<placeholder>` 的模板不计作 ID。当前严格结果为 148 个具体 ID。
- 所有 Demo ID 继续保持 `DRAFT`；G0.1 不创建发布兼容性承诺。
- 按用户当前明确指令，后续工作包在一个长生命周期实现分支中依次提交并 Push；仍保持 G0→G3
  依赖门禁，但本任务不自动合并 `main`、创建 PR 或标签。

## 4. 实际执行的命令

```text
git status --short --branch
git fetch --prune --tags git@github.com:free-world-team/free-world.git +refs/heads/*:refs/remotes/origin/*
git switch -c codex/qinglan-demo-implementation
$env:UNITY_PATH='C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe'
.\Scripts\test.ps1 -Platform All -ProjectPath E:\ai\free-world -ResultsDirectory TestResults\QinglanDemo\Baseline
.\Scripts\validate.ps1 -ProjectPath E:\ai\free-world -LogPath TestResults\QinglanDemo\Baseline\validation.log
Get-FileHash 'Docs/Game Proposal/《剑起青岚》游戏系统总纲_V2.0.md' -Algorithm SHA256
PowerShell G0.1 文档文件/标题/围栏/链接/ID/CR/DOD/需求综合门禁
git diff --check
```

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| 编译 | PASS | EditMode、PlayMode 与 Validation 均完成 Unity 脚本编译，退出码 0 |
| EditMode | PASS | `TestResults/QinglanDemo/Baseline/editmode.xml`：187/187，0 failed，0 skipped |
| PlayMode | PASS | `TestResults/QinglanDemo/Baseline/playmode.xml`：9/9，0 failed，0 skipped |
| 内容/工程验证 | PASS | `TestResults/QinglanDemo/Baseline/validation.log`：`[Project Validation] PASS` |
| G0.1 文档门禁 | PASS | 24 文件、148 个具体 ID、11 CR、10 DOD、20 需求；`G0_1_DOC_GATE=PASS` |
| 源文件完整性 | PASS | SHA-256 `F5F9B0EC38E2FB4BD890C4EFE8E170FB7B9D06CFD8C89F55F9A868E4724359BC` |
| 构建 | NOT RUN | 本工作包只收口文档与审计记录，不修改构建输入 |
| 性能/Soak | NOT RUN | 本工作包不修改模拟或表现运行时 |

首次复核沿用了历史报告的 150 个 ID 预期，并用过宽正则得到 164 个命名空间式 token，门禁按
`FAIL` 处理。定位后改为只匹配完整具体 ID，确认 148 个并修正历史报告；没有把失败尝试改写为通过。

## 6. 构建产物

- 配置：不适用
- 路径：无
- 文件 Hash：无
- Build Manifest：无

## 7. 未执行项目

Windows Development/Release Build、Player Smoke、性能基准和 Soak 为 `NOT RUN`，原因是 G0.1
仅涉及 Markdown 和审计记录。它们不能被描述为 Demo 运行或发布门禁通过。

## 8. 已知限制和风险

- CR-01—CR-11 仍未决，冻结核心程序集不得在 G0.2/G0.3 完成前修改。
- 设计文档尚未进入 `main`；当前连续实现分支以已推送设计分支为父提交。
- 正式美术、音频、字体和本地化尚无生产、权利或目标硬件证据。

## 9. 未完成项

- G0.2 CR-01—CR-11 决策包。
- G0.3 Schema/API/Save/ADR/迁移与测试契约。
- G0.4 资产生产与 provenance 计划。
- G1—G3 的代码、内容、资产与发布实现。

## 10. 下一步前置条件

- G0.1 本地文档门禁、当前 Unity 基线和远端 source SHA 均已核验。
- 下一步只能进入 G0.2，逐项给出 `ACCEPTED`、`REJECTED`、`DEFERRED` 或 `SPLIT`；不得提前修改核心程序集。

## 11. 结论

`COMPLETE`

G0.1 文档工作包已在当前环境完成审计收口；Demo 实现状态仍为 `NOT RUN`。
