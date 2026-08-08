# 21 G2.6 UI、输入与可访问性

## 1. 工作包目标

G2.6 把 G2.4/G2.5 已冻结的流程、单局和 Profile 真值接到一个可操作的程序化 Demo 界面，交付标题到
再次出发的键鼠/手柄闭环、Run HUD、选择覆盖层、据点页面与最低可访问性设置。所有资源仍是框架阶段
Placeholder；正式 UI、美术、字体、音频和 Release 可读性评审属于 G3。

本包实现 CR-2026-017 和 ADR 0024：Content Schema 保持 6，Profile 保持 3，RunRecovery 保持 2；
Settings Save 从 2 升至 3。UI 不读取 Simulation Store，不重算奖励、显化、Boss、设施或结算资格。

## 2. 运行时结构与所有权

```text
Input System
  ├─ Gameplay：Move / Map / Pause / Interact
  ├─ UI：Navigate / Submit / Cancel / Tab / Page
  └─ Debug：F2 / F3（仅 Development Player）
        ↓
QinglanDemoRuntimeHost（唯一生命周期与组合根）
  ├─ QinglanDemoPresenter（页面、焦点、只读页面模型）
  ├─ QinglanRuntimeUiRoot（单 Canvas、HUD、危险层）
  ├─ QinglanDemoFlowController（流程命令与 Settings/Profile 路由）
  └─ M7 Camera/Presentation + DemoRunCoordinator + QinglanProfileCoordinator
        ↓
RunSession.CaptureUiSnapshot（10 Hz、固定容量、复用缓冲）
```

`QinglanDemoRuntimeHost` 是 Bootstrap 唯一 UI/Input/Presentation Owner。Gameplay 和 UI Action Map 互斥；
页面命令完成后立即重新应用输入模式，覆盖层关闭不会向 Gameplay 泄漏同帧输入。Flow Controller 只调用
已有 Owner 命令：保存失败停留 Result 并显示重试；成功前不开放 Hub/Title。自动提交每个结果只尝试一次，
失败后必须由玩家明确重试，避免每帧写盘。

## 3. 页面路由

| 页面/层 | 数据来源 | 允许命令 | 强制边界 |
|---|---|---|---|
| 标题/档案 | SaveCoordinator、Recovery 检测 | 开始、设置、退出 | 不提供未实现的 Continue |
| 角色/地图/Loadout | Registry、Profile 只读投影 | 导航、选择、确认、返回 | 缺失 ID 显式警告，不静默改 Profile |
| 加载 | DemoRunCoordinator | 取消 | 不读取 Scene 玩法状态 |
| Run HUD | `RunUiSnapshot` | Move、Interact、Map、Pause | 10 Hz 刷新，模拟仍为 30 Hz |
| 升级/奖励 | Application 候选投影 | 选择、确认 | 不在 UI 重算 Eligibility |
| 暂停/设置/地图 | Flow/Settings/Snapshot | 恢复、设置、返回 | 打开时禁用 Gameplay Map |
| 结算 | 不可变 RunResult、Commit 状态 | 重试保存、Hub、标题 | 保存与 Recovery 清理完成前不可离开 |
| 据点设施/行脉/收藏/故事 | QinglanProfileCoordinator 只读投影 | 购买、装配、浏览 | Loadout 应用前必须二次确认 |
| 内容错误 | 本地化错误 Key | 返回安全页 | 不显示内部异常或猜测替代内容 |

Presenter 使用统一 `QinglanDemoPageModel`。禁用项跳过焦点；每页记忆最后合法焦点，页面重建、手柄重连
或内容改变后优先恢复，原目标失效时落到第一个可见启用项。鼠标点击、滚轮、键盘和手柄都转换成相同
UI 命令，不产生设备专用业务分支。

## 4. HUD 与只读快照

`RunUiSnapshot` 固定保存生命/护盾、XP/等级、乘风机制、武器/心诀/奇物、显化资格、地图目标/事件/
地标和 Boss 阶段。缓冲只在构造时分配；`CaptureUiSnapshot` 原位覆盖并通过测试验证稳态 0 B。

- HUD 只渲染纯值、稳定 ID 和 Localization Key，不保存 `EntityHandle`、Store、Scene、Prefab 或 Sprite。
- Interact 使用 held 命令进入 `RunSession`，由地图 Objective/Event/Landmark Owner 判定距离、进度和输出。
- 升级、奖励和显化卡展示本地化行为描述、目标等级、类型标签、构筑关系和 Owner 给出的资格状态。
- Boss 危险始终同时使用形状、方向与文字；颜色、闪光、震动和伤害数字都不是唯一信息通道。

