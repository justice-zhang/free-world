# Codex 结果报告

- 任务：按 Demo 文档顺序完成 G2.8 Placeholder 垂直切片统一门禁
- 里程碑：G2.8 / M01—M16 Integration Gate
- 分支：`codex/qinglan-demo-implementation`
- Git Commit：本工作包单一提交
- 日期：2026-08-09

## 1. 实现范围

完成真实 `QinglanDemoRunFactory` 三路线 12 分钟自动玩家、同 Seed 重放、实际地图出生公平、目标到最终
Boss 规则同步、Event 装配、generation-safe Owner/Cleanup、P0 Sprite 排序、十次 Host 生命周期、
600 敌人压力截图、完整性能 Soak、Development Build 和独立 Player 闭环。

未导入正式资产或第三方包；未改变 Content/Save Schema、30 Hz、冻结公共 API 或程序集方向。目标 GPU、
正式音频/字体/本地化、Release 与商业合规属于 G3。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `QinglanG28VerticalSliceCommand.cs` | 真实 Factory/RunSession 12 分钟、三路线、重放和出生公平 JSON 门禁 |
| `QinglanG28ReadabilityCommand.cs` | 600 敌人、P0 危险、标准/高对比 1080p 渲染与指标 |
| `QinglanG28DevelopmentSmokeRunner.cs`、Player 脚本 | 独立 Build 内公共流程、存档、Hub 清理与再次出发 |
| `QinglanG28VerticalSliceTests.cs` | 三路线、重放、两 Boss、地图内容和出生公平 EditMode |
| `QinglanG28VerticalSlicePlayModeTests.cs` | 十次真实 Host 生命周期与池/Input Owner 断言 |
| `BossPhaseRuntime.cs`、`QinglanSystems.cs` | 通用 Map Objective Rule Mask 同步到 Boss |
| `QinglanDemoRunFactory.cs` | Map Event 装配时 Armed；Editor 诊断入口和 World 只读 friend |
| `SpawnRuntime.cs` | 障碍修正后再次保证玩家出生保护距离 |
| `SimulationSystems.cs`、`SimulationWorld.cs`、`SkillRuntime.cs` | 同结构阶段 Cleanup 和 generation-safe Owner 生命周期 |
| `EntityViews.cs` | P0/Mechanic/Combat/Decoration 遮挡排序 40/30/20/10 |
| G2.8 设计、Roadmap、Traceability、Test/Performance/Known Issues/Execution | 门禁范围、证据、风险和下一阶段同步 |

## 3. 关键架构决定

- 自动玩家只提供输入和选择，生产 Factory、Map、Encounter、Progression、Reward、Boss 与 Result 仍是
  唯一真值。
- 目标对 Boss 的影响按 `MapObjectiveRuntime` 规则集合循环同步，不硬编码听风或风脉台 ID。
- Event 由 Composition Root 对所选地图统一 Arm；地图内容扩展不修改核心系统。
- Cleanup 在同一结构阶段消费追加命令，Area/Projectile 在读取 Owner 前验证完整 Handle generation；没有
  清零或忽略 InvalidHandle 诊断。
- P0 优先级落实为 Presentation 排序，不更改模拟命中。无需新增 ADR。

## 4. 实际执行的命令

```text
Unity.exe -batchmode -nographics -projectPath E:\ai\free-world -runTests -testPlatform EditMode ...
Unity.exe -batchmode -nographics -projectPath E:\ai\free-world -runTests -testPlatform PlayMode ...
Unity.exe -batchmode -nographics -projectPath E:\ai\free-world -executeMethod Game.Editor.QinglanG28VerticalSliceCommand.Run ...
Unity.exe -batchmode -projectPath E:\ai\free-world -executeMethod Game.Editor.QinglanG28ReadabilityCommand.Run ...
powershell -File Scripts/validate.ps1 -LogPath TestResults/QinglanDemo/G2.8/validation-final.log
Unity.exe ... -executeMethod Game.Editor.M10ApiFreezeCommand.Run ...
Unity.exe ... -executeMethod Game.Editor.ContentPackBuildCommand.Run（两次并比较 Relative/Length/SHA-256）
powershell -File Scripts/run-performance.ps1 -TickCount 54000 -EnemyCount 1500 -ProjectileCount 3000 -PickupCount 5000 -VfxCount 200
powershell -File Scripts/build-windows.ps1 -OutputPath Builds/WindowsDevelopmentG28/AzureSword.exe -EvidenceRoot TestResults/QinglanDemo/G2.8/manifest-evidence
powershell -File Scripts/run-qinglan-g28-player-smoke.ps1 -Executable Builds/WindowsDevelopmentG28/AzureSword.exe ...
git diff --check
```

