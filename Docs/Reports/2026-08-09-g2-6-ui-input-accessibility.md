# Codex 结果报告

- 任务：按 Demo 路线实现 G2.6 完整 UI、输入与可访问性
- 里程碑：G2.6 / M12 UI Input Accessibility / M14 Settings Save
- 分支：`codex/qinglan-demo-implementation`
- Git Commit：本工作包单一提交
- 日期：2026-08-09

## 1. 实现范围

完成单一程序化 Canvas、全部 Demo 页面、Run HUD、卡牌信息、危险提示、键鼠/手柄统一命令与完整流程；
完成固定容量 `RunUiSnapshot`、真实地图 held 交互、焦点恢复、输入 Map 隔离及断连暂停；完成 Settings 3
字体/色觉/四路音量/字幕迁移与持久化。Result 保存失败保留可重试状态，Loadout 应用必须明确确认。
未导入正式资产，未执行 Release 或 G3 可读性验收。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `RunUiSnapshot.cs`、`RunSession.cs` | 固定容量 UI-safe 单局投影与 held 交互入口 |
| `QinglanDemoUiContracts.cs`、`QinglanDemoPresenter.cs` | 完整页面/命令模型、禁用项跳过、焦点记忆与恢复 |
| `QinglanRuntimeUiRoot.cs` | 单 Canvas、页面/HUD/危险层、三档缩放、五种色觉与本地化卡牌 |
| `M7InputRouter.cs` | Gameplay/UI/Debug Map、键鼠/手柄绑定、Composite 冲突与设备事件 |
| `QinglanDemoFlowController.cs` | 页面、Settings、Meta 与持久提交路由；失败显式重试 |
| `QinglanDemoRuntimeHost.cs`、`GameBootstrapper.cs` | 唯一运行时组合根、10 Hz UI、输入模式和 Bootstrap 接入 |
| `SaveModels.cs`、`UnityJsonSaveCodec.cs` | Settings 3 数据、v1/v2→v3 迁移与兼容构造 |
| `M8ProjectSetup.cs`、Localization Assets | G2.6 双语/Pseudo UI Key 与表资产 |
| `QinglanG26UiInputTests.cs` | Settings、输入、焦点、Canvas、Snapshot、交互与边界测试 |
| `QinglanG26UiInputPlayModeTests.cs` | 键盘/手柄独立闭环、叠层、提交、据点和设备生命周期 |
| ADR 0024、CR-2026-017、Architecture/Schema/Save/Freeze/Test | 所有权、迁移、API、证据与 G2.7 边界同步 |
| `Docs/DemoDevelopment/21_G2_6_*` | G2.6 完整开发与故障/回滚设计 |

## 3. 关键架构决定

- UI 只消费 Application 纯值投影；Game.UI 不引用或持有 Simulation Store。
- 单一 Runtime Host 同时管理 Canvas、Input Map、Presentation、Camera 与 Run 生命周期，页面不各建 Canvas。
- 奖励、显化、Boss、设施与结算资格只展示 Owner 结果，UI 不重算玩法真值。
- Settings 3 通过原子 SaveCoordinator 保存；不使用 PlayerPrefs，v1/v2 连续迁移并拒绝未来版本。
- 手柄连接只恢复焦点，移除才在 Active Run 自动暂停；提交失败不会逐 Tick 自动重试。
- ADR 0024 接受 Game.Application 589 个签名及 Settings 版本常量替换；其他 Freeze 程序集不变。

## 4. 实际执行的命令

```text
dotnet build Game.Tests.EditMode.csproj --nologo
Unity.exe ... -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.QinglanG26UiInputTests ...
Unity.exe ... -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.QinglanG26UiInputPlayModeTests ...
Unity.exe ... -runTests -testPlatform EditMode ...
Unity.exe ... -runTests -testPlatform PlayMode ...
Unity.exe ... -executeMethod Game.Editor.ProjectValidationCommand.Run ...
Unity.exe ... -executeMethod Game.Editor.M10ApiFreezeCommand.Run ...
Unity.exe ... -executeMethod Game.Editor.ContentPackBuildCommand.Run ...（独立输出两次）
Compare-Object content-pack-a content-pack-b（Relative、SHA-256、Length）
powershell -ExecutionPolicy Bypass -File scripts/run-performance.ps1 -TickCount 900 -EnemyCount 600 -ProjectileCount 1200 -PickupCount 2000 -VfxCount 100 -WarmupTicks 300
powershell -ExecutionPolicy Bypass -File scripts/build-windows.ps1 -OutputPath Builds/WindowsDevelopmentG26/AzureSword.exe -EvidenceRoot TestResults/QinglanDemo/G2.6
Builds/WindowsDevelopmentG26/AzureSword.exe -batchmode -nographics -logFile TestResults/QinglanDemo/G2.6/player-smoke-final.log
git diff --check
```

