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
