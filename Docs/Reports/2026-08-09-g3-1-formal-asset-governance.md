# Codex 结果报告

- 任务：建立 G3.1 正式视觉生产前置 provenance 与 Release 输入门禁
- 里程碑：G3.1 前置治理步骤（不代表 G3.1 全部完成）
- 分支：`codex/qinglan-demo-implementation`
- Git Commit：本报告所在提交
- 日期：2026-08-09

## 1. 实现范围

完成正式视觉生产前的治理步骤：解除旧工作流与当前批准 G3.1 的阶段性冲突；将记录升级为
Provenance Schema 2；主动扫描 AI、FirstParty 和实际 Release Addressables 输入；阻断 source/working/
Prompt/provenance 发布以及错误视觉 Group/Label；新增对应负向与正向测试。

本步骤未生成或导入任何 ART 资产，未开始 G3.2 音频、G3.3 字体/正文、G3.4 平衡、G3.5 目标硬件
性能或 G3.6 Release。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Assets/Game/Editor/AssetProvenanceValidator.cs` | Schema 2、AI/FirstParty 扫描、源/输出 Hash 与 Release 输入路由门禁 |
| `Assets/Game/Editor/ProjectGovernanceValidator.cs` | 将实际 Addressables Entry 的 Group/Label/Provenance 纳入 Project Validation |
| `Assets/Tests/EditMode/QinglanG31AssetGovernanceTests.cs` | 4 项 G3.1 正式资产治理测试 |
| `Assets/GameAssets/FirstParty/` | 建立批准的第一方正式资产根目录 |
| `Docs/ADR/0026-g3-1-formal-asset-provenance-schema-2.md` | 接受 Schema 2 与 Release 输入门禁长期决策 |
| `Docs/DemoDevelopment/24_G3_1_FORMAL_VISUAL_ASSETS.md` | 固定 27 个 ART 批次顺序、单批完成定义与最终门禁 |
| `Docs/CODEX_WORKFLOW.md`、`Docs/AI_ASSET_PIPELINE.md` | 收窄阶段性限制并同步实际治理规则 |
| `Docs/DemoDevelopment/09_G0_4_ASSET_PRODUCTION_PLAN.md`、模板与路线文档 | 同步 Schema 2 和已完成前置状态 |

## 3. 关键架构决定

- ADR 0026：Runtime Content Schema 6、存档、程序集与模拟 Tick 不变；正式资产治理记录升级为 Schema 2。
- `source/` 与 Prompt 使用 `sourceSha256`，`working/` 与 `final/` 使用 `outputSha256`。
- 所有实际 Release 输入必须有批准记录；`visual.release` 固定路由到 `QinglanDemo-Visual` 并带三个标签。
- G3 生成权限只适用于已批准 Manifest 批次，不构成清单外正式内容授权。

## 4. 实际执行的命令

```text
$env:UNITY_PATH='C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe'; .\scripts\test.ps1 -Platform All -ResultsDirectory TestResults/G31Baseline
$env:UNITY_PATH='C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe'; .\scripts\validate.ps1 -LogPath TestResults/G31Baseline/validation.log
$env:UNITY_PATH='C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe'; .\scripts\test.ps1 -Platform EditMode -ResultsDirectory TestResults/G31Governance
$env:UNITY_PATH='C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe'; .\scripts\test.ps1 -Platform PlayMode -ResultsDirectory TestResults/G31Governance
$env:UNITY_PATH='C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe'; .\scripts\validate.ps1 -LogPath TestResults/G31Governance/validation.log
git diff --check
```

修改前首次沙箱内 Unity 启动因许可证客户端签名校验未产生 XML，判为 `NOT RUN`；随后允许访问本机
许可证服务后，同一基线命令实际通过。

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| 编译 | PASS | Unity 6000.3.20f1 成功导入并执行测试程序集 |
| 修改前 EditMode | PASS | `TestResults/G31Baseline/editmode.xml`，292/292 |
| 修改前 PlayMode | PASS | `TestResults/G31Baseline/playmode.xml`，17/17 |
| 修改前内容验证 | PASS | `TestResults/G31Baseline/validation.log` |
| 修改后 EditMode | PASS | `TestResults/G31Governance/editmode.xml`，296/296 |
| 修改后 PlayMode | PASS | `TestResults/G31Governance/playmode.xml`，17/17 |
| 修改后内容验证/API Freeze | PASS | `TestResults/G31Governance/validation.log` |
| 构建 | NOT RUN | 本步骤未改变 Runtime、Scene、正式 Addressables Entry 或构建输入 |
| 性能/Soak | NOT RUN | 本步骤只修改 Editor 治理低频路径，不修改模拟或表现运行时 |

## 6. 构建产物

- 配置：NOT RUN
- 路径：无
- 文件 Hash：无
- Build Manifest：无

## 7. 未执行项目

Windows Development/Release Build、GPU、可读性截图和正式资产内存检查均未执行；本步骤没有正式视觉
输入，这些检查分别随 ART 批次与 G3.1 最终门禁执行。

## 8. 已知限制和风险

- G3.3 尚需增加第三方字体固定版本、许可证文件和派生文件专用校验。
- 当前尚无正式 ART 文件；门禁通过不代表任何视觉批次已经完成。

## 9. 未完成项

- 按 Manifest 顺序完成 ART-CHAR-001 至 ART-UI-005 共 27 个独立批次。
- 完成 G3.1 正式视觉集成与最终 Build/视觉审查。

## 10. 下一步前置条件

- 本治理步骤提交并 Push 后，从 ART-CHAR-001 开始，不并行、不跳序。

## 11. 结论

`COMPLETE`（仅指 G3.1 前置治理步骤；G3.1 总里程碑仍为 `IN PROGRESS`）。
