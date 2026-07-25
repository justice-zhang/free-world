# 里程碑执行日志

本文件记录已完成里程碑的可审计集成状态。原始 Unity 日志和构建产物位于本地
忽略目录；对应结果报告保存在 `Docs/Reports`。

## M0：干净工程与工程治理

- 状态：`COMPLETE`
- 日期：2026-07-25
- Unity：`6000.3.20f1`
- 结果报告：`Docs/Reports/2026-07-25-m0-clean-project.md`
- 最终标签：`framework-m0`

### 集成记录

| 项目 | 记录 |
|---|---|
| M0 实现提交 | `57ce2a02ae0e83cac251615a273519c8b4c251fe` |
| M0 实现合并 | PR #1，merge commit `79d01d81b2a62b5c2dfb4a151d0772a2f46c93ad` |
| 构建清理修复 | `36b6d6e03fe14f7a25087f512ba1f35692600786` |
| 修复合并 | PR #2，merge commit `33f78be9bbe59eeff84a591bf42abbab86e01035` |
| 分支收敛 | M0 收尾合并并打标签后，以 `main` 为唯一规范分支；删除三个已合并的临时分支 |

### 最终检查

| 检查 | 结果 | 证据 |
|---|---|---|
| Git diff 与提交图 | PASS | M0 两个实现提交均为 `main` 祖先；收尾工作树只含已说明文件 |
| Bootstrap Scene 静态检查 | PASS | Build Settings 仅启用 `Assets/Scenes/Bootstrap.unity`；场景只有 Main Camera 与唯一 GameBootstrapper |
| CLI 失败路径 | PASS | Unity 错误返回 0 时：测试缺 XML 返回 4，验证缺 PASS 标记返回 5，构建缺新 EXE 返回 5 |
| 编译 | PASS | `TestResults/m0-final-compile.log`：Unity 退出 0，无编译错误 |
| EditMode | PASS | `TestResults/m0-final-tests/editmode.xml`：6/6 |
| PlayMode | PASS | `TestResults/m0-final-tests/playmode.xml`：4/4 |
| 内容验证 | PASS | `TestResults/m0-final-validation.log`：`[M0 Validation] PASS` |
| Windows Development Build | PASS | `TestResults/m0-final-build-rerun.log`：`[M0 Build] PASS`；EXE SHA-256 `5D7EEB5359C2E35E4EB1F6A5844B25C3D7556795BD2F15EC234A2011406BC9C6` |
| Windows Player 冒烟 | PASS | `TestResults/m0-final-player-smoke.log`：进入 MainMenu，无错误；8 秒后主动终止 |
| 构建后工作树 | PASS | Addressables 临时 `link.xml` 与 `.meta` 均未残留 |
| 性能/Soak | NOT RUN | M0 无正式模拟负载，按后续性能里程碑执行 |

### 下一步

只有在明确指定 M1、重新读取 M1 提示词并完成新的主分支基线后，才能开始内容模型工作。

## M1：核心类型与内容系统

- 状态：`COMPLETE`
- 日期：2026-07-25
- Unity：`6000.3.20f1`
- 实现报告：`Docs/Reports/2026-07-25-m1-content-system.md`
- 审查报告：`Docs/Reports/2026-07-25-m1-review-gate.md`
- 最终标签：`framework-m1`

### 集成记录

| 项目 | 记录 |
|---|---|
| M1 实现提交 | `8edcfadee2f2d3824dee5db0a401146e51e39f22` |
| M1 实现合并 | GitHub PR #4，merge commit `268e3f23535e304dfc4843d85ada5d2a47f642a0` |
| M1 收尾提交 | `586ae087e16bee5752dcff9a65b8dae835122c8c` |
| M1 收尾合并 | GitHub PR #5 |
| 标签目标 | `framework-m1` 指向 M1 收尾 PR 的最终 merge commit |

### 最终检查

| 检查 | 结果 | 证据 |
|---|---|---|
| Git diff 与范围 | PASS | 相对 `framework-m0` 修改 16、新增 70、删除 0；全部属于 M1 |
| Bootstrap Scene 静态检查 | PASS | Build Settings 仅启用 Bootstrap；场景有 2 个根对象、1 个 GameBootstrapper、1 个有效 baked Catalog 引用 |
| asmdef 与禁用模式 | PASS | 13 asmdef、40 条内部依赖、0 缺失、0 环；禁用 API 和 Runtime Unity 引用均为 0 |
| 编译 | PASS | `TestResults/M1ReviewFinal/compile.log`：Unity 退出 0，无 C# 错误 |
| EditMode | PASS | `TestResults/M1ReviewFinal/editmode.xml`：33/33 |
| PlayMode | PASS | `TestResults/M1ReviewFinal/playmode.xml`：5/5 |
| 内容/治理验证 | PASS | `TestResults/M1ReviewFinal/validation.log`：`[Project Validation] PASS` |
| Windows Development Build | PASS | Build Manifest：`Succeeded`、`StandaloneWindows64`、Development |
| Windows Player 冒烟 | PASS | `TestResults/M1ReviewFinal/player-smoke.log`：`packs=1, entries=4`，无失败标记 |
| Release Build | NOT RUN | M1 已执行适用的 Development Build；Release 门禁随正式发布阶段执行 |
| 性能/Soak | NOT RUN | M1 无模拟 Tick、实体或高频运行负载 |

### 下一步

M2 只能从带 `framework-m1` 标签的最终 `main` 创建独立分支；M1 不再扩张内容 Schema。
