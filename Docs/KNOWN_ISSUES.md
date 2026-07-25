# 已知问题

状态定义：

- `OPEN`：尚未解决，可能影响当前工作。
- `ACCEPTED`：当前里程碑允许的限制，已明确影响和后续处理阶段。
- `PLANNED`：已分配到后续里程碑。
- `RESOLVED`：已修复并有验证证据。

## M0

| ID | 状态 | 严重度 | 问题与影响 | 处理 |
|---|---|---|---|---|
| M0-KI-001 | RESOLVED | High | Unity 在 `Library/SourceAssetDB` 无法打开时可能返回 0 且不生成测试 XML，旧脚本会误报成功。 | `Scripts/test.ps1` 现在删除旧结果，并要求 XML 存在、可解析且结果为 Passed；缺失或无效结果返回 4，测试失败返回 5。 |
| M0-KI-002 | RESOLVED | Low | 打开的 Office 文档会产生未跟踪的 `~$*.docx` 临时锁文件，污染 Git 状态。 | `.gitignore` 精确忽略 `~$*.docx`，不删除用户正在使用的临时文件。 |
| M0-KI-003 | RESOLVED | High | Unity CLI 日志偶尔包含 Licensing Client 握手或令牌刷新错误，失败启动甚至可能返回 0。 | 三个脚本均清除对应旧结果；测试要求有效 Passed XML，验证要求 PASS 标记，构建要求新 EXE、PASS 标记和有效 Build Manifest。缺少任一证据必须判定 FAIL。 |
| M0-KI-004 | ACCEPTED | Low | Windows Player 冒烟测试进入 MainMenu 并稳定运行 8 秒后被主动终止，进程退出码为 `-1`。 | 该值代表测试主动关闭，不是崩溃；日志不得出现未处理异常。后续自动化应增加显式退出命令。 |
| M0-KI-005 | ACCEPTED | None | M0 的 MainMenu 是黑色空场景，没有正式 UI。 | 符合 M0 禁止提前实现玩法和正式菜单的范围；表现与 UI 在 M7 实现。 |
| M0-KI-006 | PLANNED | None | 性能与 30 分钟 Soak Test 未执行。 | M0 无正式模拟负载；在模拟内核和压力场景具备后按性能里程碑执行。 |
| M0-KI-007 | ACCEPTED | Low | Unity 自动生成的 `.meta` 和 Addressables YAML 含空值尾随空格，完整里程碑 `git diff --check` 会报告这些生成字段。 | 手写 C#、PowerShell、Markdown、JSON 和 asmdef 必须通过 whitespace 检查；不手工批量重写 Unity 序列化文件。 |

当前没有阻止 M1 开始的 `OPEN` 问题。

## M1

| ID | 状态 | 严重度 | 问题与影响 | 处理 |
|---|---|---|---|---|
| M1-KI-001 | RESOLVED | Medium | Character 先验证被引用 Skill 的非 canonical ID 时，错误路径曾指向 Character，无法定位实际错误资产。 | Baker 现在先预验证 Pack 内每个定义自身的 ID 和路径；`ContentBakerTests.AuthoringRejectsNonCanonicalIdWithPackAndAssetPath` 已覆盖。 |
| M1-KI-002 | RESOLVED | High | Catalog、Manifest、运行时定义和 Registry 的 `IReadOnlyList` 曾直接暴露 backing array，可绕过验证并使 Hash 或索引状态失配。 | 构造输入继续 clone，对外返回缓存只读视图；`RuntimeCollectionsDoNotExposeMutableBackingArrays` 已覆盖。 |
| M1-KI-003 | ACCEPTED | Low | M1 Bootstrap 直接引用测试 Pack 的 baked `TextAsset`，尚未实现正式 Addressables Pack 生命周期、异步句柄或 DLC。 | 符合 ADR 0003 的 M1 落地边界；正式内容接入前再实现通用 Pack 加载流程，不在 M1 扩张。 |
| M1-KI-004 | PLANNED | Medium | M1 只验证 Localization Key 非空，尚未验证 Key 是否存在于 Locale 表。 | Unity Localization 表、伪本地化和缺 Key 门禁在 M8 实现。 |
| M1-KI-005 | PLANNED | None | M1 未运行 30 分钟性能/Soak Test。 | M1 无模拟负载；固定种子压力场景和性能 JSON 在 M10 执行。 |

当前没有阻止 M2 开始的 `OPEN` 问题。
