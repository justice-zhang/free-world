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
