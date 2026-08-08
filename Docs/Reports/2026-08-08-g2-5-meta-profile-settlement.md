# Codex 结果报告

- 任务：按 Demo 路线实现 G2.5 局外成长、Profile v3 与原子结算
- 里程碑：G2.5 / M11 Meta Hub / M14 Save Platform
- 分支：`codex/qinglan-demo-implementation`
- Git Commit：本工作包单一提交
- 日期：2026-08-08

## 1. 实现范围

完成 12 节点三分支、3 嵌片、4 设施、3 故事、6 藏品、灵砂购买与免费重置；完成缺失 ID 安全降级、
不可变运行 Loadout/唯一奖励快照、Profile 单一 Owner、胜负差异化永久合并、事务幂等、保存/Recovery
双失败重试、提交后平台事件和 Result 页面门禁。实现 CR-2026-015 当前 Demo 的 Recovery 检测、拒绝提示、
明确开始新局清理与禁止 Continue。未提前实现 G2.6 实际 UI/输入或 G3 正式资产/Release。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Assets/Game/Application/QinglanMetaProgression.cs` | 数据驱动购买、装配、容量/前置/互斥、设施和缺失 ID 投影 |
| `Assets/Game/Application/QinglanProfileCoordinator.cs` | Profile 单一 Owner、原子结算、双阶段重试、Recovery 拒绝和提交事件 |
| `QinglanRunResults.cs`、`QinglanApplicationContracts.cs` | 不可变 MetaLoadout、唯一奖励快照和兼容构造 |
| `DemoRunCoordinator.cs`、`ApplicationEvents.cs` | 保存前 Result 门禁、提交确认和 `RunResultCommitted` |
| `PlatformApplicationEventRouter.cs` | 提交后平台路由和胜利成就边界 |
| `BuildState.cs`、`QinglanDemoRunFactory.cs` | 开局低频 Meta 输出注入和已领取唯一奖励初始化 |
| `QinglanContentValidation.cs` | MetaFacility/Story Schema 6 合法引用目标扩展 |
| `QinglanG25ContentSetup.cs` 与生成资产 | Pack 0.9.0 的 43 个新增定义、双语键和 Addressables |
| `QinglanG25MetaSaveTests.cs` | 8 项局外、结算、失败重试、恢复拒绝和装配测试 |
| `QinglanG25SettlementPlayModeTests.cs` | 保存/清 Recovery 前禁止离开 Result 的真实流程测试 |
| `Docs/ADR/0023-*`、Freeze/Schema/Save/Architecture | 决策、兼容、迁移、API 和事务顺序同步 |
| `Docs/DemoDevelopment/20_G2_5_*` | G2.5 模块结构、内容拓扑、协议、验证与 G2.6 边界 |

## 3. 关键架构决定

- `QinglanProfileCoordinator` 是唯一 Profile 写 Owner；冻结结果、内存候选、原子保存、清 Recovery、发布
  事件、允许页面离开的顺序不可交换。
- 已写 Profile 后清 Recovery 失败依靠持久化事务和同进程待发布标记重试；重启后不重发平台输出。
- Victory 与失败局分别过滤永久输出；失败仍保留合法灵砂、藏品、非胜利故事和统计。
- 缺失 Loadout ID 保留原 Profile，运行时使用安全空投影，不做未经确认的静默重写。
- 设施、故事、藏品从 Registry 引用和通用标签派生，不在 Application 硬编码具体内容 ID。
- ADR 0023 接受 Simulation +1、Application +73、删除 0；其他冻结程序集无变化。

## 4. 实际执行的命令

```text
dotnet build Game.Tests.EditMode.csproj
Unity.exe ... -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.QinglanG25MetaSaveTests ...
Unity.exe ... -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.QinglanG25SettlementPlayModeTests ...
Unity.exe ... -runTests -testPlatform EditMode ...
Unity.exe ... -runTests -testPlatform PlayMode ...
Unity.exe ... -executeMethod Game.Editor.M10ApiFreezeCommand.Run ...
powershell -ExecutionPolicy Bypass -File scripts/validate.ps1 ...
Unity.exe ... -executeMethod Game.Editor.ContentPackBuildCommand.Run ...（独立输出两次）
Compare-Object content-pack-a content-pack-b（Relative、SHA-256、Length）
powershell -ExecutionPolicy Bypass -File scripts/run-performance.ps1 -TickCount 900 -EnemyCount 600 -ProjectileCount 1200 -PickupCount 2000 -VfxCount 100 -WarmupTicks 300
powershell -ExecutionPolicy Bypass -File scripts/build-windows.ps1 -OutputPath Builds/WindowsDevelopmentG25/AzureSword.exe -EvidenceRoot TestResults/QinglanDemo/G2.5
powershell -ExecutionPolicy Bypass -File scripts/run-player-smoke.ps1 ...
git diff --check
```

首次全量 EditMode 为 272/276：四个旧里程碑测试分别硬编码旧设施引用文案、地标单输出、Pack 0.8.0
和 150 definitions。保留旧行为断言并更新为 0.9.0 的向前兼容契约后，最终 276/276 PASS。

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| 编译 | PASS | `dotnet build Game.Tests.EditMode.csproj`，0 error；27 条既有 DTO CS0649 警告 |
| G2.5 Focused EditMode | PASS | `editmode-focused.xml`，8/8 |
| G2.5 Focused PlayMode | PASS | `playmode-focused.xml`，1/1 |
| 全量 EditMode | PASS | `editmode-final.xml`，276/276 |
| 全量 PlayMode | PASS | `playmode-final.xml`，13/13 |
| 内容/治理验证 | PASS | `project-validation-final.log` 含 `[Project Validation] PASS` |
| API Freeze | PASS | Simulation 1403 / `6966b53f...db76`；Application 523 / `743d388f...1cf6` |
| Pack 双构建 | PASS | 两次各 7 Pack；Qinglan Catalog SHA-256 均为 `1a56442c...5aa` |
| 性能短测 | PASS | Tick p99 2.6436 ms、Render p99 0.7134 ms、0 B、GC 0/0/0 |
| Windows x64 Development Build | PASS | `build-development.log`、`BuildManifest.json`，Succeeded |
| Release Player Smoke | NOT RUN | 脚本只用于带 M10 Smoke Scene 的 Release Build；Development Player 进入 MainMenu 后按预期不自退 |
| 12 分钟 Headless | NOT RUN | 本包只新增开局/页面低频路径，未改变固定 Tick；执行目标规模短测与全量回归 |

## 6. 构建产物

- 配置：Windows x64 Development，Unity `6000.3.20f1`
- 路径：`Builds/WindowsDevelopmentG25/AzureSword.exe`
- 文件 Hash：`5d7eeb5359c2e35e4eb1f6a5844b25c3d7556795bd2f15ec234a2011406bc9c6`
- Build Manifest：`Builds/WindowsDevelopmentG25/BuildManifest.json`；EditMode/PlayMode/Validation/Soak
  证据均为 `pass`，未批准资产 0
- 内容：`qinglan.pack.demo` 0.9.0 / Schema 6 / 193 definitions；Content Hash
  `d332199604988624b32837002059ed0218a4f89b947874810adfc2bfbf098d8d`

## 7. 未执行项目

- G2.6 实际标题、选择、结算、据点、故事/收藏 UI、键鼠/手柄和可访问性页面。
- 完整任意 Tick Run Recovery；CR-2026-015 仍为 Deferred，当前只实现拒绝与明确清理。
- Steam 真实 SDK、Cloud、正式成就后台与跨进程平台重试；当前通过抽象和 Null Backend 验证。
- G3 正式视觉、音频、字体、目标硬件 GPU/1% Low、Release Build 和 Release Player Smoke。

## 8. 已知限制和风险

- Profile 已写、Recovery 未清且进程重启时不重发平台事件；这是 ADR 0023 明确选择的重复安全优先策略。
- 缺失内容会让当前 Run 使用空 Loadout，需由 G2.6 用警告和确认式修复入口向玩家解释。
- 设施目前只有 Locked/Available 真值投影；Visited/Updated 展示状态属于 G2.6 UI 层。
- 当前 210 个 Player 条目均为程序化 Placeholder，不能作为正式发布资产。

## 9. 未完成项

- G2.5 强制代码、内容、测试、API、文档、性能和 Development Build 范围内无未完成项。

## 10. 下一步前置条件

- G2.6 只调用 Profile Owner 命令和只读投影，不复制保存/事务/设施派生逻辑。
- Result UI 必须等待 Commit 状态成功后才能显示“已保存”或开放 Hub/Title 导航。
- Recovery 提示不得出现 Continue；清理失败必须保留重试入口。

## 11. 结论

`COMPLETE`
