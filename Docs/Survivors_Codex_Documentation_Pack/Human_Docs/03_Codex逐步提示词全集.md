**Codex 逐步提示词全集**

总控提示词、M0-M10、审查、修复、内容新增和发布审计

版本 1.0 \| 2026-07-24

用于新建独立、可商业化、可持续扩展的 Unity 类幸存者游戏框架

版本：1.0

日期：2026-07-24

## 使用说明

先提供总控提示词，再提供一个当前任务提示词。每个里程碑完成后，再提供里程碑审查门禁提示词。不要一次性把 M0-M10 全部要求 Codex 实现。

# Codex 总控提示词

你正在实现一个全新的、可商业化的 Unity 类幸存者游戏框架。

## 项目性质

项目不是任何开源游戏的 fork。参考项目只能用于理解架构思想，不允许导入或复制其美术、音频、字体、Prefab、Scene、动画、材质、Shader、品牌或演示内容。默认不复制参考项目代码；确实引入第三方代码时，必须先登记来源、许可证和改动。

## 技术基线

- Unity 版本以 ProjectSettings/ProjectVersion.txt 为准。

- Unity 6 LTS、URP、C#。

- Windows x64/Steam 为第一目标平台。

- Input System、Addressables、Localization、Unity Test Framework。

- Unity Collections、Jobs、Burst 只用于经过测量的热点。

- 不添加未经批准的第三方运行时包。

- 不使用 DOTween、Odin 或第三方 DI 框架。

- 不使用 Resources.Load、GameObject.Find 或 FindObjectOfType 解决架构问题。

## 架构原则

1\. 使用模块化单体架构。

2\. 模拟层不依赖 GameObject、Scene、Prefab、Sprite、AudioClip 或 Steam。

3\. 内容由 ScriptableObject 作者数据经过验证和 Baker 转换为运行时定义。

4\. 高频逻辑使用 RuntimeContentIndex；存档使用稳定 ContentId。

5\. 技能由 Trigger、Targeting、Delivery、Effect、LevelPatch 组合。

6\. 构筑由标签、属性修正、Synergy 和 Evolution 形成，不建立硬编码 Build 类。

7\. 新角色、新技能、新敌人和新地图在使用已有模块时不应修改核心代码。

8\. 新底层机制通过显式注册模块扩展。

9\. 表现层通过快照读取模拟状态，并向模拟层提交命令。

10\. 平台功能通过 IPlatformFacade 隔离，编辑器使用 NullPlatformFacade。

11\. 正式资源和 Placeholder 严格分组。

12\. 正式 AI 资源必须有 provenance。

13\. 所有用户可见文字必须使用本地化 Key。

14\. 存档必须版本化、原子写入并支持迁移。

## 代码规则

- 为程序集创建 asmdef，禁止循环依赖。

- Game.Core 不引用 UnityEngine。

- Game.Simulation 不引用场景或表现资源。

- 不在高频路径使用 LINQ、反射、字符串格式化或临时集合。

- 运行期间不使用 Instantiate/Destroy 创建高频对象。

- 使用构造函数和 Composition Root 传递依赖。

- 不引入全局 Service Locator。

- 公共 API 提供简明 XML 文档。

- 重大架构变化增加 ADR。

- 每个任务必须包含测试。

- 每次只实现指定里程碑，不提前实现后续内容。

- 不生成正式美术，只创建程序化 Placeholder。

- 不创建大量正式角色、技能或敌人，只创建测试夹具。

## 固定工作流程

1\. 阅读 AGENTS.md、Docs/ARCHITECTURE.md、Docs/CODEX_WORKFLOW.md 和当前里程碑提示词。

2\. 检查现有代码、Git diff 和测试。

3\. 先运行基线测试并记录结果。

4\. 输出简短实现计划。

5\. 实现当前里程碑。

6\. 增加或更新测试。

7\. 运行编译、EditMode、PlayMode、内容验证和适用的构建。

8\. 更新受影响文档和 ADR。

9\. 输出修改文件、命令、真实结果、限制和下一步前置条件。

## 真实性要求

- 未实际运行的命令必须明确写“未运行”。

- 测试失败时不得宣称完成。

- 不得伪造日志、编译结果或性能数据。

- 不得通过注释测试、删除断言或绕过验证器完成任务。

现在只执行我随后指定的一个里程碑。

# 人工预检步骤：在调用 Codex 前执行

这不是让 Codex 自动完成的里程碑。负责人先完成环境和仓库准备。

## 步骤

1\. 安装团队选定的 Unity 6 LTS 版本。

2\. 使用 URP 模板创建空项目。

3\. 打开项目一次，等待所有包和 Shader 导入完成。

4\. 关闭编辑器，初始化 Git。

5\. 添加适用的 Unity .gitignore。

6\. 提交空工程基线，标签 pre-framework-baseline。

7\. 把 Repository_Docs/ 内容复制到仓库根目录。

8\. 填写 Templates/PROJECT_VARIABLES.md，并把确认后的变量同步到仓库文档。

9\. 设置环境变量 UNITY_PATH 指向 Unity Editor 可执行文件。

10\. 手工验证以下命令能启动批处理模式：

> & \$env:UNITY_PATH -batchmode -nographics -quit -projectPath . -logFile -

11\. 建立 main 保护规则和 milestone/\* 分支约定。

12\. 给 Codex 提供 00_MASTER_CONTROL.md，然后开始 M0。

## 预检完成标准

- 空工程可打开。

- Git 工作区干净。

- Unity CLI 可调用。

- 准确 Unity 版本已记录。

- 文档已进入仓库。

- 未导入任何参考项目资源。

# M0：干净工程与工程治理

## 目标

建立能够编译、测试、验证和构建的 Unity 空框架。不要实现正式玩法。

## 前置条件

- 人工预检已完成。

- Unity 版本已记录，Git 基线干净。

## 开始前

1\. 阅读 AGENTS.md、Docs/ARCHITECTURE.md、Docs/CODEX_WORKFLOW.md。

2\. 阅读本提示词列出的相关文档。

3\. 检查当前 Git diff，确保没有未说明的改动。

4\. 运行现有测试并记录基线。

5\. 输出不超过 10 条的实现计划，然后开始修改。

## 必须交付