迭代验收先后暴露真实 Event 未 Armed、Boss Rule Mask 未同步、障碍回退点距离不足、Skill Owner 过期和
路线交互误触；逐项修复后才取得本报告的最终证据。性能外层等待 120 秒先超时，但 Unity 子进程保持
响应并继续运行；最终同一进程生成 54,000 Tick `PASS` JSON，未缩短 Tick 或降低实体数。

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| Unity 编译 | PASS | 最终 EditMode/PlayMode/Build 均重新编译全部程序集 |
| 真实 12 分钟三路线 | PASS | `vertical-slice-final.json`；4 局各 21,784 Tick、Victory、2 Boss、0 InvalidHandle |
| 同 Seed 重放 | PASS | Primary/Repeated Combined Checksum 一致 |
| 实际地图出生公平 | PASS | 21,600 Tick、2,552 普通＋2 Boss、Min 14、全部 Walkable |
| 全量 EditMode | PASS | 292/292 |
| 全量 PlayMode | PASS | 17/17；含十次 Host 清理 |
| 600 敌人可读性 | PASS | 915 View、318 P0、排序 40>20；标准/High Contrast 截图人工复核 |
| 内容/治理验证 | PASS | `validation-final.log` 含 `[Project Validation] PASS` |
| API Freeze | PASS | Simulation 1406 / `b901d061...ab82`；Application 590 / `a5950312...a3bc` |
| 内容双构建 | PASS | 两次各 14 文件；路径/长度/SHA-256 差异 0 |
| 性能/Soak | PASS | 54,000 Tick；Tick p99 11.4069 ms、Render 1.2213 ms、0 B、GC 0、Invalid 0 |
| Windows Development Build | PASS | Manifest Succeeded，四项证据均 pass |
| 独立 Development Player | PASS | 标题→Run→暂停→升级→结算/保存→Hub→再次出发，退出码 0 |
| 正式 GPU/资产/音频/字体/Release | NOT RUN | 需要 G3 正式来源、许可和目标硬件 |

## 6. 构建产物

- 配置：Windows x64 Development，Unity `6000.3.20f1`
- 路径：`Builds/WindowsDevelopmentG28/AzureSword.exe`
- 文件 Hash：SHA-256 `5d7eeb5359c2e35e4eb1f6a5844b25c3d7556795bd2f15ec234a2011406bc9c6`
- Build Manifest：`Builds/WindowsDevelopmentG28/BuildManifest.json`；Content 6 / Save 3 / Settings 3 /
  Profile 3 / Recovery 2；EditMode/PlayMode/Validation/Soak `pass`；Placeholder 210；未批准资产 0
- 构建时工作树含本工作包未提交变更，因此 Manifest 如实记录 `workingTreeClean=false` 和前一提交
  `5377f33`；本报告与代码随后由本工作包单一提交固定。

## 7. 未执行项目

- G3.1—G3.3 正式角色、敌人、Boss、地图、UI、VFX、音频、字体与完整 provenance/许可证。
- G3.4 正式数值平衡、人工手感和失败率矩阵。
- G3.5 正式资产目标 GPU、Overdraw、显存、1080p 60 和 1% Low。
- G3.6 Release Build/Player、Placeholder=0、正式 Manifest/合规签字和激活的 CI Runner。

## 8. 已知限制和风险

- 可读性截图是程序化 Placeholder；它证明优先级、形状和遮挡，不代表最终视觉品质。
- Render p99 是 Null Device Snapshot/VFX CPU 探针，不是 GPU Frame Time。
- Development Manifest 含 210 个 Placeholder，不能作为 Release 候选。
- CR-11 仍只支持检测并拒绝不完整 Run，不支持任意 Tick Continue。

## 9. 未完成项

- G2.8 强制范围内无未完成项。

## 10. 下一步前置条件

- 只进入 G3.1；先完成全部 Release 输入的 provenance/Hash 自动门禁和负向测试。
- 按 G0.4 Manifest 小批导入正式视觉资产，每批单独记录来源、许可、Hash、审查和 Addressables。
- 保持 Content/Save Schema、30 Hz、冻结 API 和单一玩法/表现 Owner；新增机制先走 CR。

## 11. 结论

`COMPLETE`
