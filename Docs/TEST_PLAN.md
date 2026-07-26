# 测试计划

## 1. EditMode

必须覆盖：

- ContentId 格式、相等、序列化和重复检测

- 内容包依赖拓扑排序与循环检测

- 作者数据烘焙与引用解析

- EntityHandle Generation 安全

- 固定 Tick 调度顺序

- RandomStream 固定种子复现

- 空间网格插入、查询、移动和删除

- 属性修正顺序

- 暴击、护甲、护盾、生命结算

- 状态叠层、刷新、替换和独立实例

- ProcDepth 限制

- 技能 LevelPatch

- 进化与联动条件

- 升级候选池和固定种子

- 存档原子写入、校验和与迁移

- 缺失内容恢复

## 2. PlayMode

完整流程：

> Bootstrap  
> -\> Main Menu  
> -\> Character Select  
> -\> Map Select  
> -\> Load Run  
> -\> Move  
> -\> Kill Enemy  
> -\> Collect XP  
> -\> Open Level Up  
> -\> Select Upgrade  
> -\> Pause  
> -\> Resume  
> -\> End Run  
> -\> Show Result  
> -\> Save Profile

还需验证：

- 手柄完成完整流程

- UI 打开时 Gameplay 输入被禁用

- 暂停只停止模拟

- View 释放后无悬空绑定

- 缺失正式资源时使用程序化占位

- 伪本地化无明显裁切

## 3. Headless Soak Test

- 固定种子

- 自动移动和升级

- 连续运行 30 分钟

- 检查 NaN、无效句柄、未清理事件、实体上限和内存趋势

- 输出性能 JSON

## 4. 扩展性验收

创建以下内容时不修改核心程序集：

> test.character.second  
> test.skill.second  
> test.map.second

只通过内容资产、地图场景和烘焙工具完成。验证它们出现在选择界面、可运行、可保存并通过内容验证。

## 5. 测试纪律

- 修复 Bug 前先添加能复现问题的失败测试。

- 不允许通过删除测试或放宽断言完成修复。

- 测试名称说明行为，不只说明方法名。

- 随机测试必须记录种子。

- 无法在自动化环境运行的项目，必须提供可重复的手工步骤和日志，但不得声称自动测试已通过。

## 6. M1 已落地覆盖

- `ContentId` 有效/无效、大小写规范化、字符串序列化和已知 Hash 碰撞。
- Pack 稳定拓扑排序、缺失依赖、循环和依赖版本不兼容。
- 作者 ScriptableObject 烘焙、纯运行时字段审计、JSON round-trip 和确定性 Hash。
- 重复 ID 同时报告两侧来源，缺失引用报告 owner ID、Pack 和资产路径。
- 非 canonical 被引用资产仍报告该资产自身路径；运行时集合不暴露可变 backing array。
- Registry 同加载顺序索引稳定，并接受无需类型分支的新定义子类。
- Bootstrap 加载一个测试 Pack、四个定义并进入空 MainMenu。

## 7. M2 已落地覆盖

- 30 Hz 固定 Tick 在不同表现 Delta 切分下得到相同 Tick 数和运动结果。
- 最大追赶 Tick 保留积压；暂停忽略 Delta；暂停单步恰好推进一次。
- 同一次追赶 Advance 保留所有已执行 Tick 的事件，零 Tick Advance 不提前清空。
- 任意非零速度都会积分位置并设置 Moving，不用阈值冻结合法运动。
- 删除后旧 Handle 失效，Slot 复用时 Generation 改变，旧 Handle 不能读写新实体。
- Swap-back 后被移动实体的 Handle 仍解析到正确状态；Store 扩容和 Free List 复用。
- M2 默认 Pipeline 的四个系统顺序和自定义测试 Pipeline 的实际调用顺序。
- Spatial Grid 半径查询与暴力结果一致，并覆盖跨 Cell 更新、删除和邻近查询。
- 相同 RandomStream 种子重复、派生流不受父流调用顺序影响。
- 命令只由 Cleanup 应用，生命周期删除同步 Store、网格、事件和诊断计数。
- RenderSnapshot 保存前后位置、朝向、状态标记并可插值。
- Headless Harness 固定种子摘要重复，并验证不创建 GameObject。

## 8. M3 已落地覆盖

- 14 个稳定 StatId 与 Runtime StatIndex 映射；Modifier 六阶段顺序、Priority、紧凑
  StackingGroup、同优先级最新项和过期回退。
- 属性 Evaluate 热路径 IL 不调用 ContentId/string 比较，也不包含正常路径托管分配指令。
- Actor slot 复用战斗记录及 Stat/Modifier/Status 数组；默认零生命初始化被原子拒绝。
- 暴击、Armor、Resistance、True Damage、单包边界、Shield→Health 顺序和固定种子复现。
- 无效目标安全失败、ProcDepth 截断计数、同实体多次致死只发一个 EntityDied。
- RefreshDuration、AddStacks、ReplaceIfStronger、IndependentInstances 四种状态策略。
- 状态周期、短持续边界、过期、驱散、免疫、死亡后不 Tick 和周期 ProcDepth 截断。
- 状态行为来自 RuntimeStatusDefinition，申请 API 不允许覆盖；非法/非有限行为安全拒绝。
- 临时护盾刷新不重复扩容；过期回收容量，即使当前护盾已耗尽也产生包含容量差值的
  ShieldChanged；有限容量聚合溢出时原子拒绝申请。
- Tick 内结构体事件在 catch-up 批次累积，下一实际批次清空；自定义 Pipeline 不会漏 Flush。
- Schema 1/2 兼容、Status 作者数据/DTO round-trip、稳定 wire token、确定性 Hash、非法字段
  验证和 Runtime Definition 无 Unity Object。

完整 30 分钟 Soak 和 1,500/3,000/5,000 实体压力 JSON 仍按性能预算在 M10 门禁启用；
M3 未引入 Jobs、Burst 或新的第三方运行时依赖。
