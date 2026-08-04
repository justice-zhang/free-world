# Codex 结果报告：Qinglan Demo G0.3 Schema、API、Save 与测试契约

- 任务：把 G0.2 接受/延期项固化为可实施契约
- 里程碑：Qinglan Demo G0.3
- 分支：`codex/qinglan-demo-implementation`
- Git Commit：本报告所在提交
- 日期：2026-08-04

## 1. 实现范围

完成 Content Schema 6、模块/Reward token、四个公共 Stat、24 项 Demo Pipeline、数据所有者、伤害/
状态/奖励事务、随机流、Profile Save Schema 3、迁移、回滚、API Freeze 变更窗口及 G1.1—G3.6
测试/性能计划。G0.3 只批准契约，不修改代码、资产、当前 Schema 常量、Save Codec 或冻结 Hash。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Docs/ADR/0013-qinglan-content-schema-6.md` | 接受 Schema 6、14 kind、模块操作数与 Stat 扩展 |
| `Docs/ADR/0014-qinglan-simulation-contracts.md` | 接受 Pipeline、Owner、伤害、状态、Reward、随机流与 Cleanup |
| `Docs/ADR/0015-qinglan-profile-save-schema-3.md` | 接受独立 Save 版本、Profile 3、v2→v3 与幂等结算 |
| `Docs/DemoDevelopment/08_G0_3_CONTRACT_FREEZE.md` | Formal CR→ADR→工作包→测试的权威冻结矩阵 |
| `Docs/ARCHITECTURE.md`、`CONTENT_SCHEMA.md` | 回填根架构与 Schema 真值 |
| `Docs/SAVE_FORMAT.md`、`PUBLIC_API_FREEZE.md` | 回填存档迁移和批准 API 最大面，保留当前 Hash |
| `Docs/TEST_PLAN.md`、`PERFORMANCE_BUDGET.md` | G1.1 起的真实验证矩阵、容量和配对基准 |
| `Docs/EFFECT_MODULES.md` | 登记 6 个获批通用模块 token |
| `Docs/ChangeRequests/CR-2026-004—015-*.md` | 将“待 G0.3”回链到 ADR 0013—0015 |
| Demo 控制文档、执行日志、已知问题 | 更新批准状态、阻塞与下一包边界 |

## 3. 关键架构决定

- Schema 6 追加 14 个定义族；Schema 1—5 的读取器、Hash 和已发布 ID 不重解释。
- 新 Skill 引用在加载期绑定；Tick 热路径不解析字符串。四个 Stat 只追加索引 14—17。
- Demo 使用显式 24 项 Pipeline；旧 M2—M6 构造器保留，Cleanup 和 DamageResolution 单写入者不变。
- DamageApplied 只代表 Shield/Health 实际减少；完全屏障、免疫、通道冷却和零伤害不触发受伤逻辑。
- Reward/Map/Boss/Elite/CharacterMechanic 有独立 Owner；永久输出只能形成 RunResultDelta。
- Settings 2、Profile 3、RunRecovery 2 独立演进；Profile v2→v3 不猜测历史首通。
- CR-11 继续延期；Profile 3 不构成完整 Run Recovery。

## 4. 实际执行的命令

```text
rg / Get-Content（审计现有 Pipeline、Schema、Save、Stat、模块、API Freeze 和模块设计）
PowerShell ADR/CR/Schema/Pipeline/Freeze 一致性校验
PowerShell 变更 Markdown H1/围栏/相对链接/尾随空白/EOF 校验
git diff --check
git status --short
```

首次一致性命令因 `Get-ChildItem -Filter` 不支持使用的范围表达式，且手抄 Platform Hash 首字符为
`5` 而仓库真实值为 `8`，校验结果为 FAIL。修正选择器和期望值后 PASS；真实冻结文档 Hash 从未改写。

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| ADR/CR 映射 | PASS | 3 份 Accepted ADR；12 CR 均有实际 ADR；无“待 G0.3” |
| Schema 一致性 | PASS | 根 Schema 与冻结文档均覆盖相同 14 kind |
| Pipeline 一致性 | PASS | 跨模块权威列表精确 24 项 |
| API 基线保护 | PASS | 五个 M10 SHA-256 原值全部存在，当前代码基线未提前更新 |
| Markdown/链接/空白 | PASS | 30 个变更文档综合检查和 `git diff --check` |
| 编译 | NOT RUN | 纯文档契约包，不修改可执行输入 |
| EditMode | NOT RUN | G1.1 实现后按新矩阵执行 |
| PlayMode | NOT RUN | 同上 |
| 内容验证 | NOT RUN | 当前 Content Schema 常量/Pack 未变化 |
| 构建 | NOT RUN | 当前 Player 输入未变化 |
| 性能/Soak | NOT RUN | 当前 Runtime 未变化；配对短测从 G1.1 执行 |

## 6. 构建产物

- 配置：NOT RUN
- 路径：无
- 文件 Hash：无
- Build Manifest：无

## 7. 未执行项目

Unity 编译、测试、Project Validation、Build 和性能均未运行，因为 G0.3 只改变未来实施规范。
文档明确要求 G1.1 先保存旧 Freeze Hash 下的预期 FAIL 签名 diff，再在批准后更新并跑至 PASS。

## 8. 已知限制和风险

- Schema 6、24 项 Pipeline、Profile 3 和 API 追加尚未实现，QD-KI-006 阻止依赖内容。
- M10 历史性能不能证明新增 Runtime 通过；G1.1 必须同机配对短测。
- CR-11 延期，Demo 不支持任意 Tick Continue。
- 正式资产、字体、音频和目标硬件证据仍未完成。

## 9. 未完成项

- G0.4 正式资产/音频/字体/本地化/provenance 生产计划。
- G1.1 通用 Schema/Runtime/API/Save 骨架和实际 Freeze 更新。

## 10. 下一步前置条件

- G0.4 只能制定可执行生产/权利/预算清单，不提前导入或实施 G1/G3 内容。
- G1.1 实现必须严格限于 `08_G0_3_CONTRACT_FREEZE.md` 的公共 API 最大面和 Fixture 范围。

## 11. 结论

`COMPLETE`。G0.3 架构审查门禁完成；不代表任何新 Runtime 或存档版本已经实现。
