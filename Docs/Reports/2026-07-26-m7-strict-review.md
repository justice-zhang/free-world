# M7 严格里程碑审查报告

- 里程碑：M7 表现层、输入与完整 UI 流程
- 审查提示词：`Docs/Survivors_Codex_Documentation_Pack/Prompts/13_MILESTONE_REVIEW_GATE.md`
- 分支：`codex/m7-presentation-ui-input`
- 基线：`5f72f1a785fe6c027d4c9753c150aabd51831d6a`（`framework-m6` peeled commit）
- 实现提交：`abdd15969023d3c3f9ba968063aae99a800d5264`
- Unity：`6000.3.20f1`
- 日期：2026-07-26

## 里程碑结论

`PASS`

M7 强制交付、完整流程、自动测试、工程验证和适用 Development Build 全部通过。正式 Localization
Table/伪本地化属于 M8；30 分钟 Soak 和目标实体规模性能 JSON 属于 M10，本次均如实标为
`NOT RUN`，不作为 M7 通过证据。

## 验收矩阵

| 验收项 | 结果 | 证据 |
|---|---|---|
| M6 基线已验收且工作树干净 | PASS | 分支从 `framework-m6` peeled commit `5f72f1a...` 创建；基线 EditMode 154/154、PlayMode 5/5、验证 PASS |
| Actor/Projectile/Area/Pickup View Binding 和池 | PASS | `CoordinatorPoolsAllKindsAndRejectsStaleRelease` 覆盖四类对象、回收和代际句柄 |
| View 不拥有玩法真值 | PASS | View 只保存 Handle、Kind、Transform 和 VisualProfile；`UiAssemblyAndViewsExposeNoSimulationStoreWrites` 通过 |
| Snapshot 插值 | PASS | `EntityViewBindsInterpolatesAndRejectsDifferentHandle`、`InterpolationClampsAndUsesShortestFacingArc` |
| 实体生成、释放和失效句柄 | PASS | Snapshot reconciliation 生成/回收；错误 Generation 不释放新绑定 |
| 受击、死亡和状态表现请求 | PASS | Combat Event 转换为同批次 Hit/Death 请求；状态请求缓冲测试通过 |
| VFX、Audio 和伤害数字池化 | PASS | 集中式池预热/增长/回收；伤害数字共享同一 Canvas；AudioSource 和测试音池化 |
| Input Action Maps | PASS | `Gameplay`、`UI`、`Debug` 存在；Gameplay/UI 互斥，Debug 独立启用 |
| 键鼠、主流手柄和重映射 | PASS | Move/Pause/Navigate/Submit 等具有 Keyboard/Gamepad Binding；交互式重映射接口测试通过 |
| 11 个应用页面 | PASS | Bootstrap、MainMenu、CharacterSelect、MapSelect、Loading、RunHUD、Pause、LevelUpDraft、RunResult、Settings、ContentError 均有 ViewModel |
| UI 使用 Presenter/ViewModel | PASS | `Game.UI` 不引用 `Game.Simulation`；UI 只依赖 Application contracts |
| 可访问性设置 | PASS | 死区、震动、屏幕震动、闪光、伤害数字、自动瞄准策略均存在并执行范围夹取 |
| 摄像机、边界和可关闭震动 | PASS | `CameraBoundsAndEffectsToggleAreHonored` 覆盖有限边界与禁用效果 |
| VisualProfile fallback | PASS | 敌人稳定 ID 解析；缺失 Profile 计数并使用程序化颜色/几何 fallback |
| 启动到测试局并完成结算 | PASS | PlayMode 使用真实 M6 内容：主菜单→角色→地图→Loading→Run→升级→暂停→结算→主菜单 |
| 键鼠和手柄菜单/升级/暂停/结算 | PASS | `KeyboardAndGamepadCompleteMenuUpgradePauseAndResultFlow` 通过 |
| 暂停时 Simulation 停止、UI 响应 | PASS | 暂停期间 Tick 不增加，UI 输入仍可恢复/结算 |
| 场景释放无池或事件泄漏 | PASS | `DestroyingBootstrapReleasesViewsPoolsAndInputOwner` 通过 |
| 无外部美术或来源不明资产 | PASS | M7 变更仅增加程序化 Placeholder、Input Action YAML 和代码；ThirdParty/AI 目录零变更 |
| 用户文字本地化就绪 | PASS | Presenter 输出 Localization Key，逻辑不硬编码正式用户文本 |
| EditMode | PASS | `TestResults/M7Final/editmode.xml`：163/163，0 failed，0 skipped |
| PlayMode | PASS | `TestResults/M7Final/playmode.xml`：8/8，0 failed，0 skipped |
| 内容/工程验证 | PASS | `TestResults/M7Final/validation.log`：`[Project Validation] PASS` |
| Windows x64 Development Build | PASS | Manifest `Succeeded`；EXE SHA-256 `5D7EEB...C9C6` |
| 正式本地化、伪本地化和字体覆盖 | NOT RUN | M8 计划项；M7 只建立 Key 边界和 Placeholder 呈现 |
| 30 分钟 Soak 与目标规模性能 JSON | NOT RUN | M10 计划项；池生命周期功能测试不外推为性能通过 |

