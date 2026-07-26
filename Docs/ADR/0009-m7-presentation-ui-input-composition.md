# ADR 0009：M7 表现、UI、输入与组合根边界

- 状态：Accepted
- 日期：2026-07-26
- 决策人：依据当前用户 M7 指令

## 背景

M6 已提供 `RunSession`、`UpgradeOfferSet`、`RenderSnapshot` 和应用状态，但 Bootstrap 尚未把
真实运行会话连接到 Unity View、输入、摄像机或页面。M7 必须建立这些边界，同时保证 UI/View
不成为玩法真值、表现对象高频生命周期使用池，并继续满足 M2 的事件批次消费契约。

## 决策

### Assembly 与组合方向

- `Game.Presentation` 依赖 Application、Simulation、Core、Input System 和 uGUI，只消费快照、
  稳定表现 ID 与事件。
- `Game.UI` 依赖 Application 和 uGUI；Presenter/ViewModel 不引用 Simulation。
- `Game.Infrastructure` 作为唯一 Unity 组合根依赖 Simulation、Presentation、UI 和 Input System，
  创建真实 M6 Placeholder Run，并连接输入命令、Presenter、View Pool 和 Camera。
- 依赖保持单向且无循环；`Game.Core` 和 `Game.Simulation` 继续不引用 UnityEngine。

### View 与表现请求

- 一个 `PresentationCoordinator` 对 RenderSnapshot 做实体集合对账和插值；四类实体 View 使用
  分池，View 只保存 `SpatialEntity` Binding 和显示状态。
- `RunSession.TryGetVisualProfileId` 是敌人稳定表现身份的只读应用边界。Profile 未命中，或实体
  当前没有实例级表现 ID 时，使用程序化 Sprite fallback。
- Simulation/Combat Event 在实际执行 Tick 后立即转为 Hit、Death、Status 表现请求；VFX、
  AudioSource 和伤害数字分别池化，伤害数字共享 UI Canvas。
- 音频只使用运行时生成且明确命名的短测试音，不引入正式或外部资产。

### 输入、UI 与可访问性

- Input Action Map 固定为 Gameplay、UI、Debug。RunHUD 启用 Gameplay，其余页面启用 UI；Debug
  在框架开发阶段保持启用。键鼠和主流 Gamepad 共享命令路径。
- UI 只依赖 `IGameFlowController`、`IInputRebindService` 和 UI-safe 投影；候选过滤、伤害、经验、
  死亡和掉落继续留在 Simulation/Application。
- Settings 的应用模型统一保存死区、震动强度、屏幕震动、闪光、伤害数字和自动瞄准策略。
- 所有页面文本使用 Localization Key；M8 再接入真实 Localization Table 与伪本地化验证。

## 被拒绝的方案

- 每实体 MonoBehaviour Update：无法满足目标实体规模和集中式生命周期规则。
- UI 直接持有 `SimulationWorld` 或 Store：会产生双重真值并绕过应用命令。
- 用 `FindObjectOfType`/Service Locator 连接页面：破坏组合根和测试隔离。
- 每个伤害数字一个 Canvas：破坏批处理与对象预算。
- 为测试流程导入外部 UI、VFX 或音频：违反 Placeholder 与资产来源规则。

## 后果

Bootstrap 现在可以进入真实 Placeholder Run，并通过键鼠或 Gamepad 完成选择、暂停、升级和
结算；缺少资源仍可运行。代价是 Infrastructure 明确增加 Presentation/UI/InputSystem 组合依赖，
因此 Assembly 治理测试同步锁定新图。正式本地化、皮肤、音效和目标规模表现基准不在 M7，
分别留给 M8/M9/M10。
