# Codex 结果报告

- 任务：按 Demo 路线实现 G2.1 旧演武场地图运行时
- 里程碑：G2.1 / M08 地图基础切片
- 分支：`codex/qinglan-demo-implementation`
- Git Commit：提交后填写
- 日期：2026-08-06

## 1. 实现范围

完成五区有限旧演武场、13 个稳定锚点、3 个目标、3 个动态事件、5 个地标、地图占位奖励、纯模拟
状态机、独立事件随机流、固定输出事务缓冲、锚点/Walkable 内容验证、Placeholder Scene、双语和
Addressables。未提前实现 Boss、真实奖励、RunResult/Profile、UI、正式资源或 G2.8 Build。

## 2. 新增和修改文件

| 文件 | 变更摘要 |
|---|---|
| `Assets/Game/Simulation/MapObjectiveRuntime.cs`、`QinglanRuntime.cs`、`QinglanSystems.cs` | 地图运行时状态、事件、地标、输出和固定 Tick 接入 |
| `Assets/Game/Content/Runtime/ContentValidator.cs` | 容量、地图所有权与 Walkable Anchor 门禁 |
| `Assets/Game/Presentation/MapAnchorBinding.cs` | Scene 稳定 Anchor ID 绑定 |
| `Assets/Game/Editor/QinglanG21ContentSetup.cs` | Pack 0.6 内容、场景、双语、Addressables 生成 |
| `Assets/GameAssets/Placeholder/QinglanDemo/*` | 13 个新定义、Baked Catalog 与 Placeholder Scene |
| `Assets/Tests/EditMode/QinglanG21MapRuntimeTests.cs` | 6 个地图运行时/内容/Scene/Validation 测试 |
| `Assets/Tests/PlayMode/QinglanG21MapRuntimePlayModeTests.cs` | Defending 状态下移动回归 |
| `Docs/ADR/0019-*`、`Docs/PUBLIC_API_FREEZE.md` | 81 条追加 API 决策、迁移和 Freeze |
| `Docs/DemoDevelopment/16_G2_1_MAP_RUNTIME.md`、`Docs/TEST_PLAN.md` | 详细设计、冻结值与证据 |

## 3. 关键架构决定

- Simulation 是目标/事件/地标唯一真值；Scene 只有表现绑定。
- MapEvent 从 RunId 派生独立随机流，不复用 World/Offer/Reward RNG。
- Tick 后初始化路径只使用固定数组；输出满时原子拒绝并可重试。
- Objective ContentId 是 G2.2 的稳定 Boss Rule 输入；G2.1 不写 Boss 私有字段。
- ADR 0019 接受 Simulation 81 条公共签名追加、0 删除，旧 API 保持兼容。

## 4. 实际执行的命令

```text
Unity.exe -batchmode -projectPath E:\ai\free-world -executeMethod Game.Editor.QinglanG21ContentSetup.RunFromCommandLine ...
dotnet build free-world.slnx --nologo
Unity.exe ... -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode.QinglanG21MapRuntimeTests ...
Unity.exe ... -runTests -testPlatform EditMode ...
Unity.exe ... -runTests -testPlatform PlayMode ...
Unity.exe ... -executeMethod Game.Editor.ProjectValidationCommand.Run ...
Unity.exe ... -executeMethod Game.Editor.M10ApiFreezeCommand.Run ...
Compare-Object G1.7/Game.Simulation.signatures.txt G2.1/Game.Simulation.signatures.txt
Unity.exe ... -executeMethod Game.Editor.M10PerformanceCommand.Run ...
Unity.exe ... -executeMethod Game.Editor.ContentPackBuildCommand.Run ...（两次独立输出目录）
Get-FileHash .../qinglan.pack.demo/0.6.0/catalog.json -Algorithm SHA256
```

## 5. 测试结果

| 检查 | 结果 | 证据 |
|---|---|---|
| 编译 | PASS | `dotnet build free-world.slnx --nologo`，0 error |
| Focused EditMode | PASS | `focused-map-final.xml`，6/6 |
| 全量 EditMode | PASS | `editmode-final.xml`，245/245 |
| 全量 PlayMode | PASS | `playmode-final.xml`，10/10 |
| 内容/治理验证 | PASS | `validation-final.log` 含 `[Project Validation] PASS` |
| API Freeze | PASS | Simulation 1273 / `fd387bc6...1a92b8`；81 添加、0 删除 |
| Pack 双构建 | PASS | 两次各 7 Pack；Catalog 字节一致，SHA-256 `01195cf0...7d86c` |
| 性能/Soak | PASS | p99 4.6635 ms，0 B，GC 0/0/0，Checksum `b455f50ce958d212` |
| Windows Build | NOT RUN | G2.8 对完整垂直切片执行；G2.1 路线不要求单独 Build |

## 6. 构建产物

- 配置：Content Pack 0.6.0 / Schema 6 / 107 definitions
- 路径：`TestResults/QinglanDemo/G2.1/pack-first-final-id/`、`pack-second-final-id/`
- 文件 Hash：Qinglan Catalog `01195cf04c0f1668ebb7384594a77f0e6ca0485b088e00fca1eb74e4b647d86c`
- Build Manifest：Windows Player `NOT RUN`；Content Pack report 已生成

## 7. 未执行项目

- Boss 两只与三目标 8 组合参数 Golden：G2.2。
- 实际灵物/奇物/显化奖励消费：G2.3。
- RunResult/Profile/Story 合并：G2.4/G2.5。
- HUD、提示、输入和可读性：G2.6/G2.8。
- Windows Development Build：G2.8 完整垂直切片门禁。

## 8. 已知限制和风险

- 当前 Scene 是程序化 Placeholder，不代表正式导航、美术或照明质量。
- 事件已具确定性选择和目标解锁，但玩法实体、提示与清理表现留给后续工作包。
- 地标当前输出统一地图占位奖励，真实差异化 Reward/Story 必须由 G2.3/G2.5 接入并保留事务幂等。

## 9. 未完成项

- G2.1 范围内无未完成项；M08 的跨模块退出条件按 G2.2—G2.8 路线继续关闭。

## 10. 下一步前置条件

- G2.2 只通过三个 Objective ContentId 组合 Boss Rule，不读取 Map Runtime 私有数组或 Scene Binding。
- 若 Boss 机制无法用现有 BossPhase/Skill/Status 组合表达，先提交新的 Change Request。

## 11. 结论

`COMPLETE`