## Git Diff 与范围

- 相对 `framework-m6`，实现提交新增 35、修改 18、删除 0，共 53 个文件。
- 变更均属于 Application、Presentation、UI、Infrastructure 组合、程序化 Placeholder、Bootstrap、
  测试、asmdef、ProjectSettings 或同步文档。
- M7 修改 Bootstrap Scene 和项目 Input Action 引用属于当前 UI/Input 生命周期范围；未修改 Packages、
  存档格式、内容 Schema、第三方代码或正式资产。
- 未发现 M8 本地化工具、局外系统、正式 UI 皮肤或后续性能后端等范围外实现。

## 架构与禁用模式审查

| 检查 | 结果 | 说明 |
|---|---|---|
| asmdef 方向和循环 | PASS | UI→Application，Presentation→Application/Simulation 只读边界，Infrastructure 为组合根；Unity 四项门禁均完成程序集编译 |
| Core/Simulation 引用 UnityEngine | PASS | `using UnityEngine`/`UnityEngine.` 静态扫描零命中 |
| GameObject.Find / FindObjectOfType / Resources.Load | PASS | 产品程序集静态扫描零命中；内置字体使用 Unity built-in API，不调用 `Resources.Load` |
| 高频 LINQ、反射、字符串格式化和临时集合 | PASS | M7 产品路径无 System.Linq/Reflection；Snapshot 同步复用集合和池 |
| 全局 Service Locator | PASS | 静态扫描零命中；Bootstrap 显式创建并注入流程、输入、表现和 UI |
| 高频 Instantiate/Destroy | PASS | 创建仅发生在池增长、Input Asset 初始化和 Bootstrap 组合；Destroy 仅发生在 teardown，无逐实体帧调用 |
| 逐实体 MonoBehaviour.Update | PASS | 只有组合根 `M7RuntimeHost.Update` 与单摄像机 `LateUpdate`；四类 View 和短效对象无 Update |
| UI/View 直接写 Simulation Store | PASS | UI asmdef 不引用 Simulation；View 无 Store 字段/调用；应用命令是唯一写入口 |
| 内容、存档和 Unity Object 边界 | PASS | 敌人只通过稳定 VisualProfileId 跨边界；Simulation 不持有 GameObject/Sprite/AudioClip；存档格式未变 |
| 本地化和资产规则 | PASS | 逻辑使用 Key；新音画均为程序生成/明确测试音；ThirdParty/AI 无变更 |
| whitespace 和 Unity 资产身份 | PASS | 完整实现 diff `git diff --check` 为 0；Project Validation 检查引用和工程配置通过 |

## 审查中发现并完成的最小修复

| 文件 | 修复 |
|---|---|
| `Assets/Game/Presentation/PresentationCoordinator.cs`、`Assets/Game/Infrastructure/M7RuntimeHost.cs` | 调整事件消费与 Snapshot 同步顺序，并仅在 Handle 仍绑定时处理 Removed 事件，避免同批次重复释放 |
| `Assets/Tests/PlayMode/M7FullFlowPlayModeTests.cs` | 使用 Input System TestFixture 的正确 teardown 顺序，确保虚拟键鼠/手柄和 Input owner 都被释放 |
| `Assets/GameAssets/Placeholder/M7InputActions.asset` 及 `.meta` | 只清理 Unity 生成空字段的行尾空格，使完整实现 diff 通过 whitespace 门禁 |

所有最小修复都在最终全量 EditMode、PlayMode、Validation 和 Development Build 之前完成。

## 实际命令与结果

| 命令类别 | 结果 |
|---|---|
| M7 Project Setup | PASS，Bootstrap、Camera、四个 baked catalog 和项目 Input Action 引用已配置 |
| 基线 EditMode / PlayMode / Validation | PASS，154/154、5/5、Validation PASS |
| 全量 EditMode | PASS，163/163 |
| 全量 PlayMode | PASS，8/8 |
| Project Validation | PASS |
| Windows Development Build | PASS，Manifest `Succeeded` |
| diff/name-status/禁用模式/外部资产静态扫描 | PASS，53 个实现文件；无禁用模式或外部资产命中 |

完整命令列于 `Docs/Reports/2026-07-26-m7-presentation-ui-input.md`。

## 未解决问题

- 没有阻止 M7 合并或 M8 开始的 `OPEN` 问题。
- 正式本地化、伪本地化和字体覆盖为 `NOT RUN`，计划在 M8。
- 30 分钟 Soak、目标实体规模与性能分位 JSON 为 `NOT RUN`，计划在 M10。
- 程序化 fallback、Debug Map 的 release 边界和性能事实边界已登记为 M7-KI-001 至 M7-KI-004。
