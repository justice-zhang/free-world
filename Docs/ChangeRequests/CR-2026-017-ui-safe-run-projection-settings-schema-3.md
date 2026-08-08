# Change Request：UI-safe Run 投影与 Settings Save Schema 3

- 编号：CR-2026-017
- 状态：Implemented
- 提交日期：2026-08-09
- 提交人：Codex
- 目标里程碑：G2.6、M12、M14
- 关联 ADR：ADR 0024

## 1. 变更摘要

为完整 Demo 页面、HUD、输入和可访问性新增可复用的 Application 层 Run UI 快照，并把 Settings
Save 从 Schema 2 升至 3，保存字体缩放、色觉模式、四路音量和字幕。UI 只消费纯值投影，不读取或持有
Simulation Store。

## 2. 触发场景

- 用户或设计需求：M12 要求完整 HUD、键鼠/手柄闭环和最低可访问性设置。
- 当前限制：Settings 2 缺少字体、色觉、音量、字幕；旧快照不能表达 Boss、构筑和地图进度。
- 可复现示例：重启后 150% 字体与高对比模式丢失，或 HUD 为获取 Boss 阶段而直接查询 Simulation。

## 3. 现有模块为何不足

现有 `RenderSnapshot` 面向实体表现，`GameState` 面向页面状态；两者均不能同时提供低频 HUD 所需的生命、
护盾、升级、Boss、风脉、目标、事件、地标和构筑纯值。把这些规则复制到 UI 会形成第二份玩法真值。
Settings 2 的固定字段也无法通过 Trait、内容定义或适配器组合表达用户设备偏好。

## 4. 提议方案

- 新增或修改的模块：`RunUiSnapshot`、`RunSession.CaptureUiSnapshot`、Settings 2→3 Migration、
  `AccessibilitySettings`。
- 公共 API：追加可复用 UI 快照、交互 held 命令、Settings 3 字段与 setter；保留 Settings 2 构造函数。
- 数据结构：Settings 3 新增 `fontScale`、`colorVision`、四路 volume、`subtitlesEnabled`。
- 注册方式：不新增内容类型；所有 HUD 内容名称继续使用稳定 ContentId 解析 Localization Key。
- 编辑器工作流：M8 Setup 生成 en、zh-Hans、Pseudo UI Key，Project Validation 校验三 Locale。

## 5. 影响分析

| 领域 | 影响 |
|---|---|
| Assembly 依赖 | Application 追加纯值投影；Game.UI 仍只引用 Application/Unity |
| Content Schema | 不变，仍为 6 |
| Save Schema | Settings 2→3；Profile 3、RunRecovery 2 不变 |
| Addressables | Bootstrap 引用既有 Qinglan Catalog；无正式资产 |
| 性能 | 快照固定容量复用，10 Hz UI 投影，稳态 0 B 分配门禁 |
| 测试 | 迁移/round-trip、UI-safe 边界、两种输入闭环、焦点、Locale、构建 |
| 平台 | 无新平台 API；设置仍由 SaveCoordinator 原子保存 |
| 资产与许可 | 仅程序化 Canvas 与 OS 字体 fallback，无外部资产 |
| 兼容性 | 兼容读取 v1/v2，旧构造函数保留，新字段使用安全默认值 |

## 6. 备选方案

把 HUD 直接绑定 `SimulationWorld` 较少代码，但会破坏程序集方向、产生 UI 玩法真值并让测试依赖 Scene；
把新设置只存 PlayerPrefs 会绕过按 kind 的校验、迁移和原子存档，因此均拒绝。

## 7. 迁移与回滚

- 迁移步骤：v1→v2 后继续 v2→v3；新增字段默认字体 100%、标准色觉、四路 100%、字幕开启。
- 旧数据处理：保留 Locale、死区、震动、屏幕震动、闪光、伤害数字、自动瞄准和重绑。
- 回滚步骤：停止写 Settings 3 时可保留 v2 备份；已写 v3 文件不得被旧版本静默覆盖，须显式恢复备份。

## 8. 验收标准

- [x] 新机制为所有角色、地图、Boss 和构筑复用
- [x] UI 不持有 Simulation Store 或 Unity Object 玩法真值
- [x] Settings v1/v2→v3 与完整 round-trip 有自动测试
- [x] HUD 快照复用稳态 0 B 分配
- [x] 键盘和手柄分别完成标题→Run→结算→据点→再次出发
- [x] 文档和 ADR 已更新

## 9. 审批

- 技术负责人：依据用户连续开发、全部自行决策和后续免确认授权
- 内容负责人：不改变 Content Schema 或正式内容
- 制作人：依据用户当前明确授权
- 结论：Implemented；最终 EditMode、PlayMode、Project Validation、API Freeze、性能短测与 Windows
  x64 Development Build 均有 PASS 证据
