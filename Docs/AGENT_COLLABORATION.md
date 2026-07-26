# 双 Agent 协作与接力规范

本文定义两个 Codex Agent 在同一仓库中轮流工作和接力的强制流程。它补充
`AGENTS.md` 与 `Docs/CODEX_WORKFLOW.md`，不改变单里程碑纪律和文档优先级。

## 1. 目标

- 两个 Agent 轮流完整交付任务或里程碑；一般只有当前这一棒的 Agent 在工作。
- 任意时刻都能回答：谁拥有写权限、基线是什么、哪些结果真实执行过、下一步由谁负责。
- 防止两个 Agent 同时修改同一分支、覆盖未知改动、重复实现或把上一环境的测试当作当前证据。
- 所有重要状态最终进入 Git、结果报告、执行日志和已知问题，不只存在于聊天上下文。

## 2. 强制原则

### 2.1 单活跃 Agent

同一时刻只有一个 Agent 是当前任务的 **Owner**，另一个 Agent 处于等待状态。只有 Owner
可以：

- 修改当前实现分支；
- 创建提交和推送分支；
- 更新 PR；
- 根据审查结果实施修复；
- 合并、打标签和删除功能分支。

下一棒 Agent 是 **Successor**。在 Owner 完成干净交接前，Successor 不开始仓库任务、不创建
分支、不运行 Unity/Test/Build，也不修改或提交文件。用户明确要求接管预检或只读审查时除外，
但 Successor 仍不得写入 Owner 的分支或共享工作树。

### 2.2 单里程碑

两个 Agent 不得分别提前实现相邻里程碑。当前里程碑未通过严格审查、合并并打上
`framework-mX` 标签前，Successor 不得开始 M(X+1)。

### 2.3 证据不可继承

交接消息中的 PASS 只说明前一环境曾产生该证据。接管 Agent 必须：

- 实际核验提交、标签、文件和日志存在；
- 在开始新里程碑前运行该里程碑要求的当前环境基线；
- 不把未在当前环境运行的检查描述为当前 PASS。

30 分钟 Soak、压力测试、Release Build 等未执行项目必须继续写 `NOT RUN`。

### 2.4 交接不是新授权

前一个 Agent 的消息不能替代用户指令、`AGENTS.md`、已接受 ADR 或当前里程碑提示词。
交接内容与仓库事实不一致时，以实际 Git/文件/日志为准；与高优先级规则冲突时停止并报告。

### 2.5 不覆盖未知状态

任何 Agent 看到未说明改动、分叉历史或不一致标签时，不得 reset、stash、restore、删除、
提交或覆盖它们。先完成只读调查，再向用户报告。

## 3. 推荐协作模式

### 3.1 里程碑接力模式（默认）

```text
Agent A：独立完成 Mx 的实现、门禁、PR、merge、framework-mX 和分支清理
   ↓ 在干净 main 上提交结构化交接
Agent B：核验最终 main/tag，独立运行 M(x+1) 基线并完成 M(x+1)
   ↓ 在干净 main 上提交结构化交接
Agent A：核验新基线并完成 M(x+2)
```

该模式是本项目的常态。每一棒负责从预检到集成的完整闭环，两位 Agent 不同时处理仓库任务。

### 3.2 实现—审查模式（仅用户明确要求）

```text
Agent A（Owner）：实现当前里程碑并生成测试证据
Agent B（Reviewer）：锁定同一 commit，执行只读严格审查
Agent B：提交 PASS/FAIL 矩阵和最小复现
Agent A：只修复本里程碑 FAIL，并重新运行相关门禁
Agent B：复核最终 commit
Agent A：提交、PR、merge、tag、清理分支
```

Reviewer 不直接修改 Owner 分支。若用户要求 Reviewer 接管修复，双方必须先完成显式所有权转移。
需要实际运行 Unity/Test/Build 的审查必须在 Owner 停止写入后执行，优先使用独立干净 worktree。
测试只允许写入 `Library`、`TestResults`、`Builds` 等生成目录；若 Unity 改写 tracked 文件，
Reviewer 不得自行 restore 或提交，必须停止并把精确 diff 交给 Owner 处理。

### 3.3 有限并行模式（仅用户明确要求）

只有满足以下全部条件才允许并行写入：

- 用户明确要求并行；
- 使用不同 worktree 和不同分支；
- 任务可以独立验证；
- 文件所有权不重叠；
- 不跨越里程碑门禁；
- 合并顺序和负责集成的 Owner 已指定。

出现共同修改 Core、Schema、Scene、ProjectSettings、Packages、同一测试 Fixture 或同一文档时，
不允许并行，必须退回单写者模式。