1\. 建立目录：

> Assets/Game  
> Assets/Tests/EditMode  
> Assets/Tests/PlayMode  
> Assets/GameAssets/Placeholder  
> Assets/GameAssets/AI  
> Assets/ThirdParty  
> Docs/ADR  
> Scripts

2\. 创建 asmdef：

> Game.Core  
> Game.Content.Runtime  
> Game.Simulation  
> Game.Platform.Abstractions  
> Game.Application  
> Game.Content.Authoring  
> Game.Infrastructure  
> Game.Presentation  
> Game.UI  
> Game.Platform.Null  
> Game.Editor  
> Game.Tests.EditMode  
> Game.Tests.PlayMode

3\. 设置依赖，确保 Core 不引用 UnityEngine，Simulation 不引用 GameObject、MonoBehaviour 或场景，无循环依赖。

4\. 只安装并锁定与当前 Unity 版本兼容的官方包：Input System、Addressables、Localization、Collections、Mathematics、Burst、Test Framework。不要添加第三方包。

5\. 创建 GameBootstrapper 和最小 Bootstrap Scene。Bootstrap 只负责 Composition Root、初始化 NullPlatformFacade、记录启动日志并进入空 MainMenu 状态。

6\. 创建程序化占位资源生成器：生成单色圆、方形和线条纹理；输出到 Placeholder；自动附加 placeholder 和 development-only Addressables 标签。

7\. 创建最小验证器：

- Assets/ThirdParty 文件必须有 Third Party 记录。

- 正式 AI 资源必须有 provenance。

- Release 标签不能包含 Placeholder。

8\. 创建命令行脚本：

> Scripts/test.ps1  
> Scripts/validate.ps1  
> Scripts/build-windows.ps1

脚本从 UNITY_PATH 环境变量读取编辑器路径，参数和退出码清晰。

9\. 创建 Windows Development Build 入口，但不实现正式菜单或玩法。

## 必须测试

EditMode：

- 程序集可加载。

- asmdef 依赖符合预期。

- Placeholder 生成器生成正确路径和标签。

- 验证器能发现一个测试违规样本。

PlayMode：

- Bootstrap Scene 可启动。

- GameBootstrapper 只创建一次。

- NullPlatformFacade 可用。

- 启动过程中没有未处理异常。

命令行：

- Scripts/test.ps1 返回正确退出码。

- Scripts/validate.ps1 成功。

- Windows Development Build 可以生成。

## 验收标准

- Unity 工程无编译错误。

- EditMode 和 PlayMode 测试通过。

- Bootstrap 可进入空 MainMenu 状态。

- Windows Development Build 存在且可启动到空菜单。

- Git 中没有参考项目资源。

- 所有 Placeholder 标签正确。

- 文档与实际程序集依赖一致。

## 禁止

- 不创建正式角色、技能、敌人或地图。

- 不实现 Steam、完整存档或构筑。

- 不提前实现 M1 内容模型。

- 不添加第三方插件。

- 不导入外部图片或音频。

## 文档更新

- 填写 ADR 0001 的准确 Unity 版本。

- 更新 Docs/ARCHITECTURE.md 的实际 asmdef 依赖图。

- 在 README 写出命令行测试和构建方法。

## 完成报告

使用 Templates/CODEX_RESULT_REPORT.md，必须包含：

1\. 修改文件清单。

2\. 关键设计和理由。

3\. 实际执行的命令。

4\. 编译、测试、验证和构建的真实结果。

5\. 未完成事项和已知限制。

6\. 下一里程碑前置条件。

# M1：核心类型与内容系统

## 目标

实现稳定内容 ID、内容包、作者数据、烘焙运行时定义、注册表和构建前验证。不要实现战斗。

## 前置条件

- M0 已验收并打标签。

- asmdef、Bootstrap、测试脚本可用。

## 开始前

1\. 阅读 AGENTS.md、Docs/ARCHITECTURE.md、Docs/CODEX_WORKFLOW.md。

2\. 阅读本提示词列出的相关文档。

3\. 检查当前 Git diff，确保没有未说明的改动。

4\. 运行现有测试并记录基线。

5\. 输出不超过 10 条的实现计划，然后开始修改。

## 必须交付

1\. 在 Core 实现：

- ContentId

- ContentTag

- RuntimeContentIndex

- ContentVersion

- 明确的 Result/Error 类型

2\. ContentId 必须：规范化、验证字符、保持原始字符串、可比较、可序列化；不只保存 Hash。

3\. 实现内容包 Manifest、依赖模型、版本检查和拓扑排序。

4\. 实现最小作者数据：

- ContentPackAuthoring

- CharacterAuthoring

- SkillAuthoring

- EnemyAuthoring

- MapAuthoring

本里程碑字段可以最小化，但要为后续扩展保留清晰边界。

5\. 实现对应纯运行时定义和 Baked Catalog。运行时定义不得含 Unity Object。

6\. 实现 ContentBaker：从作者数据生成可验证的运行时 Catalog，并输出内容 Hash。

7\. 实现 ContentRegistry：

- 按稳定 ID 查找

- 加载后分配 RuntimeContentIndex

- 拒绝重复 ID

- 记录来源 Pack

- 不允许静默覆盖

8\. 实现 ContentValidator：ID 格式、重复 ID、缺失引用、依赖缺失、依赖循环、版本不兼容。

9\. 创建一个最小测试内容包，只使用程序化占位资源。

10\. 在 Bootstrap 中加载测试内容包并输出摘要，不进入战斗。

## 必须测试

- ContentId 有效/无效样本、大小写和序列化测试。

- Hash 碰撞不影响稳定 ID 比较。

- 内容包拓扑排序正确。

- 缺失依赖和循环依赖失败。

- 重复 ID 失败且指出两个来源。

- 作者数据可烘焙为不含 Unity Object 的运行时定义。

- 同一输入产生相同内容 Hash。

- Registry 运行时索引稳定于同一次加载顺序。

- Bootstrap 能加载测试包并输出条目数量。

## 验收标准

- 新内容定义不需要修改 Registry 的硬编码列表。

- Baked Catalog 中没有 ScriptableObject、GameObject、Sprite 或 AudioClip 引用。

- 所有错误有可定位的 ID、Pack 和作者资产路径。

