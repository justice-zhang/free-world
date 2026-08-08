# Codex 结果报告

- 任务：按 Demo 路线实现 G2.7 程序化 Placeholder 表现、池、预警和音频
- 里程碑：G2.7 / M13 Presentation Assets Audio（Placeholder 子集）
- 分支：`codex/qinglan-demo-implementation`
- Git Commit：本工作包单一提交
- 日期：2026-08-09

## 1. 实现范围

完成 Registry/Tag/Delivery 驱动的玩家、敌人、Boss、Projectile、Area、Pickup 与 Affix 程序化 Profile；
完成九种形状、轮廓/方向/色觉通道、旧庭边界/障碍/五区/11 个状态标记、乘风档位与 Boss 阶段信号；
完成 200 VFX、总计 32 AudioSource、96 伤害数字的有界池和 P0 驱逐/合并策略；完成代码生成测试音、
四路音量、Gameplay/Paused/Story/Boss 混音和危险 Duck。

未导入正式 Sprite、动画、材质、Shader、音频、字体或第三方包；未改变 Content/Save Schema、30 Hz
Tick 或 Assembly 方向。正式资产、GPU、音频质量和 Release 不属于 G2.7。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `ProceduralPresentationProfiles.cs` | P0—P3、九形状、语义音频、色觉变体和 Profile Catalog |
| `EntityViews.cs`、`ViewPools.cs` | 程序化 Sprite Library、轮廓、方向、双 Affix Overlay 与池化绑定 |
| `PresentationEffects.cs` | 200 VFX、32 AudioSource、96 伤害数字、优先级/冷却/Duck/混音和诊断 |
| `PresentationCoordinator.cs` | 单一 Owner、事件路由、乘风/Boss 信号、Settings 和 Map 同步 |
| `ProceduralMapPresentation.cs` | 边界、区域、障碍和 Objective/Event/Landmark 世界表现 |
| `QinglanProceduralPresentationFactory.cs` | 按通用 kind/tag/Delivery/Profile 构建样式，不按具体内容 ID 分支 |
| `QinglanProceduralMapFactory.cs` | RuntimeMapDefinition 到 Presentation DTO 的只读转换 |
| `RunSession.cs`、`SkillRuntime.cs`、`EnemyRuntime.cs` | 稳定 Delivery/Profile/Pickup/Affix 身份只读桥接 |
| `QinglanDemoRuntimeHost.cs` | 复用 G2.6 Host，接入目录、地图、Run 状态和混音生命周期 |
| `QinglanG27PresentationPolishTests.cs` | Profile、身份、地图、三池策略和稳态分配专项 |
| `QinglanG27PresentationPolishPlayModeTests.cs` | 真实 Run、地图、玩家轮廓、色觉切换、容量和销毁集成 |
| ADR 0025、CR-2026-018、Architecture/API/Performance/Test | 决策、API Freeze、预算和证据同步 |
| `22_G2_7_PLACEHOLDER_PRESENTATION_POLISH.md` | G2.7 完整实现、生命周期、降级和 G2.8 边界设计 |

## 3. 关键架构决定

- Simulation/Application 只追加 4 条稳定 ID 只读查询，Presentation 不读取 Store 或可变记录。
- Infrastructure 在 Run 装配低频阶段从内容 kind/tag/Delivery/Profile 构建样式；新增内容不修改核心程序集。
- P0 危险不静默丢弃：满池先驱逐低层，再合并同类 P0；P2/P3 允许丢弃/聚合并记录计数。
- 色觉、低闪、无震动和关闭伤害数字不会删除 P0 的形状、轮廓或方向通道。
- 地图世界层只消费纯 DTO 和 `RunUiSnapshot`，不参与 Walkable、交互、出生、奖励或状态转换。
- ADR 0025 接受 Simulation 1406 / Application 590 的新 Freeze；Content/Save/Tick 不变。

## 4. 实际执行的命令

```text
dotnet build Game.Presentation.csproj --nologo（Unity 导入新文件前，生成 csproj 未包含新源，FAIL；不作最终编译证据）
Unity.exe -batchmode -nographics -quit -projectPath E:\ai\free-world -logFile ...\compile-3.log
Unity.exe ... -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.QinglanG27PresentationPolishTests ...
Unity.exe ... -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.QinglanG27PresentationPolishPlayModeTests ...
Unity.exe ... -runTests -testPlatform EditMode ...\editmode-final.xml
Unity.exe ... -runTests -testPlatform PlayMode ...\playmode-final.xml
Unity.exe ... -executeMethod Game.Editor.ProjectValidationCommand.Run ...\validation-old-hash-expected-fail.log
Unity.exe ... -executeMethod Game.Editor.M10ApiFreezeCommand.Run（api-current / api-final）
Unity.exe ... -executeMethod Game.Editor.ProjectValidationCommand.Run ...\validation-final.log
Unity.exe ... -executeMethod Game.Editor.ContentPackBuildCommand.Run（content-pack-a / content-pack-b）
Compare-Object content-pack-a content-pack-b（Relative、Length、SHA-256）
powershell -ExecutionPolicy Bypass -File scripts/run-performance.ps1 -TickCount 900 -EnemyCount 600 -ProjectileCount 1200 -PickupCount 2000 -VfxCount 200 -WarmupTicks 300
powershell -ExecutionPolicy Bypass -File scripts/build-windows.ps1 -OutputPath Builds/WindowsDevelopmentG27/AzureSword.exe -EvidenceRoot TestResults/QinglanDemo/G2.7
Builds/WindowsDevelopmentG27/AzureSword.exe -batchmode -nographics -logFile ...\player-smoke-final.log
git diff --check
```

