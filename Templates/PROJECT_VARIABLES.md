# 项目变量

在开始 M0 前填写。本文件是所有 Codex 提示词中的占位符来源。

| 变量 | 值 |
|---|---|
| `<PROJECT_NAME>` | `AzureSword` |
| `<ROOT_NAMESPACE>` | `AzureSword` |
| `<CONTENT_NAMESPACE>` | `base` |
| `<UNITY_VERSION>` | `6000.3.20f1` |
| `<TARGET_PLATFORM>` | `Windows x64 / Steam` |
| `<SIMULATION_TICK_RATE>` | `30 Hz` |
| `<TARGET_RENDER_FPS>` | `60 FPS` |
| `<MIN_TARGET_HARDWARE>` | `Windows 10 64-bit；4 核 x64 CPU；8 GB RAM；支持 DirectX 11、2 GB VRAM 的 GPU` |
| `<DEFAULT_LOCALE>` | `zh-CN` |
| `<SECONDARY_LOCALE>` | `en` |
| `<REPOSITORY_URL>` | `https://github.com/justice-zhang/free-world.git` |
| `<TEAM_NAME>` | `justice-zhang` |

## 冻结规则

- M0 验收后，不得随意修改 `<ROOT_NAMESPACE>`。
- 首个公开测试版本发布后，不得修改已发布的 ContentId。
- Unity 版本升级必须新建 ADR，运行完整回归测试后才能合并。
- 模拟 Tick 频率变化会影响平衡、存档恢复和重放验证，必须经过架构评审。
