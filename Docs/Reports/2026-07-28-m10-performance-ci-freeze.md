# Codex 结果报告

- 任务：性能、CI、构建与框架冻结
- 里程碑：M10
- 分支：`codex/m10-performance-ci-freeze`
- Git Commit：`da8980694e7d3713a9dda0781ff35ee6b77496c8`
- 日期：2026-07-28

## 1. 实现范围

完成可配置目标规模 Stress Harness、54,000 Tick Headless Soak、性能/内存/GC/池/诊断 JSON、分系统
热点计时和固定种子 Checksum。实际测量在预算内，因此未迁移 Jobs/Burst，也未改变 30 Hz 模拟真值。

完成完整 Build Manifest、Windows Development/Release verification、Release Player Smoke、自托管
Windows CI、提交后独立干净克隆入口、五个核心程序集公共 API Freeze、架构审计和 Definition of
Done 签字。Release verification 不包含正式内容；没有新增玩法或正式/第三方资产。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `M10StressHarness.cs`、Simulation `AssemblyInfo.cs` | 目标规模纯模拟、分系统零分配计时、测试可见边界 |
| `M10PerformanceCommand.cs`、`run-performance.ps1` | 30 分钟测量、机器/分位/内存/GC/池/诊断 JSON 与 CLI |
| `PresentationEffects.cs` | 有界 VFX 池、命中/扩容/失败/丢弃指标，保留原构造器兼容性 |
| `M10PerformanceFreezeTests.cs` | 配置、目标实体、确定性、零分配、VFX 池和非法配置覆盖 |
| `BuildManifestWriter.cs`、`Windows*Build.cs` | 完整 Manifest、Windows 长路径 Hash、Development/Release 构建 |
| `ReleaseBuildGateValidator.cs`、`M10ReleaseSmokeRunner.cs` | 实际 Scene/Group Release 门禁和可启动 Player Smoke |
| `CoreApiFreezeValidator.cs`、`ProjectGovernanceValidator.cs` | 五程序集规范化 public API Hash 门禁 |
| `Scripts/*.ps1`、`.github/workflows/windows-self-hosted.yml` | 可靠 Editor 等待、全门禁脚本、干净克隆和自托管 CI |
| ADR 0012、Build/CI、API Freeze、Architecture/Test/Performance | M10 长期决定、操作与证据同步 |
| Framework Signoff、Execution Log、Known Issues、两份报告 | 冻结签字、历史计划项关闭与真实失败记录 |

## 3. 关键架构决定

- ADR 0012 接受纯 Simulation 目标规模 Harness 与 Editor 测量边界；Profiler/JSON 不进入固定 Tick。
- EnemyDecision 正式平均 6.1583 ms、总体 Tick p99 10.9851 ms，未达到需要 Jobs/Burst 的证据阈值。
- Release verification 只排除全组均为 Placeholder/development-only 的 Addressables Group；混合组
  保持包含并由 Release 门禁阻断，不能漏打正式内容来换取成功。
- Build Manifest 绑定 Git、Unity、Schema、Pack/Catalog、Packages、Addressables、测试状态和 EXE Hash。
- public API Freeze 只覆盖公开类型与 public 成员；破坏性变化须经 ADR、兼容性和迁移计划。

## 4. 实际执行的命令