首次 Focused EditMode 为 4/5，音频测试在 P0 尚可驱逐普通源时过早期望合并；补足三次合法驱逐后验证
“全部 P0 满池再合并”，最终 7/7。该修订没有放宽池策略。初始 `dotnet build` 失败只因 Unity 生成
csproj 尚未重新列入新文件；锁定版本 Unity 随后实际导入并编译全部程序集成功。

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| Unity 编译 | PASS | `compile-3.log`，Tundra build success；最终测试/Build 再次完成编译 |
| G2.7 Focused EditMode | PASS | `focused-editmode-final-2.xml`，7/7 |
| G2.7 Focused PlayMode | PASS | `focused-playmode-final.xml`，1/1 |
| 全量 EditMode | PASS | `editmode-final.xml`，290/290 |
| 全量 PlayMode | PASS | `playmode-final.xml`，16/16 |
| 内容/治理验证 | PASS | `validation-final.log` 含 `[Project Validation] PASS` |
| API Freeze | PASS | Simulation 1406 / `b901d061...ab82`；Application 590 / `a5950312...a3bc` |
| API 规范差异 | PASS | Simulation +3、Application +1、其他 0；总新增 4、删除 0 |
| 内容双构建 | PASS | 两次各 7 Pack / 14 文件，逐文件 Hash/长度差异 0 |
| 性能短测 | PASS | Tick p99 2.5539 ms、Render p99 1.0531 ms、0 B、GC 0/0/0、200 VFX 0 丢弃 |
| Windows x64 Development Build | PASS | `build-development-final.log`、`BuildManifest.json`，Succeeded |
| Development Player 启动 Smoke | PASS | `player-smoke-final.log`，实际加载 5 Pack / 220 definitions；取证后明确终止进程 |
| 12 分钟完整玩家流程 | NOT RUN | 固定由 G2.8 对完整垂直切片执行 |
| 600—1200 敌人 P0 人工可读性 | NOT RUN | G2.8 需实际可玩压力场景与人工评审 |
| 正式 GPU/音频/资产与 Release | NOT RUN | 需要 G3 正式来源、许可和目标硬件 |

## 6. 构建产物

- 配置：Windows x64 Development，Unity `6000.3.20f1`
- 路径：`Builds/WindowsDevelopmentG27/AzureSword.exe`
- EXE SHA-256：`5d7eeb5359c2e35e4eb1f6a5844b25c3d7556795bd2f15ec234a2011406bc9c6`
- Build Manifest：`Builds/WindowsDevelopmentG27/BuildManifest.json`；Settings 3/Profile 3/Recovery 2，
  EditMode/PlayMode/Validation/Soak 均为 `pass`，Placeholder 210，未批准资产 0
- 内容：`qinglan.pack.demo` 0.9.0 / Schema 6 / 193 definitions；Content Hash
  `d332199604988624b32837002059ed0218a4f89b947874810adfc2bfbf098d8d`

## 7. 未执行项目

- G2.8 完整 12 分钟垂直切片、实际 Boss/高压 P0 可读性、连续两局和统一完成定义门禁。
- G3 正式角色/敌人/地图/UI/VFX/音频/字体、完整 provenance/许可证、目标 GPU/Overdraw/1% Low。
- Release Build/Smoke 与无 Placeholder 合规包；当前 Development Manifest 有 210 个 Placeholder 条目。

## 8. 已知限制和风险

- 生成 Sprite 和正弦测试音只证明身份、优先级、混音与生命周期，不代表正式视听质量。
- Null Device Render CPU 数据不测 GPU、透明 Overdraw、Shader Variant、音频 DSP 或目标硬件 1% Low。
- 程序化地图标记按锚点后缀/稳定 fallback 绑定；G3 正式地图 Profile 应显式作者绑定并走 provenance。
- Build 时工作树含本工作包未提交变更，Manifest `workingTreeClean=false`，源基线记录前一提交；最终代码
  由本工作包随后单一提交固定。

## 9. 未完成项

- G2.7 强制代码、测试、API、Validation、内容确定性、性能和 Development Build 范围内无未完成项。

## 10. 下一步前置条件

- G2.8 复用当前 Profile、池、Host、UI 和固定内容，不扩 Schema 或建立第二套玩法/表现真值。
- 运行完整 12 分钟真实流程、Boss/奖励/结算/再次出发、生命周期和 P0 压力可读性审查。
- 正式资源继续留在 G3；任何文件在来源、许可、Hash 和审核未完成前不得进入 Release 标签。

## 11. 结论

`COMPLETE`
