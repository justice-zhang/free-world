# G2.7 程序化 Placeholder 表现、池与音频

- 工作包：G2.7
- 对应模块：M13 表现、资产与音频
- 分支：`codex/qinglan-demo-implementation`
- 决策：CR-2026-018、ADR 0025
- 输入：G2.6 单一 Host/Canvas/Input、Schema 6 稳定 Profile ID、Settings 3

## 1. 范围与非范围

本工作包把已可玩的 Demo 从同质方块提升为可自动辨认的程序化垂直切片：玩家、普通敌人、Boss、
Projectile、Area、Pickup、Affix、地图目标和危险信号都保留颜色之外的形状/方向通道；VFX、音频源和
伤害数字具有固定容量与可观测降级；四路音量、暂停/故事/Boss 混音、低闪和五种色觉模式统一生效。

不导入 Sprite、动画、材质、音乐、音效、字体或第三方包；代码生成测试音和纹理仍是
`development-only` Placeholder。正式资产、专用 Audio/Vfx Profile、目标硬件 GPU/Overdraw 和人工最坏
混战可读性属于 G3，不得由本工作包的 Null Device 或自动测试冒充。

## 2. 所有权与依赖

```text
ContentRegistry(kind/tag/delivery/profile id)
            |
            v
QinglanProceduralPresentationFactory ----> ProceduralPresentationCatalog
            |                                      |
RuntimeMapDefinition -> ProceduralMapConfiguration |
                                                   v
RunSession stable-id queries -> PresentationCoordinator -> bounded View/VFX/Audio/Text pools
RunUiSnapshot ----------------------^          |
AccessibilitySettings -------------------------+
```

- Simulation 只返回 active delivery、Affix 和实体对应的稳定 ID，不引用 `UnityEngine`。
- Application 只做 `SpatialEntity` 到稳定 Profile/Overlay ID 的只读桥接。
- Infrastructure 在装配阶段按通用 kind、tag、Delivery 和 Profile ID 构建表现目录与地图 DTO。
- Presentation 是 Sprite、AudioClip、View、效果池和混音的唯一 Owner；不写玩法状态。
- UI 继续使用 G2.6 的一个 Canvas；伤害数字复用共享 Canvas，不创建第二套 HUD。

## 3. 程序化视觉目录

`ProceduralVisualLibrary` 在运行时生成 Square、Circle、Diamond、Triangle、Ring、Cross、Chevron、Hexagon、
Line 九类 32×32 Sprite。轮廓层和最多两个 Affix Overlay 与实体 View 一起池化。

| 语义 | 形状/方向 | 默认优先级 | 稳定身份来源 |
|---|---|---:|---|
| 玩家 | Triangle、朝向 | P1 | Character / Qinglan Profile |
| 普通敌人 | Circle；飞行 Diamond；辅助 Cross | P2 | Enemy VisualProfile |
| 爆炸种囊 | Ring | P0 | Enemy tag |
| Boss | Hexagon、朝向 | P0 | Enemy VisualProfile / Boss tag |
| 玩家投射物 | Diamond/Chevron | P1 | Skill Delivery PresentationId |
| 敌方范围/冲锋线 | Ring/Line | P0 | Skill tag＋Delivery |
| Pickup/Reward/Relic | Cross/Diamond | P1 | Pickup/ContentId |
| Affix | Ring/Cross/Chevron/Hexagon 外框 | P1 | 实例 Affix ID |

缺 Profile 的 Development 路径使用按 `EntityKind` 的安全 fallback 并累计诊断；形状不会因色觉设置变化。
High Contrast 仍根据 hostile/friendly 选择黑白轮廓，低闪或关闭震动不移除 P0 的实体前摇轮廓。

## 4. 地图世界层

`QinglanProceduralMapFactory` 只复制地图范围、9 个障碍、前五个区域锚点和 11 个 Objective/Event/Landmark
稳定 ID/位置。`ProceduralMapPresentation` 生成边界、区域底色、墙体和三类标记，并从
`RunUiSnapshot` 更新隐藏、进度和完成状态。

