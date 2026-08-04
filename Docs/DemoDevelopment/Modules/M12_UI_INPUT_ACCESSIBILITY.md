# M12 UI、输入与可访问性

## 1. 信息架构

Demo 页面：标题/档案、角色选择、地图选择、Loadout、加载、Run HUD、升级、奇物/显化选择、暂停、
设置、故事覆盖层、结算、据点四设施、内容错误。所有文字来自 Localization Key。

## 2. HUD

| 区域 | 内容 | 数据源 |
|---|---|---|
| 左上 | 生命/护盾、等级、XP | UI-safe Run Snapshot |
| 上中 | 时间、阶段/Boss 生命、目标提示 | Application/Boss/Objective ViewModel |
| 右上 | 小地图/风脉台/事件/地标线索 | Map UI Snapshot |
| 下方 | 本命器、3 武器、4 心诀、3 奇物 | Build ViewModel |
| 角色附近 | 状态/危险方向/交互提示 | Presentation Request |
| 乘风 | 四档环、当前进度、降档反馈 | CharacterMechanic ViewModel |

伤害数字可关闭并共享 Canvas；Boss 高危提示不依赖伤害数字。

## 3. 升级与奖励卡

每张卡显示名称、当前/目标等级、行为变化、标签、与当前构筑关系、显化资格和冲突。不得只写百分比。
禁用项不进入候选；宝匣无合格显化时显示确定性 fallback，而非空白卡。

## 4. 输入

| Action Map | 主要动作 | 启用页面 |
|---|---|---|
| Gameplay | Move、Map、Pause、Interact | InRun；弹窗时禁用 |
| UI | Navigate、Submit、Cancel、Tab、Page | 非 Run 页面/弹窗 |
| Debug | 测试升级、结束 Run、诊断 Overlay | Development only；Release 禁用 |

键鼠：WASD/方向键、鼠标/键盘 UI；手柄：左摇杆、D-pad、标准确认/取消。通用攻击/闪避键不加入。
重绑冲突必须提示；断开手柄时保持暂停/页面焦点。

## 5. 可访问性

最低设置：重绑、摇杆死区、震动强度、屏幕震动、闪光强度、伤害数字、自动瞄准、字体大小、色觉
区分、主/音乐/环境/音效音量、字幕。现有 Settings 缺字体大小、色觉和音量字段，属于 M14 Save
Schema 评审项。

危险信息至少使用两种非颜色通道：形状/方向/纹理/音效。低闪光不能移除前摇边界；关闭震动不影响
输入；自动瞄准策略只改变 Targeting 输入，不产生额外伤害真值。

## 6. UI 性能

- 单共享 Canvas，静态/高频层分离；
- 升级/据点列表使用复用元素，不每帧创建；
- 小地图只消费低频地图快照；
- 数字与计时格式化在低频 Presenter 层，Simulation 无字符串；
- 伤害数字聚合/池化，达到上限按非关键优先级丢弃并计数。

## 7. 错误和空状态

- 缺内容/本地化/Profile：显示稳定错误码和可恢复操作；
- 无候选：自动 Skip 并显示短提示；
- 存档失败：明确“未保存”，提供重试；
- 空收藏专题：显示获取线索，不显示随机概率假信息；
- 手柄焦点永不落入不可见/禁用控件。

## 8. 测试与验收

| 类型 | 必须覆盖 |
|---|---|
| EditMode | ViewModel 不引用 Simulation Store/Unity Object；Key 非空 |
| PlayMode | 键鼠/手柄完整闭环；弹窗禁 Gameplay；焦点恢复 |
| Localization | zh-Hans/en/Pseudo、长文本、字体、100%/125%/150% UI Scale |
| Accessibility | 色觉、低闪、无震动、伤害数字关闭仍可通关 |
| Performance | HUD/小地图/伤害数字池无稳态 GC |
| Release | Debug Map 不可用，开发诊断不可触发胜利 |

退出条件：两种输入完成全流程，所有关键战斗信息可在主要可访问性组合下辨识，UI 不拥有玩法真值。
