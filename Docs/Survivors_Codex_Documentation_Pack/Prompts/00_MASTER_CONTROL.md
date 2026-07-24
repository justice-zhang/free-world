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