- 内容验证可以从命令行运行。

- M0 测试仍全部通过。

## 禁止

- 不实现实体、战斗、技能执行或刷怪。

- 不使用反射扫描自动发现所有类型。

- 不使用 enum 固定所有内容 ID。

- 不允许重复 ID 后按最后加载覆盖。

- 不把 Addressables AssetReference 放入 Simulation Runtime Definition。

## 文档更新

- 更新 Docs/CONTENT_SCHEMA.md。

- 创建或更新内容包 ADR。

- 记录测试包结构和新增内容流程。

## 完成报告

使用 Templates/CODEX_RESULT_REPORT.md，必须包含：

1\. 修改文件清单。

2\. 关键设计和理由。

3\. 实际执行的命令。

4\. 编译、测试、验证和构建的真实结果。

5\. 未完成事项和已知限制。

6\. 下一里程碑前置条件。

# M2：固定 Tick 模拟内核

## 目标

建立与 Unity 表现层隔离的固定 Tick 世界、实体句柄、紧凑 Store、系统管线、空间网格和渲染快照。

## 前置条件

- M1 内容系统已验收。

- 测试运行时定义可以被 Simulation 引用。

## 开始前

1\. 阅读 AGENTS.md、Docs/ARCHITECTURE.md、Docs/CODEX_WORKFLOW.md。

2\. 阅读本提示词列出的相关文档。

3\. 检查当前 Git diff，确保没有未说明的改动。

4\. 运行现有测试并记录基线。

5\. 输出不超过 10 条的实现计划，然后开始修改。

## 必须交付

1\. 实现 SimulationClock 和固定 30 Hz Tick Runner，支持累积时间、最大追赶 Tick、暂停和单步调试。

2\. 实现 EntityHandle(Index, Generation)、Free List、Generation 校验和失效句柄检测。

3\. 实现最小 Store：

- ActorStore

- ProjectileStore

- AreaStore

- PickupStore

使用 Dense Array 和 Swap-back Remove。不要写通用 ECS。

4\. 实现 SimulationWorld、ISimulationSystem 和显式系统 Pipeline。M2 只放最小移动、生命周期、清理和快照系统。

5\. 实现 RandomStream，种子、派生流和调用规则可测试。禁止在 Simulation 中使用 UnityEngine.Random。

6\. 实现统一 2D Spatial Grid：插入、更新、删除、半径查询、邻近查询。

7\. 实现命令缓冲和事件缓冲的基础结构，避免在系统遍历时直接改变 Store 结构。

8\. 实现 RenderSnapshot：记录上一 Tick 和当前 Tick 的实体位置、朝向、状态标记。表现层可插值，但本里程碑不做正式 View。

9\. 提供 Headless Simulation Harness，可创建测试 Actor、移动若干 Tick 并导出摘要。

10\. 添加诊断计数：活动实体、创建、删除、无效句柄访问、每 Tick 时间。

## 必须测试

- 固定 Tick 在不同渲染 Delta 下得到相同 Tick 数和结果。

- 暂停和单步正确。

- 删除实体后旧句柄失效。

- Swap-back 后其他有效句柄仍正确。

- Store 扩容和复用正确。

- 系统顺序固定且可断言。

- Spatial Grid 半径查询与暴力结果一致。

- 同一种子产生相同移动结果。

- 快照包含前后状态并可计算插值。

- Headless Harness 不创建 GameObject。

## 验收标准

- Simulation Assembly 不引用 MonoBehaviour、Scene 或表现资源。

- 无逐实体 Update。

- 固定种子测试重复通过。

- 无效句柄不会读取或写入其他实体。

- M1 内容加载测试保持通过。

## 禁止

- 不实现完整伤害、状态、技能、敌人 AI 或地图。

- 不在 M2 过早 Job 化全部逻辑。

- 不使用 UnityEngine.Random。

- 不通过静态全局 World 访问模拟。

## 文档更新

- 更新 ADR 0002 和系统执行顺序。

- 文档化 Store 生命周期、句柄失效和快照格式。

## 完成报告

使用 Templates/CODEX_RESULT_REPORT.md，必须包含：

1\. 修改文件清单。

2\. 关键设计和理由。

3\. 实际执行的命令。

4\. 编译、测试、验证和构建的真实结果。

5\. 未完成事项和已知限制。

6\. 下一里程碑前置条件。

# M3：属性、伤害、护盾与状态系统

## 目标

在模拟内核上实现统一属性修正、伤害管线、生命/护盾、状态效果、死亡和高频事件缓冲。

## 前置条件

- M2 模拟内核已验收。

- Store、命令缓冲、事件缓冲和固定 Tick 稳定。

## 开始前

1\. 阅读 AGENTS.md、Docs/ARCHITECTURE.md、Docs/CODEX_WORKFLOW.md。

2\. 阅读本提示词列出的相关文档。

3\. 检查当前 Git diff，确保没有未说明的改动。

4\. 运行现有测试并记录基线。

5\. 输出不超过 10 条的实现计划，然后开始修改。

## 必须交付

1\. 实现稳定 StatId 和运行时 StatIndex。初始支持生命、移动速度、伤害、攻击速度、冷却、范围、持续、弹数、穿透、暴击、护甲、拾取范围、幸运、回复。

2\. 实现 ModifierCollection，统一顺序：Base -\> AddFlat -\> AddPercent -\> Multiply -\> Clamp -\> Override。Modifier 包含 SourceId、StatId、Operation、Value、Priority、StackingGroup、Duration。

3\. 实现 DamagePacket 和 DamageContext，包含来源实体、目标实体、来源 ContentId、伤害类型、标签、基础值、暴击资格、ProcCoefficient、击退、位置和 ProcDepth。

4\. 实现伤害结算顺序并集中在 DamageResolutionSystem。不得让技能直接修改生命值。

5\. 实现 Health、Shield、Armor/Resistance 的最小模型。

6\. 实现状态系统：RefreshDuration、AddStacks、ReplaceIfStronger、IndependentInstances、MaxStacks、TickInterval、DispelTags、ImmunityTags。

7\. 实现死亡请求与死亡事件，确保同一实体只死亡一次。

8\. 实现 Tick 内结构体事件缓冲：DamageApplied、StatusApplied、EntityDied、ShieldChanged。