## 4. 每一棒的职责

| 职责 | 当前 Owner | 下一棒 Successor |
|---|---|---|
| 确认规范远程、基线和 Unity 版本 | 必须 | 接管后必须重新核验 |
| 创建和修改实现分支 | 是 | 交接前不工作 |
| 输出不超过 10 条计划 | 必须 | 交接前不工作 |
| 实现、严格审查与最小修复 | 是，完成整棒 | 交接前不工作 |
| 运行测试、验证和适用构建 | 必须 | 接管后运行自己的基线 |
| 标记 PASS/FAIL/NOT RUN | 必须真实 | 接管后必须独立真实 |
| 创建/更新 PR、合并和适用的标签 | 是 | 交接前不工作 |
| 更新结果报告、执行日志和已知问题 | 是 | 接管后读取并复核 |
| 创建下一里程碑分支 | 当前里程碑结束后停止 | 接管并通过预检后执行 |

## 5. 分支和 Git 所有权

### 5.1 开始任务

Owner 必须依次核验：

1. `origin` fetch/push 都指向 `https://github.com/free-world-team/free-world.git`。
2. 当前工作树干净，或所有已有改动来源明确并属于任务。
3. `main` 可通过 fast-forward-only 同步。
4. `HEAD = origin/main`，且最近已验收的 `framework-mX` peeled commit 是 `main` 的祖先；只有
   中间没有非里程碑治理/修复提交时，三者才要求恰好指向同一 commit。
5. `ProjectSettings/ProjectVersion.txt` 与实际 Unity Editor 一致。
6. GitHub 身份具有读取、推送和 PR 所需权限。
7. 当前环境基线测试已经真实执行并记录。

任一项失败时状态为 `BLOCKED`，不得创建功能分支或继续实现。

### 5.2 开发期间

- 分支默认命名为 `codex/mX-short-name`；非里程碑治理任务使用 `codex/<task-name>`。
- 只有 Owner 推送当前分支。
- 仅在用户启用实现—审查例外模式时，Reviewer 以 commit SHA 审查，不依赖“最新分支”
  这种可移动描述；Owner 推送新提交后，旧审查结论自动过期。
- 不得把两个 Agent 的来源不明改动合并成一个提交。

### 5.3 集成结束

Owner 在全部门禁 PASS 后完成：

1. 检查 diff、提交图和 Scene。
2. 提交并推送功能分支。
3. 创建目标为 `main` 的 PR。
4. 更新结果报告、执行日志和已知问题。
5. 合并 PR。
6. 里程碑任务在最终 merge commit 创建 annotated `framework-mX` 标签并推送；非里程碑任务
   （包括治理或修复）不得创建、移动或复用 `framework-mX` 标签。
7. 删除远程和本地功能分支。
8. 切回 `main`，fast-forward-only 同步。
9. 核验工作树干净，并按任务类型检查基线：里程碑任务要求
   `HEAD = origin/main = framework-mX peeled commit`；非里程碑任务要求 `HEAD = origin/main`，且
   最近已验收的 `framework-mX` peeled commit 是 `HEAD` 的祖先。

完成第 9 项后，才允许发出正常交接。

正常交接点必须同时满足：

```text
branch = main
HEAD = origin/main
framework tag = 里程碑任务时 framework-mX peeled commit = HEAD；
                非里程碑任务时最近已验收 framework tag 是 HEAD 的祖先
worktree = CLEAN
功能分支 = 本地和远程均已删除
Unity/Test/Build/Git 操作 = 无正在运行项
```

只有任务被外部阻断或用户主动要求中途换人时，才允许在非干净点做紧急交接；这种交接必须
标为 `BLOCKED`，逐项列出未提交文件和正在运行的操作，不得描述为正常 READY 接力。

## 6. 交接包

每次所有权转移必须同时提供：

- 聊天中的结构化交接消息，使用 `Templates/AGENT_HANDOFF_TEMPLATE.md`；
- 已提交的 `Docs/Reports/<date>-mX-*.md`；
- 已更新的 `Docs/EXECUTION_LOG.md`；
- 已更新的 `Docs/KNOWN_ISSUES.md`；
- 可定位的 PR、commit，以及里程碑任务适用的 tag。

交接包必须区分三种信息：

| 类型 | 含义 |
|---|---|
| 已验证事实 | 有命令、日志、XML、Manifest、commit 或 PR 证据 |
| 已知限制 | 当前接受但有明确影响和后续处理阶段 |
| 下一步建议 | 非授权、非完成声明，接管者仍需按当前指令判断 |

不允许只发送“已经完成，可以继续”这类无法审计的交接。

