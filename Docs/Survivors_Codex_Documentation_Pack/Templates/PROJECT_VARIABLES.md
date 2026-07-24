# 项目变量

在开始 M0 前填写。本文件是所有 Codex 提示词中的占位符来源。

| 变量 | 值 |
|---|---|
| `<PROJECT_NAME>` | 例如 `Project Ember` |
| `<ROOT_NAMESPACE>` | 例如 `Studio.ProjectEmber` |
| `<CONTENT_NAMESPACE>` | 例如 `base` |
| `<UNITY_VERSION>` | 以 `ProjectSettings/ProjectVersion.txt` 为准 |
| `<TARGET_PLATFORM>` | Windows x64 / Steam |
| `<SIMULATION_TICK_RATE>` | 默认 30 Hz |
| `<TARGET_RENDER_FPS>` | 默认 60 FPS |
| `<MIN_TARGET_HARDWARE>` | 待填写 |
| `<DEFAULT_LOCALE>` | `zh-CN` |
| `<SECONDARY_LOCALE>` | `en` |
| `<REPOSITORY_URL>` | 待填写 |
| `<TEAM_NAME>` | 待填写 |

## 冻结规则

- M0 验收后，不得随意修改 `<ROOT_NAMESPACE>`。
- 首个公开测试版本发布后，不得修改已发布的 ContentId。
- Unity 版本升级必须新建 ADR，运行完整回归测试后才能合并。
- 模拟 Tick 频率变化会影响平衡、存档恢复和重放验证，必须经过架构评审。