9\. 实现 ProcDepth 和触发链上限，并记录被截断次数。

10\. 创建测试状态：Burning、Slow、Shielded；仅使用占位定义。

## 必须测试

- 所有 Modifier 运算顺序和优先级。

- 同 StackingGroup 的规则。

- 暴击、护甲、护盾、生命结算。

- 伤害不会低于/高于已定义边界。

- 状态四种叠层策略。

- 状态 Tick、过期、驱散和免疫。

- ProcDepth 超限被截断。

- 死亡事件只触发一次。

- 无效目标伤害安全失败。

- 固定种子结果可复现。

## 验收标准

- 技能或测试命令不能直接写 Health。

- 状态定义来自运行时内容，不持有 Unity Object。

- 事件缓冲在 Tick 结束后正确清空。

- 旧测试全部通过。

## 禁止

- 不实现完整技能选择和投射物行为。

- 不把具体角色技能写进 Damage System。

- 不在每次属性读取时分配集合。

- 不使用字符串比较作为高频属性查找。

## 文档更新

- 更新伤害顺序、Modifier 规则和状态 Schema。

- 若改变 M1 Runtime Definition，记录兼容性影响。

## 完成报告

使用 Templates/CODEX_RESULT_REPORT.md，必须包含：

1\. 修改文件清单。

2\. 关键设计和理由。

3\. 实际执行的命令。

4\. 编译、测试、验证和构建的真实结果。

5\. 未完成事项和已知限制。

6\. 下一里程碑前置条件。

# M4：模块化技能运行时

## 目标

实现 Trigger、Targeting、Delivery、Effect 和 LevelPatch 的可注册技能运行时，使普通新技能可通过配置组合。

## 前置条件

- M3 伤害与状态系统已验收。

- M1 内容烘焙可扩展。

## 开始前

1\. 阅读 AGENTS.md、Docs/ARCHITECTURE.md、Docs/CODEX_WORKFLOW.md。

2\. 阅读本提示词列出的相关文档。

3\. 检查当前 Git diff，确保没有未说明的改动。

4\. 运行现有测试并记录基线。

5\. 输出不超过 10 条的实现计划，然后开始修改。

## 必须交付

1\. 扩展 Skill Authoring/Runtime Definition：Trigger、Targeting、Delivery、Effects、LevelPatches、Tags、Cooldown 和资源成本。

2\. 实现显式注册表：

- Trigger Executor

- Targeting Executor

- Delivery Executor

- Effect Executor

- Condition Evaluator

不得使用运行时反射扫描。

3\. 实现烘焙 EffectOp\[\] 和紧凑技能运行时数据。

4\. 初始 Trigger：Timer、OnHit、OnKill、OnDamageTaken、OnPickup、OnStatusApplied。

5\. 初始 Targeting：Self、Nearest、Random、Circle、Cone、Line、Ring、RandomPointAroundPlayer。

6\. 初始 Delivery：Instant、Projectile、Area、Aura、Orbit。

7\. 初始 Effect：Damage、Heal、ApplyStatus、RemoveStatus、Knockback、Pull、ModifyStat、SpawnSecondarySkill、GrantShield、GainResource。

8\. 实现 Skill Instance、等级、冷却、触发上下文、目标结果和执行命令缓冲。

9\. LevelPatch 在烘焙时验证路径和类型，运行时不得通过字符串反射修改对象。

10\. 创建至少四个测试技能：单体投射物、环绕物、地面区域、伤害光环。全部使用程序化占位表现 ID。

11\. 实现技能预览的纯模拟 Harness，输出 DPS、命中数和触发次数，UI 后续实现。

## 必须测试

- 每种初始 Trigger 的基本行为。

- Targeting 在空间网格上得到正确目标。

- Projectile、Area、Aura、Orbit 的生命周期。

- EffectOp 正确调用 M3 伤害/状态系统。

- LevelPatch 各等级结果。

- 同一技能可被两个角色实例复用。

- ProcDepth 在二次技能中传播。

- 缺失模块 ID 在验证阶段失败。

- 四个测试技能固定种子结果稳定。

## 验收标准

- 新测试技能不需要新增 MonoBehaviour。

- 运行时不引用 Authoring ScriptableObject。

- 高频技能执行不使用反射或 LINQ。

- Skill 不直接修改 Health。

- 内容验证能报告无效 LevelPatch 和模块引用。

## 禁止

- 不实现敌人 AI、正式 VFX、正式 UI 或构筑选择。

- 不为四个测试技能各写一个专用控制器。

- 不允许 LevelPatch 在运行时解析任意字符串路径。

## 文档更新

- 更新 Docs/EFFECT_MODULES.md 和 Skill Schema。

- 记录新增模块的稳定 ID 和参数。

## 完成报告

使用 Templates/CODEX_RESULT_REPORT.md，必须包含：

1\. 修改文件清单。

2\. 关键设计和理由。

3\. 实际执行的命令。

4\. 编译、测试、验证和构建的真实结果。

5\. 未完成事项和已知限制。

6\. 下一里程碑前置条件。

# M5：敌人、刷怪、遭遇与地图运行时

## 目标

建立可配置敌人、轻量移动与决策、刷怪预算、遭遇时间线、有限地图和无限区块地图边界。

## 前置条件

- M4 技能运行时已验收。

- M2 空间网格和 M3 战斗系统稳定。

## 开始前

1\. 阅读 AGENTS.md、Docs/ARCHITECTURE.md、Docs/CODEX_WORKFLOW.md。

2\. 阅读本提示词列出的相关文档。

3\. 检查当前 Git diff，确保没有未说明的改动。

4\. 运行现有测试并记录基线。

5\. 输出不超过 10 条的实现计划，然后开始修改。

## 必须交付

1\. 扩展 Enemy Definition：基础属性、碰撞半径、移动模式、攻击 SkillId、Tags、奖励、VisualProfileId。

2\. 实现轻量敌人决策：追踪玩家、保持距离、冲锋准备/执行、简单远程攻击。行为通过配置和小型模块组合，不创建每敌人 MonoBehaviour。

3\. 实现 Steering、局部分离和简单障碍规避。普通敌人不使用 NavMeshAgent。

4\. 实现 Spawn Scheduler 和 Spawn Request Buffer。

