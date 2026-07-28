# M10 框架冻结架构审计

- 基线：`framework-m9`
- 目标分支：`codex/m10-performance-ci-freeze`
- Unity：`6000.3.20f1`
- 审计日期：2026-07-28
- 最终结论：在 M10 全部门禁完成前为 `NOT RUN`

## 1. Assembly 与依赖方向

- `Game.Core` 和 `Game.Simulation` 继续不引用 UnityEngine；M10 Stress Harness 位于 Simulation，
  只使用纯值存储、系统和固定 Tick。
- `Game.Editor` 作为最外层工具新增对 `Game.Application` 的直接引用，用于读取 Save Schema 和生成
  Manifest。Application 不反向引用 Editor，不形成循环。
- `Game.Infrastructure` 只在 Release Smoke Scene 中组合 Application/Simulation；平台抽象仍通过
  Facade 边界消费，Steam SDK 未进入 Core/Simulation。
- 公共 API 由五个稳定程序集的规范化签名 Hash 冻结，见 `Docs/PUBLIC_API_FREEZE.md`。

## 2. 高频模拟与性能边界

- 目标规模是实际 1,500 Enemy、3,000 Projectile、5,000 Pickup；Enemy、Movement、Lifetime、
  Cleanup 和 Snapshot 仍为稠密批处理，没有逐敌 `MonoBehaviour.Update`。
- 热路径不使用 LINQ、反射、字符串格式化或每 Tick 临时托管集合；预热后的固定 Tick 分配必须为
  0 B。Profiler、Stopwatch 汇总和 JSON 只存在于测试/Editor 边界。
- 短测证据显示 EnemyDecision 最热，但总体 p99 仍在预算内，因而不引入 Jobs/Burst。若未来基线
  超限，先提供分系统证据和 ADR，再选择可回滚的批处理后端。

## 3. 内容扩展、存档、本地化与平台

- Content Schema 保持 5；新增角色、技能、被动、Trait、敌人、地图、Encounter、Synergy 和
  Evolution 仍由 Pack/稳定 ContentId 表达，不修改核心程序集。
- Save Schema 保持 2，存档不保存 RuntimeContentIndex 或 Unity Object；M10 没有改变迁移策略。
- 用户可见文本继续通过 Localization Key；M10 命令行/Manifest 诊断不是玩家 UI。
- Release Smoke 不调用真实 Steam、云或成就后端；Null 平台边界仍可离线运行。

## 4. Scene、Addressables 与资产来源

- Development 使用既有 Bootstrap；M10 不修改正式 Scene 或 ProjectSettings。
- Release verification 的临时 Scene 和 Addressables 作用域只存在于构建命令内，并在 finally 中恢复。
  Release Validator 检查实际 Build Scene 依赖和所有 IncludeInBuild Group。
- 仓库中的 Placeholder 继续留在 `Assets/GameAssets/Placeholder` 并阻止普通 Release。框架验证构建
  不把这些内容纳入 Player；Manifest 必须报告实际输入 Placeholder 为 0。
- M10 不新增正式、AI 或 Third Party 资产，不改变 provenance/许可证要求。

## 5. 可复现性与冻结门禁

- 固定种子压力场景进行同配置双次校验，并在正式运行输出最终 Checksum。
- Development/Release Manifest 绑定构建时 Git 来源、版本、Schema、Pack、Packages、Addressables、
  测试状态和 Player Hash。
- GitHub 自托管 Windows Runner 与本地干净克隆运行同一 PowerShell 入口；CI 不携带 License Secret。
- 最终审查必须核对 Git diff/日志、Scene/ProjectSettings、禁用模式、完整测试、30 分钟 Soak、两个
  构建、Release Player、干净克隆与文档。任何未实际执行项只能写 `NOT RUN`。

## 6. 当前审计状态

架构实现已完成初检；正式 54,000 Tick、最终两个 Build、Release Player、干净克隆和严格审查的
最终状态将在实际执行后写入 M10 结果与严格审查报告。本文件不预先把这些项目声明为通过。
