# M13 表现、正式资产与音频

## 1. 方向

清朗东方幻想：阳光废墟、草木生长、风脉、旧剑与生活痕迹并存。危险使用清晰层级，但不把青岚旧庭
做成阴沉尸骸/末日场景。角色、敌方危险、可拾取物和环境色域要分离。

## 2. 资产清单

| 类别 | 最低范围 |
|---|---|
| 角色 | 陆青野移动/受击/倒下/胜利/四档风势；轮廓和头像 |
| 武器/VFX | 六武器基础＋六显化＋状态＋命中/死亡/拾取 |
| 敌人 | 六普通、四 Affix Overlay、折枝、听风三阶段 |
| 地图 | 五区域 Tile/Props、三风脉台、三事件、五地标、Boss 场 |
| UI | 标题、HUD、卡牌、据点、图标、色觉替代、光标/焦点 |
| 叙事 | 三故事演出所需立绘/场景元素；不额外批量制作完整版内容 |
| 字体 | 商业嵌入许可明确的中英字体与 fallback |

正式资源不从参考开源游戏、旧项目或来源不明素材改造。AI 资源不得把参考项目素材作为输入。

## 3. Profile 契约

VisualProfile：Sprite/Animator/Material/Scale/Sort/VFX references；AudioProfile：事件到 Audio Address；
VfxProfile：预热、容量、优先级、降级、色觉变体。Simulation 只持稳定 Profile/PresentationId。

缺 Profile：Development 使用程序化方形/测试音并告警；Release 由验证器阻断，不能静默 fallback。

## 4. VFX 预算

| 优先级 | 例子 | 降级 |
|---|---|---|
| P0 危险 | Boss 前摇、种囊爆炸、震地、冲锋线 | 不丢；降低非关键层 |
| P1 机制 | 风势档位、标记、潮汐相位、风脉台 | 合并重复粒子，保留轮廓 |
| P2 命中 | 普通命中、暴击、状态 Tick | 采样/聚合 |
| P3 装饰 | 草叶、尘、远景风 | 远处降频/关闭 |

玩家攻击和敌方危险颜色/形状分离。透明叠加达到阈值时先降 P3/P2，不改变模拟命中。

## 5. 音频层

| 层 | 内容 |
|---|---|
| 环境 | 风、草木、旧庭木石、远钟 |
| 探索音乐 | 清朗、留白、地域主题 |
| 常态战斗 | 增加节奏但保留环境辨识 |
| 高压 | 10:30 后增层，不完全替换主题 |
| Boss | 折枝/听风独立层和阶段 Stem |
| 机制/危险 | 乘风档、Affix、Boss 高危、显化、拾取 |

高危提示优先于普通命中；同类音效有并发上限和冷却。设置支持独立音量，暂停/故事使用 Snapshot 混音，
不依赖 `Time.timeScale` 唯一控制。

## 6. 池和生命周期

Actor/Projectile/Area/Pickup View、VFX、AudioSource、伤害数字全部池化并由集中 Coordinator 推进。
Scene/Run/Phase 所有权明确；阶段结束清理 Boss-owned 表现，Run 结束释放 Addressables 句柄和订阅。

## 7. Provenance

每个正式文件记录：Owner、来源类型、工具/模型版本、日期、提示词、输入引用、人工修改、许可证、
允许平台/用途、源文件 Hash、输出 Hash、审核人。Hash 不一致、来源缺失、Third Party 未登记均阻断 Release。

## 8. 实机验证

M10 Null Device Render 数据不能替代正式资产 GPU 基线。G3 在最低/推荐目标硬件测量 CPU/GPU Frame、
Overdraw、Batches、SetPass、显存、加载峰值、Shader Variant、池和 GC；保存捕获和设备信息。

## 9. 测试与验收

| 检查 | 必须证据 |
|---|---|
| Validation | Profile/Address/Label/Provenance/License/Hash |
| PlayMode | 四类 View、阶段切换、Scene 释放、缺资源 Development fallback |
| Readability | 600—1200 敌人最坏构筑＋Boss P0 危险可读 |
| Accessibility | 低闪/色觉/无震动/低伤害数字组合 |
| Audio | 并发上限、Duck、阶段 Stem、危险提示 |
| GPU | 目标机 Frame Debugger/Profiler/捕获报告 |

退出条件：正式视听有完整来源，危险层级在高压场景可读，所有高频表现池化且 Release 无 Placeholder。
