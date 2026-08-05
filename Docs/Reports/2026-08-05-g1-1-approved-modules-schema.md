# Codex 结果报告：Qinglan Demo G1.1 批准通用模块与 Schema

- 任务：实现 G0.3 批准的通用 Schema、Simulation、Save 与公共 API 骨架
- 里程碑：Qinglan Demo G1.1
- 分支：`codex/qinglan-demo-implementation`
- Git Commit：本报告所在提交
- 日期：2026-08-05

## 1. 实现范围

完成 Content Schema 6、14 个新 kind、24 项固定流水线、通用状态事务、往返投射、伤害通道策略、
4 个新 Stat 的实际消费者、Profile 3 纯数据/Codec/连续迁移、API Freeze 更新和机器可读证据导出。
本包只提供后续 Qinglan 内容依赖的通用能力与测试 Fixture，没有创建角色、武器、心诀、敌人、地图或
正式资产；Meta Validator、结算 Coordinator 和完整局外事务仍属于 G2.5。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Assets/Game/Core/StatId.cs`、`DamageChannelId.cs` | 4 个批准 Stat 与 5 个稳定伤害通道 |
| `Assets/Game/Content/Runtime/Qinglan*.cs` | Schema 6 定义、DTO、解析与跨引用验证 |
| `Assets/Game/Content/Authoring/*Authoring.cs`、`ContentBaker.cs` | 新 kind 作者输入、Bake 与旧 Schema 兼容 |
| `Assets/Game/Simulation/Qinglan*.cs`、`StatusTransactionRuntime.cs` | 24 项 Pipeline、机制运行时、状态原子事务 |
| `Assets/Game/Simulation/Skill*.cs`、`Combat*.cs`、`Stats.cs` | 往返投射、Detonate、触发位置、通道策略与 Stat 消费 |
| `Assets/Game/Application/Save*.cs`、`QinglanApplicationContracts.cs` | 独立文档版本、Profile 3、稳定事务/应用层契约 |
| `Assets/Game/Infrastructure/UnityJsonSaveCodec.cs` | Profile 3 编解码、canonical 集合与 v1→2→3/v2→3 |
| `Assets/Game/Editor/CoreApiFreezeValidator.cs`、`BuildManifestWriter.cs` | 签名导出与三文档版本 Manifest |
| `Assets/Tests/EditMode/QinglanG11ContractsTests.cs` 等 | G1.1 契约、迁移、兼容、机制和分配回归 |
| `Docs/PUBLIC_API_FREEZE.md`、ADR 0015、路线/模块/日志/问题 | 批准 Hash、分期边界与实测证据 |

## 3. 关键架构决定

- `Game.Core` 继续零 `UnityEngine` 引用；Qinglan 高频机制只在固定 Tick 的纯模拟层执行。
- Schema 6 只追加 kind/字段；Schema 1—5 golden hash 不变，旧公共构造器继续保留。
- 零伤害同时保留旧 `DamageApplied(0)` 事件并追加显式 `DamageResolved(Zero)`，避免下游语义丢失。
- `SaveSchema.CurrentVersion` 作为已弃用最高版本改为 3；内部使用按文档类型查询，不混用版本。
- ADR 0015 修订分期：G1.1 提前交付通用 Codec/Migration/Fixture；G2.5 保留 Meta 校验和完整幂等结算。

## 4. 实际执行的命令

```text
dotnet build FreeWorld.sln
Unity.exe -batchmode -nographics -projectPath E:/ai/free-world -runTests -testPlatform EditMode -testResults TestResults/QinglanDemo/G1.1/editmode.xml -quit
Unity.exe -batchmode -nographics -projectPath E:/ai/free-world -runTests -testPlatform PlayMode -testResults TestResults/QinglanDemo/G1.1/playmode.xml -quit
powershell -ExecutionPolicy Bypass -File Scripts/validate.ps1 -LogFile TestResults/QinglanDemo/G1.1/validation.log
Unity.exe -batchmode -nographics -projectPath E:/ai/free-world -executeMethod Game.Editor.M10ApiFreezeCommand.Run -quit
powershell -ExecutionPolicy Bypass -File Scripts/run-performance.ps1（900 Tick；G0.4 基线与 G1.1 当前配对）
powershell -ExecutionPolicy Bypass -File Scripts/build-windows.ps1（BUILD_OUTPUT=Builds/WindowsDevelopmentG11/AzureSword.exe）
rg（架构禁用 API、Core UnityEngine、.meta/GUID 静态检查）
git diff --check
```

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| 编译 | PASS | Unity 6.3 脚本编译与 Windows Build；`dotnet build` 亦通过 |
| EditMode | PASS | `TestResults/QinglanDemo/G1.1/editmode.xml`，203/203 |
| PlayMode | PASS | `TestResults/QinglanDemo/G1.1/playmode.xml`，9/9 |
| 内容验证 | PASS | `validation.log` 含 `[Project Validation] PASS`；Schema 1—5 golden hash 保持 |
| API Freeze | PASS | 旧基线预期差异已保存；Core/Content/Simulation/Application 仅批准扩展，Platform 不变 |
| 构建 | PASS | Windows x64 Development，Manifest `result=Succeeded`、`unapprovedAssetCount=0` |
| 性能/Soak | PASS | 900 Tick 当前 p99 9.1799 ms、0 B；54,000 Tick 机制分配回归属于 203 项 EditMode 并通过 |

批准 Freeze：Core 168/`25766747...e176`，Content 918/`ca593752...502d`，Simulation
1160/`a6555342...81f`，Application 346/`bea7fe99...6197`，Platform 73/`8eb5f2cc...1738`；
完整 SHA-256 见 `Docs/PUBLIC_API_FREEZE.md`。

## 6. 构建产物

- 配置：Unity 6000.3.20f1，StandaloneWindows64，Development
- 路径：`Builds/WindowsDevelopmentG11/AzureSword.exe`
- 文件 Hash：`5d7eeb5359c2e35e4eb1f6a5844b25c3d7556795bd2f15ec234a2011406bc9c6`
- Build Manifest：`Builds/WindowsDevelopmentG11/BuildManifest.json`

`Builds/` 与 `TestResults/` 是仓库既有忽略的可再生证据目录，不纳入源码提交；报告保留路径、计数、
Hash 与判定，所有非忽略未跟踪源码和 `.meta` 纳入本提交。

## 7. 未执行项目

Development Player 自动退出冒烟 `NOT RUN`：当前自动退出 Runner 只注入 Release Build，Development
Player 正常进入 MainMenu 后持续运行，已按精确进程主动终止；不把该观察表述为 PASS。G1.1 没有执行
G2.5 完整存档结算 PlayMode、G3 正式资产验证或 Release Player 重启，这些不属于当前包。

## 8. 已知限制和风险

- G1.1 只有通用定义/执行器，没有 Qinglan 实际内容，功能可玩性从 G1.2 开始建立。
- Profile 3 基础 Codec/Migration 已完成，但 Meta 容量/互斥/缺内容验证与事务 Coordinator 未完成。
- Development Build Manifest 记录构建时工作树未提交，这是按“验证后提交”的门禁顺序产生的真实状态。
- 正式资产、FirstParty provenance 自动门禁和字体许可仍由 QD-KI-003/007/008 阻断 Release。

## 9. 未完成项

- G1.2—G1.7 数据切片与完整 Placeholder Pack。
- G2.1—G2.8 可玩垂直切片与完整局外事务。
- G3.1—G3.6 正式生产、Release 与目标硬件验收。

## 10. 下一步前置条件

- G1.2 只创建 M02/M03 角色/战斗内容和固定 Seed 测试，消费 G1.1 公共模块。
- 后续若发现机制无法由已批准模块表达，必须先提交新的 Change Request，不得硬编码内容例外。

## 11. 结论

`COMPLETE`。G1.1 全部强制编译、EditMode、PlayMode、Validation、API、性能短测和 Development Build
门禁均有实际 PASS 证据；未把不适用或未执行项目表述为通过。
