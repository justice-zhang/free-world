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
