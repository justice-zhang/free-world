# ADR 0025：G2.7 程序化 Profile、优先级池与表现身份桥接

- 状态：Accepted
- 日期：2026-08-09
- 决策人：依据用户当前连续 Demo 开发与自行决策授权
- 关联里程碑：G2.7、M13
- 关联 CR：CR-2026-018
- 承接：ADR 0004、0009、0012、0024

## 背景

G2.6 已交付单一 Host/Canvas 与可操作闭环，但 M7 fallback 仍把绝大多数实体画成同类方块，VFX/Audio
缺少 P0—P3 优先级、固定上限与 Settings 3 混音，地图目标和 Boss/机制切换也没有程序化世界表现。
Content 已保存稳定 Visual/Presentation Profile ID，当前桥接没有完整消费。

## 决策

- 保持 `QinglanDemoRuntimeHost`、`PresentationCoordinator` 和共享 Canvas 为唯一 Owner。
- Infrastructure 在 Run 装配低频阶段从 Registry 的 kind、tag、Delivery 和稳定 Profile ID 构建
  `ProceduralPresentationCatalog`；新增内容只需合法 Profile/Tag，不按具体 Qinglan ID 写分支。
- Simulation/Application 只追加稳定 ID 查询；不暴露 Store、Unity Object 或可变技能/词缀记录。
- 程序化 Sprite Library 提供 circle/diamond/triangle/ring/chevron/cross 等轮廓；玩家、敌人、Boss、
  Pickup、Projectile、Area 和 Affix Overlay 由集中 View Pool 绑定，View 不计算命中。
- VFX 固定 200 同时，AudioSource 总计固定 32（2 个循环、30 个瞬态）且瞬态预留 8 个 P0 通道；P0 在满池时合并或驱逐低优先级，
  不静默丢弃。P2/P3 可采样/聚合并记录计数。
- Master/Music/Ambience/Effects、低闪、色觉、伤害数字、屏幕震动统一消费 Settings 3。危险始终保留
  轮廓、方向或纹理；关闭闪光/震动不缩短 P0 前摇。
- Map Presentation 只读取 RuntimeMapDefinition 与 `RunUiSnapshot`，程序化生成边界、障碍、锚点和
  Objective/Event/Landmark 状态，不参与 Walkable、距离或奖励判定。

## 兼容与回滚

不改变 Assembly 方向、Content Schema 6、Settings/Profile/Recovery 版本、30 Hz Pipeline 或稳定 ID。
旧 `VisualProfile` 与旧 VFX/Audio 调用保留；缺 Profile 继续安全 fallback。回滚时可停用程序化 Catalog/
Map Director，但公开 API 追加不得删除。

## 测试

Focused EditMode 覆盖 Tag 驱动 Profile、全部 Shape、五色觉、Delivery/Affix/Pickup 桥接、P0 池策略、
音频并发/冷却/混音、地图状态和稳态分配。PlayMode 覆盖真实 Run 的玩家/六敌/Boss/技能/目标表现、
暂停/故事混音、Settings 组合与销毁清理。最终重跑全量测试、Validation/API、目标规模短测和 Windows
x64 Development Build；正式 GPU/音频质量与 Release 保持 NOT RUN 至 G3。