5\. 实现 Encounter Schedule：阶段、时间、预算曲线、间隔曲线、敌人权重、群组、Elite、Boss 和并发上限。

6\. 实现 Spawn Pattern：Ring、Edge、Cluster、Line、Ambush、Portal、FixedAnchor、OffscreenRandom。

7\. 实现 IMapRuntime，首批：

- FiniteArenaMapRuntime

- ChunkedInfiniteMapRuntime 最小版本

8\. 地图场景只提供视觉和简化障碍输入；刷怪逻辑不写在地图 MonoBehaviour。

9\. 实现 Difficulty Snapshot：生命、伤害、速度、刷怪率、Elite 概率和奖励倍率。

10\. 创建两个测试地图、四种测试敌人和一个测试 Boss。全部使用程序化占位资源。

11\. 实现 Map/Encounter Headless Harness，可在无表现层情况下运行 5 分钟模拟。

## 必须测试

- 各敌人行为状态转换。

- Steering 和分离不会产生 NaN。

- Spawn Budget 与并发上限。

- 各 Spawn Pattern 在地图合法位置生成。

- 同一 Encounter 可用于两个地图。

- Map Runtime 的 Walkable 和 ResolveMovement。

- 固定种子生成相同区块和刷怪序列。

- Boss 只在指定阶段生成一次。

- Headless 5 分钟无未处理异常和实体泄漏。

## 验收标准

- 地图和遭遇配置解耦。

- 新敌人使用已有行为时只需配置。

- 新地图使用已有 MapRuntime 时只需 Definition、Scene 和 Encounter。

- 普通敌人没有逐个 NavMeshAgent 或 Update。

- M4 技能可由敌人和玩家共同使用。

## 禁止

- 不实现最终地图美术或正式敌人。

- 不把刷怪时间轴硬编码进 Scene。

- 不为每种敌人创建完整继承树。

- 不在每个敌人上单独做全局寻路。

## 文档更新

- 更新地图、敌人和 Encounter Schema。

- 文档化无限区块激活/释放策略。

## 完成报告

使用 Templates/CODEX_RESULT_REPORT.md，必须包含：

1\. 修改文件清单。

2\. 关键设计和理由。

3\. 实际执行的命令。

4\. 编译、测试、验证和构建的真实结果。

5\. 未完成事项和已知限制。

6\. 下一里程碑前置条件。

# M6：局内成长、构筑、联动与进化

## 目标

实现经验、升级选择、技能/被动库存、构筑标签、Synergy、Evolution、局内结果和可复现候选池。

## 前置条件

- M5 地图和敌人已验收。

- 技能、战斗和内容定义稳定。

## 开始前

1\. 阅读 AGENTS.md、Docs/ARCHITECTURE.md、Docs/CODEX_WORKFLOW.md。

2\. 阅读本提示词列出的相关文档。

3\. 检查当前 Git diff，确保没有未说明的改动。

4\. 运行现有测试并记录基线。

5\. 输出不超过 10 条的实现计划，然后开始修改。

## 必须交付

1\. 实现 XP、等级曲线、经验拾取和 LevelUp Request。

2\. 实现 Skill Inventory 和 Passive Inventory：槽位、等级、最大等级、重复获取、替换策略。

3\. 实现 Offer Generator：候选池、权重、前置条件、互斥、已满槽、最大等级、Reroll、Banish、Skip 接口。

4\. 候选生成使用 Run Random Stream 的专用派生流，并可记录种子和选择历史。

5\. 实现 BuildState：Owned Skills、Passives、Traits、Tags、Active Synergies、Evolution Eligibility。

6\. 实现 Condition Evaluator：OwnsContent、HasTagCount、SkillLevelAtLeast、StatAtLeast、MapHasTag。

7\. 实现 Synergy Outputs：AddModifier、UnlockOffer、AddEffectOp、TransformSkill、GrantTrait。

8\. 实现 Evolution Definition 和 Consume Policy。

9\. 实现 Run State、暂停升级选择、Run End、Run Result 和基础统计。

10\. 创建两个测试 Synergy 和一个测试 Evolution，不创建正式构筑。

11\. 实现自动玩家 Harness：自动移动、拾取和选择升级，完成 10 分钟测试局。

## 必须测试

- XP 曲线和多次连续升级。

- Offer 权重、互斥、前置、满级和槽位规则。

- 固定种子候选结果可复现。

- Reroll 使用可预测但不同的序列。

- Banish 不再出现被移除内容。

- Synergy 条件与输出。

- Evolution 条件、转化和消费策略。

- 构筑标签随内容变化更新。

- 10 分钟自动局可完成并产生一致统计。

## 验收标准

- 不存在硬编码 FireBuild、CritBuild 等类。

- 新 Synergy 和 Evolution 只需配置。

- 升级 UI 尚未实现时，应用层可通过命令选择。

- 暂停升级时 SimulationClock 停止，测试时钟继续可控。

## 禁止

- 不实现正式数值平衡和局外商店。

- 不把构筑判断散落到技能类。

- 不使用全局随机数生成候选。

- 不在 UI 中实现候选规则。

## 文档更新

- 更新构筑、Synergy、Evolution 和 Offer Schema。

- 文档化随机流和重放诊断数据。

## 完成报告

使用 Templates/CODEX_RESULT_REPORT.md，必须包含：

1\. 修改文件清单。

2\. 关键设计和理由。

3\. 实际执行的命令。

4\. 编译、测试、验证和构建的真实结果。

5\. 未完成事项和已知限制。

6\. 下一里程碑前置条件。

# M7：表现层、输入与完整 UI 流程

## 目标

连接模拟快照与 Unity 表现层，建立 View 池、输入、摄像机、占位 VFX/音频和完整可操作 UI 流程。

## 前置条件

- M6 局内循环已可通过 Headless Harness 完成。

- Render Snapshot 和应用状态机接口稳定。

## 开始前

1\. 阅读 AGENTS.md、Docs/ARCHITECTURE.md、Docs/CODEX_WORKFLOW.md。

2\. 阅读本提示词列出的相关文档。

3\. 检查当前 Git diff，确保没有未说明的改动。

4\. 运行现有测试并记录基线。

5\. 输出不超过 10 条的实现计划，然后开始修改。