边界修订后的最终 Focused 为 EditMode 7/7、PlayMode 2/2。首次最终全量 PlayMode 为 14/15，旧测试直接
离开 Result 与 G2.5 的保存门禁冲突；测试改为实际完成持久提交后，最终 15/15 PASS。

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| 编译 | PASS | `dotnet build`，0 error；27 条既有 DTO/反射载体 CS0649 警告 |
| G2.6 Focused EditMode | PASS | `focused-editmode-boundaries.xml`，7/7 |
| G2.6 Focused PlayMode | PASS | `focused-playmode-boundaries.xml`，2/2 |
| 全量 EditMode | PASS | `editmode-final-3.xml`，283/283 |
| 全量 PlayMode | PASS | `playmode-final-3.xml`，15/15 |
| 内容/治理验证 | PASS | `validation-final-3.log` 含 `[Project Validation] PASS` |
| API Freeze | PASS | Application 589 / `279c5f16...128`；其他四程序集不变 |
| 内容双构建 | PASS | 两次各 7 Pack，逐文件 SHA-256 与长度一致 |
| 性能短测 | PASS | Tick p99 2.3676 ms、Render p99 0.6256 ms、0 B、GC 0/0/0 |
| Windows x64 Development Build | PASS | `build-development-final.log`、`BuildManifest.json`，Succeeded |
| Development Player Smoke | PASS | `player-smoke-final.log`，实际加载 5 Pack / 220 definitions |
| 12 分钟 Headless | NOT RUN | 不改变固定 Tick 公式；交互专项与目标规模性能短测已执行 |
| Release Build / Release Smoke | NOT RUN | 当前 210 个 Player 条目均为 Placeholder；由 G3.6 执行 |
| 正式字体/音频/视觉可读性 | NOT RUN | 需要 G3 正式资产、provenance 与目标硬件人工验收 |

## 6. 构建产物

- 配置：Windows x64 Development，Unity `6000.3.20f1`
- 路径：`Builds/WindowsDevelopmentG26/AzureSword.exe`
- 文件 Hash：`5d7eeb5359c2e35e4eb1f6a5844b25c3d7556795bd2f15ec234a2011406bc9c6`
- Build Manifest：`Builds/WindowsDevelopmentG26/BuildManifest.json`；Settings 3/Profile 3/Recovery 2，
  EditMode/PlayMode/Validation/Soak 均为 `pass`，Placeholder 210，未批准资产 0
- 内容：`qinglan.pack.demo` 0.9.0 / Schema 6 / 193 definitions；Bootstrap 5 Pack / 220 definitions

## 7. 未执行项目

- G2.7 程序化角色/敌人/地图/技能/VFX/音频表现与对象池整合。
- G2.8 垂直切片统一评审与完整可读性门禁。
- G3 正式资产、正式字体/正文、音乐/音效、目标硬件 GPU/1% Low、Release Build 与合规发布包。
- 任意 Tick 的完整 Run Recovery；CR-2026-015 仍保持 Deferred。

## 8. 已知限制和风险

- OS CJK 字体和程序化 Canvas 仅为 Placeholder，不能代表正式字体裁切、Fallback 和商业授权通过。
- Development 无图形启动验证组合根与内容加载，不替代一局真实玩家人工可读性评审。
- Profile 已写、Recovery 未清且跨进程重启时仍按 ADR 0023 选择不重发平台事件。
- 构建时工作树含本里程碑未提交变更，因此 Manifest `workingTreeClean=false`；源基线准确记录为前一提交，
  最终代码由本工作包随后单一提交固定。

## 9. 未完成项

- G2.6 强制代码、测试、迁移、API、文档、性能和 Development Build 范围内无未完成项。

## 10. 下一步前置条件

- G2.7 复用 `QinglanDemoRuntimeHost`、`RunUiSnapshot` 和现有事件流，不新增第二 UI/Input 真值。
- 所有表现继续使用程序化 Placeholder 和池；不得导入来源不明、参考项目或未登记第三方资源。
- 表现层修改 Scene、生命周期或输入时继续运行相关 PlayMode；热点修改必须重跑性能基线/对比。

## 11. 结论

`COMPLETE`