```text
git fetch --prune --tags origin
git switch main
git pull --ff-only origin main
git switch -c codex/m10-performance-ci-freeze

./Scripts/test.ps1 -Platform EditMode -ResultsDirectory TestResults/M10Final
./Scripts/test.ps1 -Platform PlayMode -ResultsDirectory TestResults/M10Final
./Scripts/validate.ps1 -LogPath TestResults/M10Final/validation.log
./Scripts/run-performance.ps1 -OutputPath TestResults/M10Final/performance.json -LogPath TestResults/M10Final/performance.log
./Scripts/build-windows.ps1 -OutputPath Builds/WindowsDevelopment/AzureSword.exe -LogPath TestResults/M10Final/build-development.log -EvidenceRoot TestResults/M10Final
./Scripts/build-windows-release.ps1 -OutputPath Builds/WindowsRelease/AzureSword.exe -LogPath TestResults/M10Final/build-release.log -EvidenceRoot TestResults/M10Final
./Scripts/run-player-smoke.ps1 -PlayerPath Builds/WindowsRelease/AzureSword.exe -OutputPath TestResults/M10Final/release-player.json -LogPath TestResults/M10Final/release-player.log

./Scripts/verify-clean-clone.ps1 -SourceRepository F:\Code\AzureSword -Branch codex/m10-performance-ci-freeze -EvidenceOutput F:\Code\AzureSword\TestResults\M10CleanCloneEvidence -KeepClone

rg -n <禁用模式> Assets/Game/Simulation/M10StressHarness.cs Assets/Game/Editor Assets/Game/Presentation
git diff --check
git diff --name-status framework-m9
git log --oneline --decorate --graph
```

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| M9 基线 | PASS | EditMode 181/181、PlayMode 9/9、Validation PASS |
| 最终 EditMode | PASS | 根工作区与干净克隆均 187/187，0 failed/skipped |
| 最终 PlayMode | PASS | 根工作区与干净克隆均 9/9，0 failed/skipped |
| 内容/工程/API Freeze | PASS | `[Project Validation] PASS`；五程序集 Hash 匹配 |
| 30 分钟目标规模 | PASS | 54,000 Tick；1,500/3,000/5,000；Checksum `13193d7c4cc3251a` |
| 性能/分配/内存 | PASS | Tick p99 10.9851 ms；Render CPU p99 1.2482 ms；0 B、0 GC、无持续增长 |
| Development Build | PASS | 根工作区与干净克隆均 Succeeded |
| Release verification | PASS | 非 Development；实际输入 Placeholder=0，四项测试证据 pass |
| Release Player | PASS | 60 Tick、4 actors/snapshot、0 invalid handles、退出码 0 |
| 干净 clone | PASS | 提交 `da89806` 的七阶段完整流水线，结束后源码树无差异 |
| GitHub Actions 实际 run | NOT RUN | Workflow 已提交；组织自托管 Runner 尚未运行 |

失败尝试真实记录：沙箱内 Unity 两次因许可证访问返回 198；沙箱外首次 PlayMode 因 Scene Processor
解引用 null BuildReport 为 9/9 FAIL；构建/测试外层曾被 `Start-Process -Wait` 后代进程拖到超时；
Player 首次调用参数名不匹配；第一次干净克隆在 264 字符 Addressables 路径的 Manifest Hash 失败。
这些问题均最小修复并在最终门禁或第二次完整干净克隆重跑；没有把失败尝试改写为 PASS。

## 6. 构建产物

- Development：`Builds/WindowsDevelopment/AzureSword.exe`；干净克隆 SHA-256
  `5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6`。
- Release verification：`Builds/WindowsRelease/AzureSword.exe`；SHA-256
  `34C4E304E53E56499267DFD9C975C63DC279ED3011A69A8CA16EB207F1856A8F`。
- 干净克隆 Manifest：两者均绑定 `da8980694e7d3713a9dda0781ff35ee6b77496c8`，
  `workingTreeClean=true`，Content/Save Schema 5/2，四项证据 pass。

## 7. 未执行项目

- GitHub 自托管 workflow 实际运行：`NOT RUN`；已交付 workflow 和实际 PASS 的等价干净克隆脚本。
- 正式内容 GPU Frame Time/RenderDoc：`NOT RUN`；当前 Null Device 指标是明确标注的 CPU 表现探针。
- Steam SDK、正式内容、签名安装包/商店分发：`NOT RUN`，不属于框架 M10。

## 8. 已知限制和风险

- Release verification 是内容为空的框架 Player，不是可销售版本。
- 性能数值绑定报告中的 Ryzen 9 9950X3D/48 GB/Null Device 环境；未来真实内容需单独 GPU 基线。
- 自托管 Runner 必须预装并激活 Unity `6000.3.20f1`，且配置四个约定标签。

## 9. 未完成项

- M10 强制实现和本地/干净克隆门禁无未完成项；外部 CI 实际 run 与正式内容性能按已知限制跟踪。

## 10. 下一步前置条件

- 合并 PR 后让 `framework-m10` 指向最终 `main` merge commit，删除功能分支并切回干净 `main`。
- 后续公共 API 破坏性变化先提交 ADR、兼容性、迁移和回滚计划，并运行性能回归。

## 11. 结论

`COMPLETE`