## 必须交付

1\. 实现 View Binding 和池：ActorView、ProjectileView、AreaView、PickupView。View 不拥有玩法真值。

2\. 实现 Snapshot Interpolation、实体生成/释放、受击、死亡和状态表现请求。

3\. 实现 VFX Request Pool 和 Audio Request 路由。只使用程序化图形和明确的测试音，禁止正式资源。

4\. 建立 Input Action Maps：Gameplay、UI、Debug。支持键鼠与主流手柄，UI 打开时切换 Action Map。

5\. 实现应用状态和页面：

- Bootstrap

- MainMenu

- CharacterSelect

- MapSelect

- Loading

- RunHUD

- Pause

- LevelUpDraft

- RunResult

- Settings

- ContentError

6\. UI 使用 ViewModel/Presenter，不直接访问 Simulation Store。

7\. Settings 至少包含重映射接口、死区、震动强度、屏幕震动、闪光强度、伤害数字和自动瞄准策略。

8\. 实现基础摄像机、边界、屏幕震动接口和可关闭效果。

9\. 实现缺失 VisualProfile 时的程序化 fallback。

10\. 建立完整 PlayMode 流程测试和手柄导航测试。

## 必须测试

- View 创建、绑定、回收和句柄失效。

- Snapshot 插值无明显跳变。

- View 无法直接写 Simulation Store。

- Gameplay/UI Action Map 正确切换。

- 键鼠和手柄完成菜单、升级、暂停和结算。

- 暂停时 Simulation 停止，UI 正常响应。

- 缺失资源使用 fallback。

- 释放场景后无池或事件泄漏。

- 完整 PlayMode 流程通过。

## 验收标准

- 可以从启动进入测试局并完成结算。

- View 层不计算伤害、经验、死亡或掉落。

- 所有高频 View 使用池。

- 没有导入外部美术。

- UI 用户文字准备接入本地化，不在逻辑中硬编码。

## 禁止

- 不实现正式 UI 皮肤、角色或特效。

- 不让 View 调用 Damage System。

- 不用 FindObjectOfType 连接 UI 和服务。

- 不为伤害数字创建独立 Canvas。

## 文档更新

- 更新表现层数据流和 UI 状态图。

- 文档化输入动作和可访问性设置。

## 完成报告

使用 Templates/CODEX_RESULT_REPORT.md，必须包含：

1\. 修改文件清单。

2\. 关键设计和理由。

3\. 实际执行的命令。

4\. 编译、测试、验证和构建的真实结果。

5\. 未完成事项和已知限制。

6\. 下一里程碑前置条件。

# M8：版本化存档、本地化与平台边界

## 目标

实现可靠本地存档、迁移、本地化和 Null 平台适配，为后续 Steam 集成建立稳定接口。

## 前置条件

- M7 完整本地流程已验收。

- Run Result 和内容 ID 稳定。

## 开始前

1\. 阅读 AGENTS.md、Docs/ARCHITECTURE.md、Docs/CODEX_WORKFLOW.md。

2\. 阅读本提示词列出的相关文档。

3\. 检查当前 Git diff，确保没有未说明的改动。

4\. 运行现有测试并记录基线。

5\. 输出不超过 10 条的实现计划，然后开始修改。

## 必须交付

1\. 实现 settings.json、profile.json、run_recovery.json 的独立模型。

2\. 实现 ISaveStorage、本地文件实现、原子写入、临时文件、备份、校验和和取消。

3\. 实现 Schema Version、迁移注册表和至少 v1-\>v2 的测试迁移样本。

4\. 存档只保存稳定 ContentId、Pack 版本和纯数据，不保存 RuntimeIndex 或 Unity Object。

5\. 实现缺失内容处理和诊断结果。

6\. 接入 Localization：简体中文、英文、伪本地化。所有用户可见文字迁移为 Key。

7\. 实现 IPlatformFacade 子服务接口和 NullPlatformFacade：Achievements、Stats、Cloud、RichPresence、Identity。

8\. 实现云同步状态模型和冲突策略接口，但不引入真实 Steam SDK。

9\. 成就和平台统计通过应用事件触发，不允许 Simulation 直接调用平台服务。

10\. 增加设置保存、档案保存、局内恢复和本地化流程测试。

## 必须测试

- 原子写入中断不破坏上一版本。

- 校验和失败能恢复备份或返回明确错误。

- v1 样本迁移到 v2。

- 缺失 ContentId 不导致未处理异常。

- RuntimeIndex 不进入 JSON。

- 简中、英文和伪本地化加载。

- UI 无硬编码用户文字。

- NullPlatform 下可完整运行。

- Simulation Assembly 不引用平台接口实现。

- 云冲突模型的本地较新、远端较新和分叉情况。

## 验收标准

- 旧存档可迁移。

- 存档失败有用户可理解的应用层结果。

- 无 Steam 环境仍可启动、保存和完成游戏。

- 平台服务可以替换而不修改 Simulation。

## 禁止

- 不集成真实 Steam SDK。

- 不把云端文件当作唯一存档。

- 不使用 BinaryFormatter。

- 不把本地化字符串直接存入内容定义作为唯一真值。

## 文档更新

- 更新 Docs/SAVE_FORMAT.md、本地化 Key 规范和 STEAM_INTEGRATION_BOUNDARY.md。

## 完成报告

使用 Templates/CODEX_RESULT_REPORT.md，必须包含：

1\. 修改文件清单。

2\. 关键设计和理由。

3\. 实际执行的命令。

4\. 编译、测试、验证和构建的真实结果。

5\. 未完成事项和已知限制。

6\. 下一里程碑前置条件。

# M9：编辑器工具与内容生产工作流

## 目标

让非程序人员能够安全创建、验证、预览和打包角色、技能、敌人、地图、联动和内容包。

## 前置条件

- M8 内容、流程和保存格式已稳定。

- 所有主要 Authoring Schema 已确定。

## 开始前

1\. 阅读 AGENTS.md、Docs/ARCHITECTURE.md、Docs/CODEX_WORKFLOW.md。

2\. 阅读本提示词列出的相关文档。

3\. 检查当前 Git diff，确保没有未说明的改动。

4\. 运行现有测试并记录基线。

