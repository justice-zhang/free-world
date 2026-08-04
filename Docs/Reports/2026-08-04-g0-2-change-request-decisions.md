# Codex 结果报告：Qinglan Demo G0.2 Change Request 决策

- 任务：评审 Demo CR-01—CR-11 并形成正式 Change Request
- 里程碑：Qinglan Demo G0.2
- 分支：`codex/qinglan-demo-implementation`
- Git Commit：本报告所在提交
- 日期：2026-08-04

## 1. 实现范围

完成 11 项 Demo CR 的正式决策、编号映射、边界和 G0.3 输入顺序。CR-01—09 接受；CR-10
因公共 Stat 与 Damage Resolution 风险不同拆为两项并接受；CR-11 完整 Run Recovery 延期。
本包不修改运行时、Schema、存档、Package、资产或冻结 API，不提前实施任何 CR。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Docs/ChangeRequests/CR-2026-004-*.md`—`CR-2026-015-*.md` | 12 份正式 CR，包含方案、影响、迁移、回滚、验收和审批 |
| `Docs/DemoDevelopment/07_CHANGE_REQUEST_DECISIONS.md` | G0.2 决策矩阵、CR-10 拆分和 CR-11 延期边界 |
| `Docs/DemoDevelopment/05_SCHEMA_GAP_AND_CHANGE_REQUESTS.md` | 回填最终决定 |
| `Docs/DemoDevelopment/06_REQUIREMENTS_TRACEABILITY.md` | 追踪正式 CR 映射与尚未实现状态 |
| `Docs/DemoDevelopment/README.md` | 登记决策控制文档 |
| `Docs/EXECUTION_LOG.md` | 登记 G0.2 结果与下一门禁 |
| `Docs/KNOWN_ISSUES.md` | 关闭决策阻塞，登记 G0.3 与 Recovery 边界 |

## 3. 关键架构决定

- 接受只授权 G0.3 契约设计，不等于授权直接改冻结代码。
- Content Schema 6、Profile Save Schema 3、Pipeline、随机流、Cleanup、迁移和 API Freeze 必须在实现前一起定稿。
- 公共 Stat 扩展与伤害通道/屏障/受击冷却分开评审，避免把两类兼容风险绑成一次变更。
- Demo 不提供“继续本局”；不完整 Run 只能检测、提示并在明确开始新局后清理。
- G0.2 只形成 CR，不新增 ADR；对应 ADR 在 G0.3 统一完成。

## 4. 实际执行的命令

```text
git status --short
git diff --check
Get-ChildItem / Get-Content（核对模板、既有 CR 与决策文档）
PowerShell CR 结构校验（12 文件、H1、1—9 节、状态、映射、CR-01—11 覆盖）
PowerShell Markdown 相对链接与围栏校验
git diff --check
```

首次 CR 校验使用了 PowerShell 不支持的 brace glob，首次链接校验的插值变量后紧跟冒号，二者均为
校验命令语法 FAIL；修正命令后得到下表最终结果，期间未改写待验内容以规避失败。

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| CR 结构/映射 | PASS | 12/12 文件；H1、1—9 节、状态与 Demo CR 映射完整；CR-01—11 全覆盖 |
| Markdown 链接/围栏 | PASS | 所有目标相对链接存在，围栏成对 |
| 空白检查 | PASS | `git diff --check` 退出码 0 |
| 编译 | NOT RUN | 纯文档决策包，不修改可执行输入 |
| EditMode | NOT RUN | 同上；G0.1 当前基线为 187/187 PASS |
| PlayMode | NOT RUN | 同上；G0.1 当前基线为 9/9 PASS |
| 内容验证 | NOT RUN | 未修改 Content/Schema/资产；G0.1 Project Validation 为 PASS |
| 构建 | NOT RUN | G0.2 不含 Player 输入变化 |
| 性能/Soak | NOT RUN | G0.2 不含运行时变化 |

## 6. 构建产物

- 配置：NOT RUN
- 路径：无
- 文件 Hash：无
- Build Manifest：无

## 7. 未执行项目

Unity 编译、测试、内容验证、构建与性能均未运行，因为本包只决定后续变更边界，不修改任何可执行
输入。没有把 G0.1 证据改记为本包新执行的结果。

## 8. 已知限制和风险

- CR-2026-004—014 尚无 ADR/Schema/API Freeze 契约，不能进入实现。
- CR-2026-015 延期意味着 Demo 不支持任意 Tick 继续本局。
- 正式资产权利和目标硬件证据仍由 G0.4/G3 门禁处理。

## 9. 未完成项

- G0.3 跨模块契约与 ADR。
- G0.4 资产生产、预算、权利与 provenance 计划。

## 10. 下一步前置条件

- 按 `07_CHANGE_REQUEST_DECISIONS.md` 顺序完成 Schema 6、Pipeline/所有者/事件/随机流、Profile
  Schema 3、ADR、API Freeze 变更计划、迁移与测试矩阵。
- G0.3 完成前不得修改冻结核心程序集实施接受项。

## 11. 结论

`COMPLETE`。G0.2 文档决策门禁完成；该结论不代表接受项已经实现。