地图表现不参与 `IsWalkable`、交互距离、奖励、出生或状态转换。开始新 Run 时按 Descriptor MapId
重建，Run 消失时释放；因此不会把上一局标记泄漏到下一局。

## 5. 池、优先级与降级

| 池 | 上限/预热 | 满池策略 | 指标 |
|---|---|---|---|
| Actor/Projectile/Area/Pickup View | 8/16/8/16 起始 | 现有池扩容，Run 结束统一回收 | Active/Fallback |
| VFX | 200 / 32 | P0 驱逐低层或与同形 P0 合并；低层丢弃 | Created/Peak/Drop/Evict/Merge |
| AudioSource | 总计 32：2 循环＋30 瞬态；预热 8，预留 8 P0 | 同类冷却；P0 驱逐低层或合并；普通请求不占预留 | Active/Peak/Drop/Cooldown/Evict/Merge |
| 伤害数字 | 96 / 16 | 超限聚合，暴击延长已有强调项 | Created/Aggregated |

池由 Coordinator 单点推进，无逐效果 `Update`。P0 音频出现时普通提示短时约 -6 dB Duck；普通命中
同类冷却避免密集叠加。所有计数可由测试和性能报告读取，但不影响模拟 Checksum。

## 6. 音频与混音

音频仅由代码生成短正弦测试提示、探索循环和环境循环，不引用磁盘音频。`PresentationMixState` 包含
Gameplay、Paused、Story、Boss；每帧消费 Settings 3 的 Master/Music/Ambience/Effects：

- Gameplay 保留环境和低音量探索层；
- Paused/Story 压低音乐、环境与 Effects，但不依赖 `Time.timeScale`；
- Boss 提升音乐层和危险提示；
- P0 Boss Phase/Danger 优先于 Hit、Pickup、Confirm。

生成测试音用于自动门禁，不是 G3 正式音频质量、响度、循环点或音乐 Stem 验收证据。

## 7. 生命周期

1. Host 初始化一次目录、共享 Sprite Library、四类 View Pool、VFX/Audio/Text Pool。
2. Session 变化先 `Clear` 全部 View/瞬态状态，再按 MapId 设置程序化地图。
3. 每个 Render Snapshot 只 Acquire/Apply/Release；只在首次绑定解析 Profile/Affix。
4. Combat Events 转为瞬态请求；RunUiSnapshot 只触发乘风档位/Boss 阶段和地图低频更新。
5. Host 销毁时释放 View、VFX、AudioSource、生成 AudioClip/Sprite/Texture、地图根和事件订阅。

## 8. API、存档与回滚

- Simulation：新增 3 条只读查询；Application：新增 1 条 Overlay 查询；删除 0。
- Content Schema 6、Profile 3、Settings 3、Recovery 2 和固定 Tick 均不变。
- 不保存 Sprite、AudioClip、EntityHandle、运行时索引或解析后的 Profile。
- 回滚可停用目录/地图并保留新增 API；旧 `VisualProfile`、VFX 和 Audio 调用兼容入口仍存在。

## 9. 测试矩阵

| 检查 | 自动证据 |
|---|---|
| Profile | 玩家/Boss/Boss Area 形状、优先级、High Contrast；真实 delivery 稳定 ID |
| Map | 96×72 范围、9 障碍、5 区域、11 状态标记 |
| VFX | 固定容量、低层驱逐、P0 合并、稳态 Tick 0 B |
| Audio | 预留、冷却、丢弃、驱逐、合并、上限 |
| Text | 固定容量与聚合 |
| PlayMode | Bootstrap 真实 Run、地图、玩家轮廓、色觉切换、池上限、销毁 |
| Regression | 全量 EditMode/PlayMode、Validation/API、性能短测、Development Build/Smoke |

## 10. G2.8 输入

G2.8 只做完整垂直切片统一门禁、12 分钟实际流程和可读性审查，不扩 Content/Save Schema，不导入 G3
正式资产。若自动压力或人工评审发现 P0 被遮挡，先调整表现优先级、形状、排序或池降级，不修改模拟命中。
