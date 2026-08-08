# ADR 0024：G2.6 单一 UI 组合根、UI-safe Run 投影与 Settings Schema 3

- 状态：Accepted
- 日期：2026-08-09
- 决策人：依据用户当前连续 Demo 开发与自行决策授权
- 关联里程碑：G2.6、M12、M14
- 关联 CR：CR-2026-017
- 承接：ADR 0010、ADR 0015、ADR 0022、ADR 0023

## 背景

G2.5 已交付 Run、Profile、Meta 和持久化真值，但 Bootstrap 仍由旧 M7 Host 展示框架页，缺少完整 Qinglan
页面、地图/构筑/Boss HUD、两种输入闭环和 Settings 3 可访问性。M12 明确禁止 UI 读取 Simulation Store，
并要求升级/奖励 UI 只展示 Owner 已筛选候选，不自行重算资格。

## 决策

### 单一运行时组合根

`QinglanDemoRuntimeHost` 成为 Bootstrap 唯一的 UI/Input/Presentation 生命周期 Owner。它组合：

```text
Input System → QinglanDemoPresenter → QinglanDemoFlowController
                                      ↓
                  DemoRunCoordinator / QinglanProfileCoordinator
                                      ↓
                RunUiSnapshot + Localization Key → shared Canvas
```

Gameplay 与 UI Action Map 互斥；Debug Map 仅在 `Debug.isDebugBuild` 启用。键盘、鼠标和手柄进入同一命令
入口。手柄移除/重连触发焦点恢复；若发生在 Active Run，先进入 UserPaused。页面命令执行后立即重算
Action Map，避免覆盖层关闭后的单帧输入泄漏。

### UI-safe 投影

Application 追加固定容量、可复用的 `RunUiSnapshot`，由 `RunSession` 一次复制生命/护盾/XP、风乘机制、
Boss、构筑、目标、事件和地标状态。UI 只保存这份纯值缓冲和 Localization Key；它不引用
`SimulationWorld`、Store、EntityHandle、Scene 或 Prefab。地图/交互输入仍通过 RunSession 命令进入合法
Owner，候选、显化资格、Boss 阶段和 Profile 合并均不在 UI 重算。

### 页面、卡牌与可访问性

程序化 Placeholder 使用单 Canvas，分离低频页面、HUD 与危险提示层。完整路由覆盖标题/档案、角色、地图、
Loadout、加载、Run HUD、升级、奖励、暂停、设置、故事、结算、据点设施、收藏和内容错误；配置应用前
显示确认页。卡牌显示本地化行为描述、目标等级、类型标签、构筑关系和显化资格。

危险信息固定保留形状、方向和文本，不依赖颜色、震动、闪光或伤害数字。Settings 3 保存 100/125/150%
字体、五种色觉模式、四路音量和字幕，并保留原死区、震动、屏幕震动、闪光、伤害数字、自动瞄准和重绑。

### Save 与公开 API

Settings 版本由 2 升至 3；v2→v3 使用 100% 字体、标准色觉、四路 100% 音量、字幕开启。旧 Settings
构造函数保留并委托完整构造，源码消费者无需修改。API Freeze 规范审计：Game.Application 从 523 增至
589 条，追加 67 条，唯一移除行是常量值 `SettingsCurrentVersion=2` 被批准替换为 3；其余四个冻结程序集
逐字节不变。

## 兼容与影响

- 不改变 30 Hz Simulation Tick、程序集方向、Content Schema 6、Profile 3 或 RunRecovery 2。
- Settings v1→v2→v3 连续迁移；未知未来版本仍拒绝读取，不静默降级。
- UI Snapshot 公开类型只含纯值和稳定 ID 字符串；固定容量在构造时分配，刷新复用。
- 本地化固定 Key 由 en/zh-Hans 生成，Pseudo 由英文表派生；OS CJK 字体仅是 Placeholder fallback。
- 正式 UI、美术、字体、音频、VFX 与 Release 可读性仍须 G3 provenance/验收，G2.6 不导入资产。

## 被拒绝的方案

- UI 直接读取 `SimulationWorld`：破坏 Owner 边界并让 UI 成为第二真值。
- 每帧创建 ViewModel/List/字符串：违反 UI 性能预算和稳态 GC 门禁。
- 每页一个 Canvas：增加重建和批次成本，且不利于统一字体/色觉缩放。
- 用颜色单独表达 Boss/危险：在色觉模式和低闪光下丢失关键战斗信息。
- 保存到 PlayerPrefs：绕过 Save Schema、迁移、校验、备份与原子写。

## 迁移、回滚与测试

迁移由独立 Settings 文档执行，不触碰 Profile/Recovery。回滚 Runtime Host 可恢复旧 M7 Host，但新增公开
API 不得删除；回滚 Settings 写入必须恢复 v2 备份，不能让旧 Codec 覆盖 v3。测试必须覆盖 Settings 迁移、
全部 Action/绑定和冲突、UI 不引用 Simulation、复用快照 0 B、三 Locale/CJK/三档缩放、颜色与形状通道、
键盘和手柄分别完成闭环、断连暂停/焦点、完整 EditMode/PlayMode、Project Validation、API Freeze、性能
短测、内容双构建及 Windows x64 Development Build。
