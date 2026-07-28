# ADR 0012：M10 性能证据、发布验证、CI 与公共 API 冻结

- 状态：Accepted
- 日期：2026-07-28
- 决策人：依据当前用户 M10 指令

## 背景

M9 已完成框架内容生产工具和 Placeholder Release 阻断，但此前里程碑刻意把 30 分钟 Soak、
1,500 敌人、3,000 投射物、5,000 拾取物的目标规模测量留到 M10。框架冻结还需要可复现的
Development/Release 构建、运行时 Smoke、完整构建清单、干净克隆验证和可检测的公共 API 基线。

## 决策

### 性能与 Soak

- 使用纯 `Game.Simulation` 的固定种子目标规模场景，实际创建 1,500 敌人、3,000 投射物、
  5,000 拾取物，并推进 54,000 个 30 Hz Tick（30 分钟模拟时间）。
- Unity Editor 命令负责预热、计时、内存、GC、池和 JSON 证据；模拟层不引用 Profiler、Editor、
  GameObject 或 UnityEngine。
- 每个高频系统通过无分配计时装饰器采样。只有数据证明存在必要性时才引入 Jobs/Burst；当前短测
  显示最热系统仍满足预算，因此 M10 不改变 Simulation 后端或 Tick 频率。

### Release 验证与构建清单

- 常规非 Development Build 仍会对实际纳入构建的 Addressables Group 和 Scene 依赖执行
  Placeholder、provenance、Third Party 与内容门禁。
- 框架仓库没有正式发行内容，因此 M10 的成功 Release 是“框架发布管线验证”：临时生成一个
  只包含纯程序化 Smoke Runner 的 Scene，并暂时从本次 Player 输入排除 development-only /
  placeholder Addressables Group。该作用域在构建后恢复，不能作为正式内容发布的绕过入口。
- Release Player 必须实际启动，运行固定种子 60 Tick Smoke，并输出可校验 JSON。
- Development 和 Release 均生成完整 Build Manifest，包括 Git SHA/分支/Tag/内容级清洁状态、
  Unity、Content/Save Schema、Pack 版本与 Hash、Package/Addressables Hash、测试状态、UTC 与
  EXE SHA-256。

### CI、干净克隆与 API 冻结

- GitHub Actions 使用带 `self-hosted, Windows, X64, unity` 标签的自托管 Runner，读取机器上已
  激活且版本精确匹配的 Unity，不在仓库或工作流中保存 License Secret。
- CI 顺序固定为 EditMode、PlayMode、Project Validation、目标规模性能、Development Build、
  Release Build 与 Release Player Smoke，并上传日志、XML、JSON 和 Manifest。
- `Scripts/verify-clean-clone.ps1` 从本地仓库创建独立克隆并执行相同门禁，用于证明流程不依赖
  当前工作区的 Library、缓存或未跟踪文件。
- 冻结 `Game.Core`、`Game.Content.Runtime`、`Game.Simulation`、`Game.Application` 和
  `Game.Platform.Abstractions` 的规范化公开类型与 public 成员 API SHA-256。后续变化必须通过 ADR，
  说明兼容性、Schema/存档影响、迁移与回滚，并显式更新基线。

## 被拒绝的方案

- 只用缩小场景外推目标规模：不能证明 1,500/3,000/5,000 的实际存储和热路径。
- 无证据地把所有系统迁移到 Jobs/Burst：增加调度、容器与调试复杂度，且当前测量没有必要性。
- 关闭 Release 门禁或把 Placeholder 标为正式内容：会破坏来源和发布边界。
- 仅检查源码路径而不检查实际 Build 输入：Scene/Addressables 依赖可能把 Placeholder 间接带入。
- 在 CI 内写入 Unity License：增加长期 Secret 风险；自托管 Runner 应由运维预先激活。

## 后果

M10 可以用可复现 JSON、真实 Player 与构建清单冻结框架基线，并在未来 PR 中检测性能和 API
漂移。代价是完整 Soak/构建门禁耗时较长，自托管 Runner 也必须预装精确 Unity 版本。当前 Release
只证明框架管线，不代表仓库已具备可销售的正式内容。
