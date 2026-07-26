# M8 严格里程碑审查报告

- 里程碑：M8 版本化存档、本地化与平台边界
- 审查提示词：`Docs/Survivors_Codex_Documentation_Pack/Prompts/13_MILESTONE_REVIEW_GATE.md`
- 分支：`codex/m8-save-localization-platform`
- 基线：`ee9d11ae84b93489b80a9b1946f0646f41a46ef6`（`framework-m7` peeled commit）
- 实现提交：`baddd6914c07a173cc4a6091886f1580c9a1f29d`
- Pull Request：[#14](https://github.com/free-world-team/free-world/pull/14)
- Unity：`6000.3.20f1`
- 日期：2026-07-26

## 里程碑结论

`PASS`

M8 强制交付、迁移/损坏/取消边界、三语言、Null 平台完整流程、工程验证和适用 Development
Build 全部通过。真实 Steam/云传输、Release Build、30 分钟 Soak 和目标规模性能为 `NOT RUN`，
不作为本次通过证据。

## 验收矩阵

| 验收项 | 结果 | 证据 |
|---|---|---|
| M7 基线与分支起点 | PASS | `framework-m7` peeled `ee9d11a...`；基线 163/163、8/8、Validation PASS |
| 三种独立存档模型 | PASS | Settings/Profile/RunRecovery 分离，Schema 2，不可变纯数据模型 |
| 原子 temp/backup/replace、取消 | PASS | `CancellationAfterTemporaryFlushPreservesPreviousPrimary` |
| SHA-256 失败恢复或明确错误 | PASS | 损坏主校验时恢复 `.bak`；无备份返回 `ChecksumMismatch` |
| 显式 v1→v2 迁移 | PASS | 三种迁移注册；Settings 固定 v1 样本迁移到 v2 |
| 只保存稳定 ID/Pack/纯数据 | PASS | JSON/类型审计不含 RuntimeContentIndex 或 Unity Object |
| 缺失内容诊断 | PASS | Profile 保留缺失解锁并告警；Recovery 缺必需内容返回 MissingContent |
| 英文、简中、伪本地化 | PASS | 实际 Unity Table 解析；Pseudo 扩展；PlayMode 中文呈现和 CJK 字符覆盖 |
| UI 无硬编码用户文字 | PASS | Presenter/View 只使用 Key；运行时表解析；Null Identity 无硬编码显示名 |
| Key 完整性门禁 | PASS | Project Validation 检查 active settings、三 Locale、双语表和 103 个非空 Key |
| 五个平台子服务和 Null | PASS | Achievements/Stats/Cloud/RichPresence/Identity 完整边界，无 SDK 启动 |
| Cloud 冲突模型 | PASS | 本地较新→Upload、远端较新→Download、分叉→RequireUserChoice |
| Application Event 平台路由 | PASS | RunCompleted 更新统计/成就；异步后端不阻塞事件发布 |
| Simulation 不引用平台 | PASS | 测试和静态扫描均无 Game.Platform 引用；asmdef 无平台依赖 |
| 无 Steam 完整本地流程 | PASS | 保存设置、创建恢复、完成结算、保存 Profile 并删除恢复 |
| EditMode | PASS | 172/172 |
| PlayMode | PASS | 9/9 |
| Project Validation | PASS | `[Project Validation] PASS` |
| Windows x64 Development Build | PASS | Manifest `Succeeded`；EXE SHA-256 `5D7EEB...C9C6` |
| Release Build | NOT RUN | 当前里程碑适用门禁为 Development Build |
| 性能/Soak | NOT RUN | 30 分钟和目标规模压力按计划在 M10 |

## Git Diff 与范围

- 相对 `framework-m7`，实现提交新增/修改 100 个文件，删除 0 个文件。
- 变更属于存档、Localization/Addressables 自动资产、平台边界、Application Event、M7 低频接线、
  测试、ProjectSettings 配置和同步文档；没有敌人、地图、构筑、正式资源或真实 Steam SDK。
- `.unity` Scene 无变更；`EditorBuildSettings.asset` 只新增 Localization Settings config object，
  Bootstrap Scene 仍为唯一启用 Scene。
- ThirdParty、AI、Packages 无变更；无来源不明或需许可证登记的新资产。

## 架构与禁用模式审查

| 检查 | 结果 | 说明 |
|---|---|---|
| asmdef 方向和循环 | PASS | 全量 Assembly 治理测试通过；新增 UI/Infrastructure Localization 和 Null→Core 符合 ADR 0010 |
| Core/Simulation UnityEngine | PASS | 既有治理测试通过；M8 未向两层加入 Unity 引用 |
| GameObject.Find / FindObjectOfType / Resources.Load | PASS | M8 产品代码静态扫描零命中；内置字体 fallback 不调用 `Resources.Load` |
| 高频 LINQ/反射/字符串格式化/临时集合 | PASS | 存档和平台为低频事件；固定 Tick 未修改；产品代码无 LINQ/反射 |
| Service Locator | PASS | Bootstrap 显式构造并注入 Storage、Codec、RuntimeServices、Localization 和 Platform |
| 高频 Instantiate/Destroy / 逐敌人 Update | PASS | M8 无高频实体生命周期；沿用单个 M7 Runtime Host Update |
| UI/View 写 Simulation | PASS | UI asmdef 不引用 Simulation；设置通过 Application Event，玩法仍走 Flow 命令 |
| 存档与 Unity Object 边界 | PASS | Application 模型 `noEngineReferences`；只保存 stable ID、Pack Version 和纯值 |
| 本地化和资产 | PASS | 103 个双语 Key、Pseudo 和系统字体运行时选择；无外部字体或正式资产 |
| whitespace | PASS | 手写 C#/Markdown/JSON/asmdef 零问题；Unity 生成空 YAML 字段按 M0-KI-007 保留 |

## 审查中发现并完成的最小修复

| 问题 | 根因 | 修复与证据 |
|---|---|---|
| 完整 M8 测试类首次停滞 | 测试用 `.Result` 等待捕获 Unity Context 的异步文件 I/O | 生产 I/O `ConfigureAwait(false)`，测试改 async；9/9 PASS |
| Pseudo 在 PlayMode 返回缺翻译文本 | 运行时 Pseudo Table lookup 未按 Editor 方式解析 fallback | 从 Project Locale 表取 Entry 后做 Pseudo 变换；定向测试 PASS |
| Localization 完整性未进入工程门禁 | 旧验证只检查内容 Key 非空，不检查表存在 | 新增 LocalizationProjectValidator，检查 103 Key 并 PASS |
| Validator 纯命令行误报 Locale 缺失 | 读取尚未初始化的运行时 Locale Provider | 改用 LocalizationEditorSettings 资产 API；Validation PASS |
| 平台事件可能阻塞真实异步 SDK | 路由用同步 GetResult，只因 Null 立即完成而未暴露 | 改为完成快路 + 异步观察/异常转换；Null 流程保持可观测 |

## 实际命令与结果

主要命令和日志路径列于 `2026-07-26-m8-save-localization-platform.md`。最终结果为 EditMode
172/172、PlayMode 9/9、Project Validation PASS、Windows x64 Development Build PASS。

## 未解决问题

- 没有阻止 M8 合并或 M9 开始的 `OPEN` 问题。
- M8-KI-001 至 M8-KI-004 记录开局级恢复、Null Cloud、系统字体和 M10 性能边界。
- Release、真实 Steam/Cloud 和 30 分钟/目标规模性能均为 `NOT RUN`。