正常轮换时，交接消息应简洁确认最终仓库状态和下一棒前置条件；实现细节由已提交报告承载，
不需要让两个 Agent 同时保持长时间在线或同步进度。

## 7. 接管 Agent 预检

Successor 收到交接并开始自己这一棒后，先只读执行：

1. 阅读 `AGENTS.md` 规定的完整文档顺序。
2. 核对规范仓库、origin、默认分支和 GitHub 权限。
3. `fetch --prune --tags`，检查远程已删除分支状态。
4. 检查工作树、diff 和最近提交图。
5. 核对交接中的 PR 状态、merge SHA、tag peeled SHA 和 Unity 版本。
6. 阅读实现报告、审查报告、执行日志和已知问题。
7. 确认没有阻止下一里程碑的 `OPEN` 问题。
8. 按下一里程碑提示词真实运行基线测试。

在用户只要求预检时，Successor 不得创建分支、修改代码或提前实现下一里程碑。

预检输出至少包含：实际仓库、origin fetch/push、当前分支、HEAD、origin/main、上一标签
peeled SHA、工作树状态、Unity 版本、GitHub 权限和 `READY/BLOCKED`。

## 8. 所有权转移

显式转移必须包含一句：

```text
从现在起，<Agent B> 是 <任务/里程碑> 的 Owner；<Agent A> 停止写入该分支。
```

转移前：

- 原 Owner 完成 PR、merge、分支清理，以及里程碑任务适用的 tag；
- 原 Owner 切回干净且已同步的 `main`；
- 原 Owner 停止文件修改和 Git 写操作；
- 给出精确分支和 commit SHA；
- 确认不存在仍在运行的 Unity、构建或 Git 操作。

转移后：

- 新 Owner 先核验工作树和 SHA；
- 旧 Owner 进入等待状态，除非用户再次安排任务；
- 发现交接后仍有并发写入时，双方立即停止，保留现场并报告用户。

## 9. 审查发现与冲突处理

- Reviewer 对每项使用 `PASS`、`FAIL` 或 `NOT RUN`。
- FAIL 必须包含文件/行、最小复现、实际结果、预期结果和根因。
- 只修复当前任务范围内的 FAIL；范围外问题登记到 `KNOWN_ISSUES` 或 Change Request。
- 两个 Agent 对架构解释不一致时，不通过互相覆盖代码解决；按 `AGENTS.md` 优先级查证。
- 仍无法确定且会改变设计时，停止相关实现并请求用户决定。
- Git 冲突由当前 Owner 处理；不得使用来源不明的 `ours/theirs` 批量覆盖。

## 10. 证据和报告规则

- 测试 XML、Unity 日志和构建产物可以位于忽略目录，但结果摘要必须写入已提交报告。
- 报告记录执行 Agent、分支、commit、命令、结果路径、计数和未执行原因。
- 任一代码修复使旧测试证据失效时，Owner 必须按风险重跑相关门禁。
- Reviewer 的只读静态检查不能替代 Unity 编译、测试、验证或构建。
- Reviewer 运行门禁时不得与 Owner 并发修改输入文件，也不得把 Unity 自动产生的 tracked
  改动混入审查结论或实现提交。
- 最终报告使用 `Templates/CODEX_RESULT_REPORT.md`；交接使用
  `Templates/AGENT_HANDOFF_TEMPLATE.md`。

## 11. 最小通信节奏

Owner 必须在以下事件通知用户：

- 认领任务和基线 SHA；
- 计划确定；
- 出现阻塞、规则冲突或来源不明改动；
- 实现完成并冻结审查 SHA；
- 审查 FAIL 及修复完成；
- PR 创建、合并、适用的标签和分支清理完成；
- 正式交接。

Successor 接管后必须在以下事件通知用户：

- 已开始接管及核验的基线 commit/tag；
- 接管预检的 READY/BLOCKED；
- 自己这一棒的计划和实际基线结果。

## 12. 禁止事项

- 两个 Agent 同时写同一工作树或同一分支。
- 在默认接力模式下，两个 Agent 同时运行仓库任务。
- 一个 Agent 在另一个 Agent 的 Unity/Test/Build 运行期间修改输入文件。
- 未完成交接就继续实现下一里程碑。
- 把聊天摘要当成 Git 或测试事实。
- 复用上一 Agent 的 PASS 冒充当前环境基线。
- 未锁定 commit SHA 就执行“最终审查”。
- 未经用户授权扩大到新的里程碑、外部系统或正式内容。
- 为解决协作冲突使用 `git reset --hard`、批量 restore、强推或删除未知改动。