5\. 输出不超过 10 条的实现计划，然后开始修改。

## 必须交付

1\. 实现 Content Creation Wizard，可创建 Pack、Character、Skill、Passive、Trait、Enemy、Status、Evolution、Synergy、Map、Encounter。

2\. 创建时自动：

- 生成符合规则的 ID

- 建立目录

- 建立本地化 Key

- 设置 Addressables 标签

- 创建测试模板

- 建立来源记录占位

3\. 实现 Validator Window 和命令行验证：ID、引用、依赖、循环、等级、概率、冷却、掉落、本地化、VisualProfile、碰撞半径、触发链、Placeholder 和 provenance。

4\. 实现 Wave Timeline Editor：阶段、预算、间隔、敌人权重、理论并发、生命总量、经验产量和 Boss 时间预览。

5\. 实现 Skill Preview Harness UI：选择技能/等级/属性/敌人数，显示范围、命中盒、DPS、触发次数、分配和模拟日志。

6\. 实现 Content Pack Builder：版本、依赖、Catalog、内容 Hash 和构建报告。

7\. 实现 Build Preprocessor：Release 阻止 Placeholder、缺失 provenance、Third Party 未登记、内容验证失败。

8\. 实现资产来源 Hash 检查。

9\. 创建“第二角色、第二技能、第二地图”扩展性测试内容，必须通过向导或内容资产完成。

10\. 编写面向内容制作人员的简明操作文档。

## 必须测试

- 向导生成的每种内容可烘焙。

- 重复 ID、缺失引用和循环依赖可定位。

- Release 构建遇到 Placeholder 会失败。

- provenance 缺失或 Hash 不一致会失败。

- Wave Timeline 计算与运行时抽样一致。

- Skill Preview 与 Headless Harness 结果一致。

- Pack Build 同输入产生同 Hash。

- 第二角色、技能、地图不修改核心程序集。

## 验收标准

- 非程序人员可按文档完成测试内容创建。

- 所有验证可从命令行运行。

- 构建报告列出 Pack、版本、Hash 和资源标签。

- 扩展性测试证明框架主要目标。

## 禁止

- 不创建正式美术或大量正式内容。

- 不让向导生成硬编码 Registry 修改。

- 不允许 Release 构建提供“忽略所有错误”选项。

- 不把验证逻辑只放在 EditorWindow，命令行必须可复用。

## 文档更新

- 更新内容制作手册、验证规则、Pack Builder 和资产来源流程。

## 完成报告

使用 Templates/CODEX_RESULT_REPORT.md，必须包含：

1\. 修改文件清单。

2\. 关键设计和理由。

3\. 实际执行的命令。

4\. 编译、测试、验证和构建的真实结果。

5\. 未完成事项和已知限制。

6\. 下一里程碑前置条件。

# M10：性能、CI、构建与框架冻结

## 目标

建立可重复性能基线、对真实热点进行优化、完善命令行/CI/构建清单，并完成框架冻结验收。

## 前置条件

- M9 编辑器工具和扩展性测试已验收。

- 全部核心功能可运行。

## 开始前

1\. 阅读 AGENTS.md、Docs/ARCHITECTURE.md、Docs/CODEX_WORKFLOW.md。

2\. 阅读本提示词列出的相关文档。

3\. 检查当前 Git diff，确保没有未说明的改动。

4\. 运行现有测试并记录基线。

5\. 输出不超过 10 条的实现计划，然后开始修改。

## 必须交付

1\. 建立固定压力场景：1,500 敌人、3,000 投射物、5,000 拾取物、200 VFX 请求，参数可配置。

2\. 建立 30 分钟 Headless Soak Test 和性能 JSON 输出。

3\. 记录基线：Tick 时间分位数、渲染帧时间、实体峰值、托管/Native 内存、GC、对象池、触发截断、无效句柄和 VFX 丢弃。

4\. 只对 Profiler 证明的热点迁移 Jobs/Burst。每项优化必须有优化前后数据和正确性回归测试。

5\. 完善命令：

- EditMode Test

- PlayMode Test

- Content Validate

- Soak Test

- Windows Development Build

- Windows Release Build

6\. 实现 Build Manifest：游戏版本、Git SHA、Unity 版本、Pack 版本、内容 Hash、构建 UTC、构建类型。

7\. 建立适用于自托管 Windows Runner 的 CI 工作流或等价脚本；不能假设未配置的 Unity 许可证秘密。

8\. 从干净 clone 验证导入、测试、验证和构建。

9\. 运行完整架构审计：程序集、禁止 API、内容扩展、存档、本地化、Placeholder、Third Party、provenance。

10\. 修正文档与代码差异，冻结核心公共 API；未来破坏性变化需要 ADR 和迁移计划。

11\. 执行 Docs/DEFINITION_OF_DONE.md 全部检查并生成签字报告。

## 必须测试

- 固定压力场景和 30 分钟 Soak。

- 优化前后确定性和战斗结果一致。

- 稳态托管分配目标。

- 内存无持续增长。

- 所有 EditMode、PlayMode、验证和构建命令。

- 从干净 clone 的完整流水线。

- 第二角色、第二技能、第二地图扩展性测试。

- Release Build 不含 Placeholder。

- Build Manifest 与实际内容一致。

## 验收标准

- 有可重复性能基线和机器信息。

- 所有门禁通过或明确记录批准的例外。

- Windows Release Build 可启动并完成测试局。

- 文档与代码一致。

- 框架 Definition of Done 签字完成。

## 禁止

- 不为追求数字改变模拟真值。

- 不无依据地把全部系统 Job 化。

- 不关闭安全检查或验证器生成 Release。

- 不在最终阶段引入大量新功能。

- 不把 CI 未运行描述为成功。

## 文档更新

- 更新性能基线、构建方法、CI 方法、已知限制和框架冻结报告。

## 完成报告

使用 Templates/CODEX_RESULT_REPORT.md，必须包含：

1\. 修改文件清单。

2\. 关键设计和理由。

3\. 实际执行的命令。

4\. 编译、测试、验证和构建的真实结果。

5\. 未完成事项和已知限制。

6\. 下一里程碑前置条件。

# 里程碑审查门禁提示词

对当前里程碑执行一次严格的只读审查；发现问题后再进行最小修复。不要扩张功能范围。