## 5. 输入与设备生命周期

标准绑定契约在运行时补齐缺失项：WASD 与方向键移动、Gamepad Stick/D-pad、鼠标点击/右键/滚轮、
键盘确认/取消/分页。重绑冲突扫描包含 Composite 的子绑定，不能用 W 同时占用多个动作。

- 键盘和手柄分别独立完成标题→选择→Run→暂停/覆盖层→结果保存→据点→再次出发。
- 手柄连接只触发焦点恢复，不擅自暂停；Active Run 中移除手柄自动进入 UserPaused。
- 暂停、地图、升级和奖励覆盖层禁用 Gameplay Map；Debug Map 仅 `Debug.isDebugBuild` 启用。
- 自动瞄准、死区、震动和重绑沿用 Settings 2 字段并通过 Settings 3 round-trip 保存。

## 6. Settings 3 与可访问性

Settings 3 新增三档字体缩放（100/125/150%）、五种色觉模式、Master/Music/SFX/Ambience 四路音量和
字幕开关；保留 Locale、Stick Deadzone、Vibration、Screen Shake、Flashing、Damage Numbers、Auto Aim
与 Rebinds。v2→v3 安全默认是 100%、Standard、四路 100%、字幕开启；v1 按 v1→v2→v3 连续迁移。

程序化 Canvas 统一应用字体倍率和色觉 Palette。en、zh-Hans 与 Pseudo 共用稳定 Key；当前 OS CJK 字体
只是开发期 fallback。设置保存仍走 `SaveCoordinator` 的按 kind 校验、备份与原子替换，不使用
PlayerPrefs。

## 7. API、兼容和内容

- Game.Application 规范签名由 523 增至 589：追加 67 条；唯一移除签名是
  `SettingsCurrentVersion=2` 被批准替换为 3。
- Game.Core、Game.Content.Runtime、Game.Simulation、Game.Platform.Abstractions 规范签名不变。
- 旧 Settings 构造函数、M7/M8 公共流程与旧测试调用保留；未来 Settings 版本仍拒绝读取。
- Qinglan Pack 保持 0.9.0 / Schema 6 / 193 definitions；Bootstrap 实际加载 5 Pack / 220 definitions。
- M8 Setup 生成 en/zh-Hans，并从英文派生 Pseudo；UI 表当前共 607 个 Key。

## 8. 验证矩阵

| 检查 | 最终结果 | 覆盖 |
|---|---|---|
| G2.6 Focused EditMode | PASS 7/7 | Settings 迁移、输入冲突、焦点、Canvas/CJK、危险通道、快照 0 B、真实交互、程序集边界 |
| G2.6 Focused PlayMode | PASS 2/2 | 键盘和手柄独立闭环、覆盖层、保存、据点、装配确认、设备断连 |
| 全量 EditMode/PlayMode | PASS 283/283、15/15 | M0—G2.5 回归及 G2.6 新流程 |
| Project/API | PASS | 治理、内容、本地化、Settings 3 与五程序集 Freeze |
| 内容双构建 | PASS | 两次各 7 Pack，逐文件 SHA-256/长度一致 |
| 性能短测 | PASS | 900 Tick＋300 预热；Tick p99 2.3676 ms，Render p99 0.6256 ms，0 B、0 GC |
| Windows Development | PASS | StandaloneWindows64、Manifest 四项证据 pass、Player 启动标记 |

证据目录为 `TestResults/QinglanDemo/G2.6/`；Development Player 位于
`Builds/WindowsDevelopmentG26/AzureSword.exe`。Release、正式字体/音频/资产与目标硬件视觉可读性不是
本包 PASS 项。

## 9. 回滚与故障路径

- Runtime Host 可回退到旧 Host 以定位展示故障，但已发布的 Application API 不得删除。
- Settings 3 写入后旧 Codec 不得覆盖；回滚必须显式恢复 v2 备份。
- 页面失败回到最近安全页；保存失败保留冻结结果与 Recovery，明确重试不会重复结算。
- 设备断连只改变暂停/焦点，不销毁 Run；重连不自动恢复 Gameplay，避免意外操作。

## 10. G2.7 边界

G2.7 只在现有 Presentation/UI 安全接口上补程序化角色、敌人、地图、技能、预警、VFX Pool 与音频
占位表现。不得替换单一 Canvas、复制输入路由、让 View 写 Simulation、导入来源不明资源，或提前把
Placeholder 标为 Release。表现事件必须来自只读快照/事件流并保持 30 Hz 模拟确定性。