## 审查步骤

1\. 读取当前里程碑提示词和验收标准。

2\. 检查 Git diff，列出所有新增、删除和修改文件。

3\. 标记任何超出当前里程碑范围的改动。

4\. 检查 asmdef 依赖和循环。

5\. 检查以下禁用模式：

- Simulation 引用 UnityEngine 对象

- GameObject.Find / FindObjectOfType

- Resources.Load

- 高频 LINQ、反射、临时集合

- 全局 Service Locator

- 高频 Instantiate/Destroy

- UI/View 直接写 Simulation Store

6\. 检查内容、存档、资产和本地化规则。

7\. 运行编译、EditMode、PlayMode、验证和适用构建。

8\. 将每条验收标准标记为 PASS、FAIL 或 NOT RUN。

9\. 对 FAIL 添加最小复现和根因。

10\. 只修复本里程碑范围内的失败，重新运行相关测试。

## 输出

- 里程碑结论：PASS / FAIL

- 验收矩阵

- 范围外改动

- 架构违规

- 实际命令与结果

- 修复文件

- 未解决问题

未执行的检查必须写 NOT RUN，不能按 PASS 处理。

# Bug 修复与回归提示词

修复下面指定的问题。遵守现有架构，不进行无关重构。

## 输入

- 问题描述：\<填写\>

- 复现步骤：\<填写\>

- 预期行为：\<填写\>

- 实际行为：\<填写\>

- 相关日志/种子/存档：\<填写\>

## 流程

1\. 先复现问题并记录结果。

2\. 在可能的情况下添加一个会失败的自动测试。

3\. 确认根因，不只修正表面症状。

4\. 采用最小改动修复。

5\. 不删除测试、不放宽断言、不绕过验证。

6\. 运行新测试、相关子系统测试和完整回归。

7\. 检查是否影响存档、内容 Schema、固定种子或性能。

8\. 更新相关文档和已知问题。

## 输出

- 根因

- 修复方案

- 新增回归测试

- 实际命令与结果

- 风险与未覆盖情况

# 架构审计提示词

对仓库执行架构合规审计，默认只读。除非我明确要求，不修改代码。

## 检查

- asmdef 依赖方向和循环

- Core 是否引用 UnityEngine

- Simulation 是否引用 MonoBehaviour、GameObject、Scene、Prefab、Sprite、AudioClip、Addressables 或平台 SDK

- View/UI 是否直接修改 Simulation Store

- 是否存在全局 Service Locator、无控制 Singleton 或场景查找

- 高频路径中的 LINQ、反射、字符串格式化、集合分配

- 内容注册表是否硬编码具体内容

- Runtime Definition 是否持有 Unity Object

- 存档是否保存 RuntimeIndex

- 内容 ID 是否被修改或覆盖

- Placeholder、Third Party、provenance 和本地化门禁

- 测试是否覆盖关键规则

## 输出

按严重程度列出：Blocker、High、Medium、Low。每项包含文件、行号、原因、影响和最小修复建议。没有证据时不要推测。

# 框架冻结后的内容新增提示词模板

使用现有框架新增内容，不修改核心架构。先替换尖括号中的变量。

## 内容请求

- 内容类型：\<角色/技能/被动/敌人/Boss/地图/联动/进化\>

- 稳定 ContentId：\<填写\>

- 所属 Pack：\<填写\>

- 设计目标：\<填写\>

- 使用的现有模块：\<Trigger/Targeting/Delivery/Effect/Condition/MapRuntime\>

- 正式或占位：\<占位/正式\>

- 本地化语言：zh-CN, en

## 规则

1\. 优先只创建内容资产、地图场景、视觉 Profile 和本地化条目。

2\. 不修改 Core、Simulation 公共 API。

3\. 如果现有模块无法表达，停止并提交 Change Request，不自行硬编码。

4\. 正式资源必须通过来源和 AI 资产验证。

5\. 添加内容验证、预览和最小 PlayMode 测试。

6\. 固定种子测试记录新增内容对候选池和构筑的影响。

## 输出

- 新增内容文件

- 使用的模块

- 是否修改代码；如修改，解释为何不属于框架缺陷

- 验证和测试结果

- 平衡参数清单

- 资产来源状态

# Release Candidate 审计提示词

对候选发布版本执行发布门禁审计。默认不添加新功能。

## 必查

- Git 工作区和版本标签

- Unity、包和 Build Manifest

- EditMode、PlayMode、Soak 和性能基线

- Release Build 启动和完整测试局

- Placeholder 与 development-only 扫描

- AI provenance、文件 Hash、人工复核状态

- Third Party 许可证、NOTICE 和署名

- 本地化缺失、伪本地化裁切

- 存档迁移、备份、损坏恢复和云冲突边界

- 键鼠、手柄和设置

- 崩溃、日志和隐私数据

- Steam 平台适配器禁用/启用路径

- 内容包版本、依赖和 Hash

## 输出

- GO / NO-GO

- 阻断问题

- 可接受已知问题

- 实际命令、机器和结果

- 需要人工或法律复核的项目

- 发布后监控建议

# 发布审计结果模板

- Candidate 版本：

- Git 提交与标签：

- Unity 与包锁定版本：

- Content Pack 版本与 Hash：

- 审计机器与操作系统：

- 审核人及日期：

## 门禁矩阵

| **门禁**                       | **PASS/FAIL/NOT RUN** | **证据位置** | **负责人** |
|--------------------------------|-----------------------|--------------|------------|
| 编译与程序集依赖               |                       |              |            |
| EditMode / PlayMode            |                       |              |            |
| 30 分钟 Soak 与性能基线        |                       |              |            |
| Windows Release Build          |                       |              |            |
| Placeholder / development-only |                       |              |            |
| AI provenance 与 Hash          |                       |              |            |
| Third Party 许可与署名         |                       |              |            |
| 存档迁移与损坏恢复             |                       |              |            |
| 本地化与输入设备               |                       |              |            |
| Steam 适配器边界               |                       |              |            |

## 最终决策

> ☐ GO：允许发布
>
> ☐ NO-GO：存在阻断项

- 阻断项与负责人：

- 已知问题接受人：

- 回滚构建或上一稳定版本：

- 发布后首日监控指标：
